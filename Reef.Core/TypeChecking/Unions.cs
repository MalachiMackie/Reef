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
                TypeParameters = typeParameters,
                Name = "result",
                Variants = variants,
                Functions = []
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

            variants[0] = new TupleUnionVariant
            {
                Name = "Ok",
                TupleMembers = [typeParameters[0]]
            };
            variants[1] = new TupleUnionVariant
            {
                Name = "Error",
                TupleMembers = [typeParameters[1]]
            };

            Result = resultSignature;
            BuiltInTypes = [Result];
        }

        public static UnionSignature Result { get; }
        public Guid Id { get; } = Guid.NewGuid();
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<IUnionVariant> Variants { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }

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

    private InstantiatedUnion InstantiateUnion(UnionSignature signature, IReadOnlyList<(ITypeReference, SourceRange)> typeArguments, SourceRange sourceRange)
    {
        // when instantiating, create new generic type references so they can be resolved
        GenericTypeReference[] typeArgumentReferences =
        [
            ..signature.TypeParameters.Select(x => x.Instantiate())
        ];

        if (typeArguments.Count <= 0)
        {
            return new InstantiatedUnion(signature, typeArgumentReferences);
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

        return new InstantiatedUnion(signature, typeArgumentReferences);
    }

    private InstantiatedUnion InstantiateResult(SourceRange sourceRange)
    {
        return InstantiateUnion(UnionSignature.Result, [], sourceRange);
    }

    public class InstantiatedUnion : ITypeReference
    {
        public InstantiatedUnion(UnionSignature signature, IReadOnlyList<GenericTypeReference> typeArguments)
        {
            Signature = signature;
            TypeArguments = typeArguments;

            Variants =
            [
                ..signature.Variants.Select(x => x switch
                {
                    TupleUnionVariant tuple => new TupleUnionVariant
                    {
                        Name = tuple.Name,
                        TupleMembers =
                        [
                            ..tuple.TupleMembers.Select(y => y switch
                            {
                                GenericTypeReference genericTypeReference => typeArguments.First(z =>
                                    z.GenericName == genericTypeReference.GenericName),
                                GenericPlaceholder placeholder => typeArguments.First(z => z.GenericName == placeholder.GenericName),
                                _ => y
                            })
                        ]
                    },
                    ClassUnionVariant classVariant => new ClassUnionVariant
                    {
                        Name = classVariant.Name,
                        Fields =
                        [
                            ..classVariant.Fields.Select(y => y with { Type = y.Type switch
                            {
                                GenericTypeReference genericTypeReference => typeArguments.First(z =>
                                    z.GenericName == genericTypeReference.GenericName),
                                GenericPlaceholder placeholder => typeArguments.First(z => z.GenericName == placeholder.GenericName),
                                _ => y.Type
                            } })
                        ]
                    },
                    _ => x
                })
            ];
        }

        public UnionSignature Signature { get; }

        public IReadOnlyList<IUnionVariant> Variants { get; }

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
