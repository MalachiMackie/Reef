using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class ClassSignature : ITypeSignature
    {
        public static string TupleFieldName(int index) => $"Item{index}";

        public static Lazy<ClassSignature> UInt64 { get; } = new(() => new ClassSignature()
        { Id = DefId.UInt64, TypeParameters = [], Name = "u64", Fields = [], Functions = [], Boxed = false, IsPublic = true });

        public required bool Boxed { get; init; }
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields
        {
            get => Initialized ? field : throw new InvalidOperationException("Signature is not initialized");
            init;
        }
        public required IReadOnlyList<FunctionSignature> Functions
        {
            get => Initialized ? field : throw new InvalidOperationException("Signature is not initialized");
            init;
        }
        public required string Name { get; init; }
        public required DefId Id { get; init; }
        public required bool IsPublic { get; init; }
        public bool Initialized { get; set; }
    }

    private static InstantiatedClass InstantiateClass(ClassSignature signature, Token? boxedSpecifier)
    {
        return InstantiatedClass.Create(
            signature,
            [],
            boxedSpecifier switch
            {
                { Type: TokenType.Boxed } => true,
                { Type: TokenType.Unboxed } => false,
                _ => signature.Boxed
            });
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
            return InstantiatedClass.Create(signature, [], boxed);
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            AddError(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        var instantiatedClass = InstantiatedClass.Create(signature, [.. typeArguments.Select(x => x.Item1)], boxed);

        for (var i = 0; i < Math.Min(instantiatedClass.TypeArguments.Count, typeArguments.Count); i++)
        {
            var (typeReference, referenceSourceRange) = typeArguments[i];
            ExpectType(typeReference, instantiatedClass.TypeArguments[i], referenceSourceRange);
        }

        return instantiatedClass;
    }

    private bool TryInstantiateClassFunction(
        InstantiatedClass @class,
        string functionName,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        SourceRange typeArgumentsSourceRange,
        [NotNullWhen(true)] out InstantiatedFunction? function)
    {
        var signature = @class.Signature.Functions.FirstOrDefault(x => x.Name == functionName);

        if (signature is null)
        {
            function = null;
            return false;
        }

        function = InstantiateFunction(signature, @class, typeArguments, typeArgumentsSourceRange, inScopeTypeParameters: []);
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

    // TODO: arrayType and ArrayTypeSignature can't be generic classes until we implement const generics
    public class ArrayType : ITypeReference, IInstantiatedGeneric
    {
        public ArrayType(
            ITypeReference? elementType,
            bool boxed,
            uint length)
        {
            ElementType = ArrayTypeSignature.Instance.ElementGenericPlaceholder.Instantiate(this, elementType);
            Boxed = boxed;
            Fields = [
                new TypeField
                {
                    IsMutable = false,
                    IsPublic = true,
                    IsStatic = false,
                    Name = "length",
                    StaticInitializer = null,
                    Type = InstantiatedClass.UInt64
                }
            ];
            Length = length;
        }

        public ArrayType(ITypeReference? elementType)
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
                    Name = "length",
                    StaticInitializer = null,
                    Type = InstantiatedClass.UInt64
                }
            ];
        }

        public IReadOnlyList<GenericTypeReference> TypeArguments => [ElementType];
        public GenericTypeReference ElementType { get; }
        public uint? Length { get; }
        public bool IsDynamic => Length is null;

        public bool Boxed { get; }

        public IReadOnlyList<TypeField> Fields { get; }
    }

    public class InstantiatedClass : ITypeReference, IInstantiatedGeneric
    {
        public InstantiatedClass CloneWithTypeFilter(Func<ITypeReference, ITypeReference> typeFilter)
        {
            var instantiatedTypeArguments = new List<GenericTypeReference>(Signature.TypeParameters.Count);
            var instantiatedClass = new InstantiatedClass(
                Signature,
                instantiatedTypeArguments,
                Boxed);

            instantiatedTypeArguments.AddRange(Signature.TypeParameters.Zip(TypeArguments)
                .Select(x =>
                {
                    return x.First.Instantiate(
                                        instantiatedClass,
                                        typeFilter(x.Second));
                }));

            return instantiatedClass;

        }

        private InstantiatedClass(
            ClassSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            bool boxed)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Boxed = boxed;
        }

        public static InstantiatedClass Create(ClassSignature signature, IReadOnlyList<ITypeReference> typeArguments, bool boxed)
        {
            var typeArgumentReferences = new List<GenericTypeReference>(typeArguments.Count);
            var instantiatedClass = new InstantiatedClass(signature, typeArgumentReferences, boxed);

            typeArgumentReferences.AddRange(
                signature.TypeParameters.Select((t, i) => t.Instantiate(instantiatedClass, typeArguments.Count > i ? typeArguments[i] : null)));

            return instantiatedClass;
        }

        public static InstantiatedClass UInt64 => new(ClassSignature.UInt64.Value, [], boxed: ClassSignature.UInt64.Value.Boxed);

        public bool Boxed { get; }

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ClassSignature Signature { get; }

        private IReadOnlyList<TypeField>? _fields;
        public IReadOnlyList<TypeField> GetFields()
        {
            _fields ??= [.. Signature.Fields
                            .Select(field => new TypeField()
                            {
                                IsMutable = field.IsMutable,
                                IsPublic = field.IsPublic,
                                IsStatic = field.IsStatic,
                                Name = field.Name,
                                StaticInitializer = field.StaticInitializer,
                                Type = InstantiateTypeReference(field.Type, TypeArguments, [])
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
