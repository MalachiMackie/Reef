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

    private static IEnumerable<IUnionVariant> GetUnionVariants(InstantiatedUnion union)
    {
        var boxedInstantiatedUnion = new InstantiatedUnion(union.Signature, union.TypeArguments, boxed: true);
        var unboxedInstantiatedUnion = new InstantiatedUnion(union.Signature, union.TypeArguments, boxed: false);

        return union.Signature.Variants.Select(variant =>
            {
                switch (variant)
                {
                    case TupleUnionVariant tuple:
                        {
                            var createFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                            var unboxedCreateFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                            foreach (var parameter in tuple.BoxedCreateFunction.Parameters)
                            {
                                createFunctionParameters[parameter.Key] = parameter.Value with
                                {
                                    Type = InstantiateTypeReference(parameter.Value.Type, union.TypeArguments)
                                };
                            }
                            foreach (var parameter in tuple.UnboxedCreateFunction.Parameters)
                            {
                                unboxedCreateFunctionParameters[parameter.Key] = parameter.Value with
                                {
                                    Type = InstantiateTypeReference(parameter.Value.Type, union.TypeArguments)
                                };
                            }

                            return new TupleUnionVariant
                            {
                                Name = tuple.Name,
                                TupleMembers =
                                [
                                    ..tuple.TupleMembers.Select(x => InstantiateTypeReference(x, union.TypeArguments))
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
                                ..classVariant.Fields.Select(y => y with { Type = InstantiateTypeReference(y.Type, union.TypeArguments) })
                            ]
                        };
                    default:
                        return variant;
                }
            });
    }

    private static IUnionVariant? GetUnionVariant(InstantiatedUnion union, string name)
    {
        var variant = union.Signature.Variants.FirstOrDefault(x => x.Name == name);
        if (variant is null)
        {
            return null;
        }

        var boxedInstantiatedUnion = new InstantiatedUnion(union.Signature, union.TypeArguments, boxed: true);
        var unboxedInstantiatedUnion = new InstantiatedUnion(union.Signature, union.TypeArguments, boxed: false);

        switch (variant)
        {
            case TupleUnionVariant tuple:
                {
                    var createFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                    var unboxedCreateFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
                    foreach (var parameter in tuple.BoxedCreateFunction.Parameters)
                    {
                        createFunctionParameters[parameter.Key] = parameter.Value with
                        {
                            Type = InstantiateTypeReference(parameter.Value.Type, union.TypeArguments)
                        };
                    }
                    foreach (var parameter in tuple.UnboxedCreateFunction.Parameters)
                    {
                        unboxedCreateFunctionParameters[parameter.Key] = parameter.Value with
                        {
                            Type = InstantiateTypeReference(parameter.Value.Type, union.TypeArguments)
                        };
                    }

                    return new TupleUnionVariant
                    {
                        Name = tuple.Name,
                        TupleMembers =
                        [
                            ..tuple.TupleMembers.Select(x => InstantiateTypeReference(x, union.TypeArguments))
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
                        ..classVariant.Fields.Select(y => y with { Type = InstantiateTypeReference(y.Type, union.TypeArguments) })
                    ]
                };
            default:
                return variant;
        }
    }

    public class InstantiatedUnion : ITypeReference, IInstantiatedGeneric
    {
        public InstantiatedUnion CloneWithTypeFilter(Func<ITypeReference, ITypeReference> typeFilter)
        {
            var instantiatedTypeArguments = new List<GenericTypeReference>(Signature.TypeParameters.Count);
            var instantiatedUnion = new InstantiatedUnion(
                Signature,
                instantiatedTypeArguments,
                Boxed);

            instantiatedTypeArguments.AddRange(Signature.TypeParameters.Zip(TypeArguments)
                .Select(x =>
                {
                    return x.First.Instantiate(
                        instantiatedUnion,
                        typeFilter(x.Second));
                }));

            return instantiatedUnion;
        }

        public InstantiatedUnion(
            UnionSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            bool boxed)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Boxed = boxed;
        }

        public static InstantiatedUnion Create(UnionSignature signature, IReadOnlyList<ITypeReference> typeArguments, bool boxed)
        {
            var typeArgumentReferences = new List<GenericTypeReference>();

            var instantiatedUnion = new InstantiatedUnion(signature, typeArgumentReferences, boxed);

            typeArgumentReferences.AddRange(signature.TypeParameters.Select((x, i) =>
                x.Instantiate(instantiatedUnion, typeArguments.ElementAtOrDefault(i))));

            return instantiatedUnion;
        }

        public UnionSignature Signature { get; }

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
