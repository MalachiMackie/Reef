using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class UnionSignature : ITypeSignature
    {

        public required DefId Id { get; init; }
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<IUnionVariant> Variants { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required bool Boxed { get; init; }
        public required bool IsPublic { get; init; }

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
        public required FunctionSignature BoxedCreateFunction { get; init; }
        public required FunctionSignature UnboxedCreateFunction { get; init; }
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
                { Type: TokenType.Boxed } => true,
                { Type: TokenType.Unboxed } => false,
                _ => signature.Boxed
            });
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            AddError(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        var instantiatedUnion = InstantiatedUnion.Create(signature, [.. typeArguments.Select(x => x.Item1)], boxingSpecifier switch
        {
            { Type: TokenType.Boxed } => true,
            { Type: TokenType.Unboxed } => false,
            _ => signature.Boxed
        });

        for (var i = 0; i < Math.Min(instantiatedUnion.TypeArguments.Count, typeArguments.Count); i++)
        {
            var (typeArgument, referenceSourceRange) = typeArguments[i];

            ExpectType(typeArgument, instantiatedUnion.TypeArguments[i], referenceSourceRange);
        }

        return instantiatedUnion;
    }

    private UnionSignature GetUnionSignature(DefId id)
    {
        return _moduleSignatures[id.ModuleId].Unions.First(x => x.Id == id);
    }

    public class InstantiatedUnion : ITypeReference, IInstantiatedGeneric
    {
        public InstantiatedUnion CloneWithTypeFilter(Func<ITypeReference, ITypeReference> typeFilter)
        {
            var instantiatedTypeArguments = new List<GenericTypeReference>(Signature.TypeParameters.Count);
            var variants = new List<IUnionVariant>(Variants.Count);
            var instantiatedUnion = new InstantiatedUnion(
                Signature,
                instantiatedTypeArguments,
                variants,
                Boxed);

            var unboxedInstantiatedUnion = new InstantiatedUnion(
                Signature,
                instantiatedTypeArguments,
                variants,
                false);

            var boxedInstantiatedUnion = new InstantiatedUnion(
                Signature,
                instantiatedTypeArguments,
                variants,
                true);

            variants.AddRange(Variants.Select<IUnionVariant, IUnionVariant>(x => x switch
                            {
                                UnitUnionVariant v => v,
                                ClassUnionVariant v => new ClassUnionVariant
                                {
                                    Name = v.Name,
                                    Fields = [.. v.Fields.Select(y => new TypeField {
                                        IsMutable = y.IsMutable,
                                        IsPublic = y.IsPublic,
                                        IsStatic = y.IsStatic,
                                        Name = y.Name,
                                        StaticInitializer = y.StaticInitializer,
                                        Type = typeFilter(y.Type)
                                    })]
                                },
                                TupleUnionVariant v => new TupleUnionVariant
                                {
                                    Name = v.Name,
                                    TupleMembers = [.. v.TupleMembers.Select(typeFilter)],
                                    BoxedCreateFunction = new FunctionSignature(
                                        v.BoxedCreateFunction.Id,
                                        v.BoxedCreateFunction.NameToken,
                                        v.BoxedCreateFunction.TypeParameters,
                                        new OrderedDictionary<string, FunctionSignatureParameter>(
                                            v.BoxedCreateFunction.Parameters.Select(y => KeyValuePair.Create(
                                                y.Key,
                                                new FunctionSignatureParameter(y.Value.ContainingFunction,
                                                    y.Value.Name,
                                                    typeFilter(y.Value.Type),
                                                    y.Value.Mutable,
                                                    y.Value.ParameterIndex)
                                            ))
                                        ),
                                        v.BoxedCreateFunction.IsStatic,
                                        v.BoxedCreateFunction.IsMutable,
                                        v.BoxedCreateFunction.Expressions,
                                        v.BoxedCreateFunction.ExternName,
                                        v.BoxedCreateFunction.IsMutableReturn,
                                        v.BoxedCreateFunction.IsPublic,
                                        v.BoxedCreateFunction.Attributes
                                    )
                                    {
                                        OwnerType = v.BoxedCreateFunction.OwnerType,
                                        ReturnType = boxedInstantiatedUnion
                                    },
                                    UnboxedCreateFunction = new FunctionSignature(
                                        v.UnboxedCreateFunction.Id,
                                        v.UnboxedCreateFunction.NameToken,
                                        v.UnboxedCreateFunction.TypeParameters,
                                        new OrderedDictionary<string, FunctionSignatureParameter>(
                                            v.UnboxedCreateFunction.Parameters.Select(y => KeyValuePair.Create(
                                                y.Key,
                                                new FunctionSignatureParameter(y.Value.ContainingFunction,
                                                    y.Value.Name,
                                                    typeFilter(y.Value.Type),
                                                    y.Value.Mutable,
                                                    y.Value.ParameterIndex)
                                            ))
                                        ),
                                        v.UnboxedCreateFunction.IsStatic,
                                        v.UnboxedCreateFunction.IsMutable,
                                        v.UnboxedCreateFunction.Expressions,
                                        v.UnboxedCreateFunction.ExternName,
                                        v.UnboxedCreateFunction.IsMutableReturn,
                                        v.UnboxedCreateFunction.IsPublic,
                                        v.UnboxedCreateFunction.Attributes
                                    )
                                    {
                                        OwnerType = v.UnboxedCreateFunction.OwnerType,
                                        ReturnType = unboxedInstantiatedUnion
                                    },
                                },
                                _ => throw new InvalidOperationException(x.GetType().ToString())
                            }));

            instantiatedTypeArguments.AddRange(Signature.TypeParameters.Zip(TypeArguments)
                .Select(x =>
                {
                    return x.First.Instantiate(
                        instantiatedUnion,
                        typeFilter(x.Second));
                }));

            return instantiatedUnion;
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

            var boxedInstantiatedUnion = new InstantiatedUnion(signature, typeArgumentReferences, variants, boxed: true);
            var unboxedInstantiatedUnion = new InstantiatedUnion(signature, typeArgumentReferences, variants, boxed: false);

            typeArgumentReferences.AddRange(signature.TypeParameters.Select((x, i) =>
                x.Instantiate(boxed ? boxedInstantiatedUnion : unboxedInstantiatedUnion, typeArguments.ElementAtOrDefault(i))));

            variants.AddRange(
            [
                ..signature.Variants.Select(x =>
                {
                    switch (x)
                    {
                    case TupleUnionVariant tuple:
                    {
                        var createFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                        var unboxedCreateFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                        foreach (var parameter in tuple.BoxedCreateFunction.Parameters)
                        {
                            createFunctionParameters[parameter.Key] = parameter.Value with
                            {
                                Type = HandleType(parameter.Value.Type)
                            };
                        }
                        foreach (var parameter in tuple.UnboxedCreateFunction.Parameters)
                        {
                            unboxedCreateFunctionParameters[parameter.Key] = parameter.Value with
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
                            BoxedCreateFunction = tuple.BoxedCreateFunction with
                            {
                                ReturnType = boxedInstantiatedUnion, // the create function for a tuple variant within this instantiated union returns this type, so directly use instantiated union
                                Parameters = createFunctionParameters,
                            },
                            UnboxedCreateFunction = tuple.UnboxedCreateFunction with
                            {
                                ReturnType = unboxedInstantiatedUnion, // the create function for a tuple variant within this instantiated union returns this type, so directly use instantiated union
                                Parameters = unboxedCreateFunctionParameters,
                            },
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

            return boxed ? boxedInstantiatedUnion : unboxedInstantiatedUnion;

            ITypeReference HandleType(ITypeReference type)
            {
                return type switch
                {

                    GenericTypeReference { ResolvedType: null } genericTypeReference => typeArgumentReferences.First(y =>
                        y.GenericName == genericTypeReference.GenericName),
                    GenericTypeReference { ResolvedType: { } resolvedType } => HandleType(resolvedType),
                    GenericPlaceholder placeholder => (ITypeReference?)typeArgumentReferences.FirstOrDefault(z => z.GenericName == placeholder.GenericName) ?? placeholder,
                    InstantiatedUnion union => union.CloneWithTypeFilter(HandleType),
                    InstantiatedClass klass => klass.CloneWithTypeFilter(HandleType),
                    ArrayType { Length: not null } arrayType => new ArrayType(HandleType(arrayType.ElementType), arrayType.Boxed, arrayType.Length.Value),
                    ArrayType { IsDynamic: true } arrayType => new ArrayType(HandleType(arrayType.ElementType)),
                    FunctionObject functionObject => new FunctionObject(
                        [.. functionObject.Parameters.Select(x => new FunctionParameter(HandleType(x.Type), x.Mutable))],
                        HandleType(functionObject.ReturnType),
                        functionObject.MutableReturn,
                        functionObject.IsBoxed
                    ),
                    _ => throw new InvalidOperationException(type.GetType().ToString())
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
            var sb = new StringBuilder($"{Signature.Id.FullName}");
            if (TypeArguments.Count <= 0)
            {
                return sb.ToString();
            }

            sb.Append("::<");
            sb.AppendJoin(",", TypeArguments);
            sb.Append('>');

            return sb.ToString();
        }
    }
}
