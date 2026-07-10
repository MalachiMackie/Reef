using System.Diagnostics.CodeAnalysis;
using System.Text;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class ClassSignature : ITypeSignature
    {
        public static string TupleFieldName(int index) => $"Item{index}";

        public required bool Boxed { get; init; }
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields
        {
            get => Initialized ? field : throw new InvalidOperationException("Signature is not initialized");
            init;
        }
        public required IReadOnlyList<FunctionSignature> Functions
        {
            get => Initialized ? field : throw new InvalidOperationException($"{Name} Signature is not initialized");
            init;
        }
        public required string Name { get; init; }
        public required DefId Id { get; init; }
        public required bool IsPublic { get; init; }
        public required IReadOnlyList<AttributeReference> Attributes { get; init; }
        public bool Initialized { get; set; }
    }

    private static InstantiatedClass InstantiateClass(ClassSignature signature, Token? boxedSpecifier, ITypeReference u64Type)
    {
        return InstantiatedClass.Create(
            signature,
            [],
            boxedSpecifier switch
            {
                { Type: TokenType.Boxed } => true,
                { Type: TokenType.Unboxed } => false,
                _ => signature.Boxed,
            },
            u64Type);
    }

    private InstantiatedClass InstantiateClass(
        ClassSignature signature,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        Token? boxedSpecifier,
        SourceRange sourceRange)
    {
        var boxed = boxedSpecifier switch
        {
            { Type: TokenType.Boxed } => true,
            { Type: TokenType.Unboxed } => false,
            _ => signature.Boxed
        };

        if (typeArguments.Count <= 0)
        {
            return InstantiatedClass.Create(signature, [], boxed, UInt64());
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            AddError(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        var instantiatedClass = InstantiatedClass.Create(signature, [.. typeArguments.Select(x => x.Item1)], boxed, UInt64());

        for (var i = 0; i < Math.Min(instantiatedClass.TypeArguments.Count, typeArguments.Count); i++)
        {
            var (typeReference, referenceSourceRange) = typeArguments[i];
            ExpectType(typeReference, instantiatedClass.TypeArguments[i], referenceSourceRange);
        }

        return instantiatedClass;
    }

    private bool TryInstantiateClassFunction(
        ITypeReference ownerType,
        ClassSignature classSignature,
        string functionName,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        SourceRange typeArgumentsSourceRange,
        [NotNullWhen(true)] out InstantiatedFunction? function)
    {
        var signature = classSignature.Functions.FirstOrDefault(x => x.Name == functionName);

        if (signature is null)
        {
            function = null;
            return false;
        }

        function = InstantiateFunction(signature, ownerType, typeArguments, typeArgumentsSourceRange, inScopeTypeParameters: []);
        return true;
    }

    private ClassSignature GetClassSignature(DefId id)
    {
        return _moduleSignatures[id.ModuleId].Classes.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"No type with id {id.FullName} in module {id.ModuleId.Value}");
    }

    private InstantiatedClass InstantiateTuple(
        IReadOnlyList<(ITypeReference, SourceRange)> types,
        SourceRange sourceRange,
        Token? boxingSpecifier)
    {
        if (types.Count == 0)
        {
            return Unit();
        }
        if (types.Count > 10)
        {
            throw new InvalidOperationException("Tuple can contain at most 10 items");
        }

        var signature = GetClassSignature(DefId.Tuple(types.Count));

        return InstantiateClass(signature, types, boxingSpecifier, sourceRange);
    }

    public class ArrayTypeSignature : ITypeSignature
    {
        public static ArrayTypeSignature Instance { get; } = new();
        public string Name => "array";
        public DefId Id => DefId.Array;
        public GenericPlaceholder ElementGenericPlaceholder { get; }
        public IReadOnlyList<GenericPlaceholder> TypeParameters => [ElementGenericPlaceholder];
        public IReadOnlyList<AttributeReference> Attributes => [];
        public bool Boxed => true;
        public bool IsPublic => true;

        private ArrayTypeSignature()
        {
            ElementGenericPlaceholder =
                new GenericPlaceholder
                {
                    OwnerType = this,
                    Constraints = [],
                    GenericName = "TElement"
                };
        }
    }

    public record VariantOfType(InstantiatedUnion Union) : ITypeReference;

    // TODO: arrayType and ArrayTypeSignature can't be generic classes until we implement const generics
    public class ArrayType : ITypeReference, IInstantiatedGeneric
    {
        public ArrayType(
            ITypeReference? elementType,
            bool boxed,
            ulong length,
            ITypeReference u64Type)
        {
            ElementType = ArrayTypeSignature.Instance.ElementGenericPlaceholder.Instantiate(this, elementType);
            Boxed = boxed;
            Fields = [
                new TypeField
                {
                    IsMutable = false,
                    IsPublic = true,
                    IsStatic = false,
                    NameToken = Token.Identifier("length", SourceSpan.Default),
                    StaticInitializer = null,
                    Type = u64Type
                }
            ];
            Length = length;
        }

        public ArrayType(ITypeReference? elementType, ITypeReference u64Type)
        {
            ElementType = ArrayTypeSignature.Instance.ElementGenericPlaceholder.Instantiate(this, elementType);
            // dynamic array has to be boxed
            Boxed = true;
            Fields = [
                new TypeField
                {
                    IsMutable = false,
                    IsPublic = true,
                    IsStatic = false,
                    NameToken = Token.Identifier("length", SourceSpan.Default),
                    StaticInitializer = null,
                    Type = u64Type
                }
            ];
        }

        public IReadOnlyList<GenericTypeReference> TypeArguments => [ElementType];
        public GenericTypeReference ElementType { get; }
        public ulong? Length { get; }
        public bool IsDynamic => Length is null;

        public bool Boxed { get; }

        public IReadOnlyList<TypeField> Fields { get; }
    }

    public class SelfTypeReference : ITypeReference, IInstantiatedGeneric
    {
        public SelfTypeReference(ITypeSignature signature)
        {
            Signature = signature;
            TypeArguments = [.. signature.TypeParameters.Select(x => x.Instantiate(this, x))];
        }

        public ITypeSignature Signature { get; }
        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }

        public override string ToString()
        {
            var sb = new StringBuilder(Signature.Id.FullName);
            if (TypeArguments.Count == 0)
            {
                return sb.ToString();
            }

            sb.Append("::<");
            sb.AppendJoin(",", TypeArguments.Select(x => x));
            sb.Append('>');

            return sb.ToString();
        }
    }

    public class InstantiatedClass : ITypeReference, IInstantiatedGeneric
    {
        public InstantiatedClass CloneWithTypeFilter(Func<ITypeReference, ITypeReference> typeFilter)
        {
            var instantiatedTypeArguments = new List<GenericTypeReference>(Signature.TypeParameters.Count);
            var instantiatedClass = new InstantiatedClass(
                Signature,
                instantiatedTypeArguments,
                Boxed,
                _u64Type);

            instantiatedTypeArguments.AddRange(Signature.TypeParameters.Zip(TypeArguments)
                .Select(x =>
                {
                    return x.First.Instantiate(
                                        instantiatedClass,
                                        typeFilter(x.Second));
                }));

            return instantiatedClass;

        }

        public InstantiatedClass(
            ClassSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            bool boxed,
            ITypeReference u64Type)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Boxed = boxed;
            _u64Type = u64Type;
        }

        public static InstantiatedClass Create(ClassSignature signature, IReadOnlyList<ITypeReference> typeArguments, bool boxed, ITypeReference u64Type)
        {
            var typeArgumentReferences = new List<GenericTypeReference>(typeArguments.Count);
            var instantiatedClass = new InstantiatedClass(signature, typeArgumentReferences, boxed, u64Type);

            typeArgumentReferences.AddRange(
                signature.TypeParameters.Select((t, i) => t.Instantiate(instantiatedClass, typeArguments.Count > i ? typeArguments[i] : null)));

            return instantiatedClass;
        }

        public bool Boxed { get; }

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ClassSignature Signature { get; }

        private readonly ITypeReference _u64Type;

        private IReadOnlyList<TypeField>? _fields;
        public IReadOnlyList<TypeField> GetFields()
        {
            _fields ??= [.. Signature.Fields
                            .Select(field => new TypeField()
                            {
                                IsMutable = field.IsMutable,
                                IsPublic = field.IsPublic,
                                IsStatic = field.IsStatic,
                                NameToken = field.NameToken,
                                StaticInitializer = field.StaticInitializer,
                                Type = InstantiateTypeReference(field.Type, TypeArguments, [], _u64Type, this)
                            })];
            return _fields;
        }

        public bool IsSameSignature(InstantiatedClass other)
        {
            return Signature.Id == other.Signature.Id;
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Signature.Id.FullName}");
            if (TypeArguments.Count == 0)
            {
                return sb.ToString();
            }

            sb.Append("::<");
            sb.AppendJoin(",", TypeArguments.Select(x => x));
            sb.Append('>');

            return sb.ToString();
        }

        public bool MatchesSignature(ClassSignature currentTypeSignature)
        {
            return Signature.Id == currentTypeSignature.Id;
        }
    }

}
