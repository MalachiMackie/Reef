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
                OwnerType = resultSignature,
                Constraints = [],
            };
            typeParameters[1] = new GenericPlaceholder
            {
                GenericName = "TError",
                OwnerType = resultSignature,
                Constraints = [],
            };

            var okCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var errorCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var okCreateFunction = new FunctionSignature(
                    new DefId(DefId.Result.ModuleId, DefId.Result.FullName + "__Create__Ok"),
                    Token.Identifier("result__Create__Ok", SourceSpan.Default),
                    [],
                    okCreateParameters,
                    IsStatic: true,
                    IsMutable: false,
                    Expressions: [],
                    true,
                    IsMutableReturn: true)
            {
                ReturnType = InstantiatedUnion.Create(resultSignature, typeParameters, resultSignature.Boxed),
                OwnerType = resultSignature
            };
            var errorCreateFunction = new FunctionSignature(
                new DefId(DefId.Result.ModuleId, DefId.Result.FullName + "__Create__Error"),
                Token.Identifier("result__Create__Error", SourceSpan.Default),
                [],
                errorCreateParameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [],
                true,
                IsMutableReturn: true)
            {
                ReturnType = InstantiatedUnion.Create(resultSignature, typeParameters, resultSignature.Boxed),
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
        return InstantiatedUnion.Create(
            signature,
            [],
            boxingSpecifier switch
            {
                {Type: TokenType.Boxed} => true,
                {Type: TokenType.Unboxed} => false,
                _ => signature.Boxed
            });
    }

    private InstantiatedUnion InstantiateUnion(
        UnionSignature signature,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        Token? boxingSpecifier,
        SourceRange sourceRange)
    {
        if (typeArguments.Count <= 0)
        {
            return InstantiatedUnion.Create(signature, [], boxingSpecifier switch
            {
                {Type: TokenType.Boxed} => true,
                {Type: TokenType.Unboxed} => false,
                _ => signature.Boxed
            });
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            AddError(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        var instantiatedUnion = InstantiatedUnion.Create(signature, [..typeArguments.Select(x => x.Item1)], boxingSpecifier switch
            {
                {Type: TokenType.Boxed} => true,
                {Type: TokenType.Unboxed} => false,
                _ => signature.Boxed
            });
        
        for (var i = 0; i < Math.Min(instantiatedUnion.TypeArguments.Count, typeArguments.Count); i++)
        {
            var (typeArgument, referenceSourceRange) = typeArguments[i];

            ExpectType(typeArgument, instantiatedUnion.TypeArguments[i], referenceSourceRange);
        }

        return instantiatedUnion;
    }

    private InstantiatedUnion InstantiateResult(SourceRange sourceRange, Token? boxingSpecifier)
    {
        return InstantiateUnion(UnionSignature.Result, [], boxingSpecifier, sourceRange);
    }

    public class InstantiatedUnion : ITypeReference, IInstantiatedGeneric
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
        
        public static InstantiatedUnion Create(UnionSignature signature, IReadOnlyList<ITypeReference> typeArguments, bool boxed)
        {
            var variants = new List<IUnionVariant>();
            var typeArgumentReferences = new List<GenericTypeReference>();
            
            var instantiatedUnion = new InstantiatedUnion(signature, typeArgumentReferences, variants, boxed);

            typeArgumentReferences.AddRange(signature.TypeParameters.Select((x, i) =>
                x.Instantiate(instantiatedUnion, typeArguments.ElementAtOrDefault(i))));

            variants.AddRange(
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
                                ReturnType = instantiatedUnion, // the create function for a tuple variant within this instantiated union returns this type, so directly use instantiated union
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
            ]);

            return instantiatedUnion;

            ITypeReference HandleType(ITypeReference type)
            {
                return type switch
                {
                    GenericTypeReference genericTypeReference => typeArgumentReferences.First(z => z.GenericName == genericTypeReference.GenericName),
                    GenericPlaceholder placeholder => typeArgumentReferences.First(z => z.GenericName == placeholder.GenericName),
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
