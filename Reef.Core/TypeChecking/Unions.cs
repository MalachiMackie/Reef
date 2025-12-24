using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class UnionSignature : ITypeSignature
    {
        public static readonly IReadOnlyList<ITypeSignature> BuiltInTypes;

        static UnionSignature()
        {
            var variants = new TupleUnionVariant[2];
            var typeParameters = new GenericPlaceholder[2];
            var resultSignature = new UnionSignature
            {
                Id = DefId.Result,
                TypeParameters = typeParameters,
                Name = "result",
                Variants = variants,
                Functions = [],
                Boxed = false,
            };

            typeParameters[0] = new GenericPlaceholder
            {
                GenericName = "TValue",
                OwnerType = resultSignature
            };
            typeParameters[1] = new GenericPlaceholder
            {
                GenericName = "TError",
                OwnerType = resultSignature
            };

            var resultTypeReference = new InstantiatedUnion(
                resultSignature,
                [
                    typeParameters[0].Instantiate(),
                    typeParameters[1].Instantiate()
                ],
                null);

            var okCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var errorCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var okCreateFunction = new FunctionSignature(
                    new DefId(DefId.Result.ModuleId, DefId.Result.FullName + "__Create__Ok"),
                    Token.Identifier("result__Create__Ok", SourceSpan.Default),
                    [],
                    okCreateParameters,
                    IsStatic: true,
                    IsMutable: false,
                    Expressions: [])
            {
                ReturnType = resultTypeReference,
                OwnerType = resultSignature
            };
            var errorCreateFunction = new FunctionSignature(
                new DefId(DefId.Result.ModuleId, DefId.Result.FullName + "__Create__Error"),
                Token.Identifier("result__Create__Error", SourceSpan.Default),
                [],
                errorCreateParameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [])
            {
                ReturnType = resultTypeReference,
                OwnerType = resultSignature
            };

            okCreateParameters["Item0"] = new FunctionSignatureParameter(
                okCreateFunction,
                Token.Identifier("Item0", SourceSpan.Default),
                typeParameters[0],
                Mutable: false,
                ParameterIndex: 0);

            errorCreateParameters["Item0"] = new FunctionSignatureParameter(
                errorCreateFunction,
                Token.Identifier("Item0", SourceSpan.Default),
                typeParameters[1],
                Mutable: false,
                ParameterIndex: 0);

            variants[0] = new TupleUnionVariant
            {
                Name = "Ok",
                TupleMembers = [typeParameters[0]],
                CreateFunction = okCreateFunction
            };
            variants[1] = new TupleUnionVariant
            {
                Name = "Error",
                TupleMembers = [typeParameters[1]],
                CreateFunction = errorCreateFunction
            };

            Result = resultSignature;
            BuiltInTypes = [Result];
        }

        public static UnionSignature Result { get; }
        public required DefId Id { get; init; }
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<IUnionVariant> Variants { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required bool Boxed { get; init; }

        public required string Name { get; init; }
    }

    public interface IUnionVariant
    {
        string Name { get; }
    }

    // todo: better names
    public class TupleUnionVariant : IUnionVariant
    {
        public required IReadOnlyList<ITypeReference> TupleMembers { get; init; }
        public required string Name { get; init; }
        public required FunctionSignature CreateFunction { get; init; }
    }

    public class ClassUnionVariant : IUnionVariant
    {
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required string Name { get; init; }
    }

    public class UnitUnionVariant : IUnionVariant
    {
        public required string Name { get; init; }
    }

    private bool TryInstantiateUnionFunction(
        InstantiatedUnion union,
        string functionName,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        SourceRange typeArgumentsSourceRange,
        [NotNullWhen(true)] out InstantiatedFunction? function)
    {
        var signature = union.Signature.Functions.FirstOrDefault(x => x.Name == functionName);

        if (signature is null)
        {
            function = null;
            return false;
        }

        function = InstantiateFunction(signature, union, typeArguments, typeArgumentsSourceRange, inScopeTypeParameters: []);
        return true;
    }

    private static InstantiatedUnion InstantiateUnion(UnionSignature signature, Token? boxingSpecifier)
    {
        return new InstantiatedUnion(signature, [..signature.TypeParameters.Select(x => x.Instantiate())], boxingSpecifier);
    }

    private InstantiatedUnion InstantiateUnion(
        UnionSignature signature,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        Token? boxingSpecifier,
        SourceRange sourceRange)
    {
        // when instantiating, create new generic type references so they can be resolved
        GenericTypeReference[] typeArgumentReferences =
        [
            ..signature.TypeParameters.Select(x => x.Instantiate())
        ];

        if (typeArguments.Count <= 0)
        {
            return new InstantiatedUnion(signature, typeArgumentReferences, boxingSpecifier);
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        for (var i = 0; i < Math.Min(typeArguments.Count, typeArgumentReferences.Length); i++)
        {
            var (typeArgument, referenceSourceRange) = typeArguments[i];

            ExpectType(typeArgument, typeArgumentReferences[i], referenceSourceRange);
        }

        return new InstantiatedUnion(signature, typeArgumentReferences, boxingSpecifier);
    }

    private InstantiatedUnion InstantiateResult(SourceRange sourceRange, Token? boxingSpecifier)
    {
        return InstantiateUnion(UnionSignature.Result, [], boxingSpecifier, sourceRange);
    }

    public class InstantiatedUnion : ITypeReference
    {
        public InstantiatedUnion CloneWithTypeArguments(IReadOnlyList<GenericTypeReference> typeArguments)
        {
            return new InstantiatedUnion(
                Signature,
                typeArguments,
                Variants,
                Boxed);
        }

        private InstantiatedUnion(
            UnionSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            IReadOnlyList<IUnionVariant> variants,
            bool boxed)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Variants = variants;
            Boxed = boxed;
        }
        
        public InstantiatedUnion(UnionSignature signature, IReadOnlyList<GenericTypeReference> typeArguments, Token? boxingSpecifier)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Boxed = boxingSpecifier switch
            {
                {Type: TokenType.Boxed} => true,
                {Type: TokenType.Unboxed} => false,
                _ => signature.Boxed
            };

            Variants =
            [
                ..signature.Variants.Select(x => 
                {
                    switch (x)
                    {
                    case TupleUnionVariant tuple:
                    {
                        var createFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                        foreach (var parameter in tuple.CreateFunction.Parameters)
                        {
                            createFunctionParameters[parameter.Key] = parameter.Value with
                            {
                                Type = HandleType(parameter.Value.Type)
                            };
                        }
                        return new TupleUnionVariant
                        {
                            Name = tuple.Name,
                            TupleMembers =
                            [
                                ..tuple.TupleMembers.Select(HandleType)
                            ],
                            CreateFunction = tuple.CreateFunction with
                            {
                                ReturnType = this, // the create function for a tuple variant within this instantiated union returns this type, so directly use `this`
                                Parameters = createFunctionParameters,
                            }
                        };
                    }
                    case ClassUnionVariant classVariant:
                        return new ClassUnionVariant
                        {
                            Name = classVariant.Name,
                            Fields =
                            [
                                ..classVariant.Fields.Select(y => y with { Type = HandleType(y.Type) })
                            ]
                        };
                    default:
                        return x;
                    }
                })
            ];

            ITypeReference HandleType(ITypeReference type)
            {
                return type switch
                {
                    GenericTypeReference genericTypeReference => typeArguments.First(z => z.GenericName == genericTypeReference.GenericName),
                    GenericPlaceholder placeholder => typeArguments.First(z => z.GenericName == placeholder.GenericName),
                    InstantiatedUnion union => union.CloneWithTypeArguments([..union.TypeArguments.Select(HandleType).Cast<GenericTypeReference>()]),
                    InstantiatedClass klass => klass.CloneWithTypeArguments([..klass.TypeArguments.Select(HandleType).Cast<GenericTypeReference>()]),
                    _ => type
                };
            }
        }

        public UnionSignature Signature { get; }

        public IReadOnlyList<IUnionVariant> Variants { get; }
        
        public bool Boxed { get; }

        public string Name => Signature.Name;

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }

        public bool IsSameSignature(InstantiatedUnion other)
        {
            return Signature == other.Signature;
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Signature.Name}");
            if (TypeArguments.Count <= 0)
            {
                return sb.ToString();
            }

            sb.Append('<');
            sb.AppendJoin(",", TypeArguments.Select(x => x));
            sb.Append('>');

            return sb.ToString();
        }
    }
}
