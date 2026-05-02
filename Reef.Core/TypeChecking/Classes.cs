using System.Collections.Concurrent;
using System.Diagnostics;
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
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required string Name { get; init; }
        public required DefId Id { get; init; }
        public required bool IsPublic { get; init; }

        private static readonly ConcurrentDictionary<int, ClassSignature> CachedFunctionClasses = [];

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
        public InstantiatedClass CloneWithTypeArguments(IReadOnlyList<ITypeReference> typeArguments)
        {
            Debug.Assert(typeArguments.Count == Signature.TypeParameters.Count);
            var instantiatedTypeArguments = new List<GenericTypeReference>(typeArguments.Count);
            var instantiatedClass = new InstantiatedClass(
                Signature,
                instantiatedTypeArguments,
                Fields,
                Boxed);

            instantiatedTypeArguments.AddRange(Signature.TypeParameters.Zip(typeArguments)
                .Select(x => x.First.Instantiate(
                    instantiatedClass,
                    x.Second switch
                    {
                        GenericTypeReference { ResolvedType: var resolvedType } => resolvedType,
                        _ => x.Second
                    })));

            return instantiatedClass;

        }

        private InstantiatedClass(
            ClassSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            IReadOnlyList<TypeField> fields,
            bool boxed)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Fields = fields;
            Boxed = boxed;
        }

        public static InstantiatedClass Create(ClassSignature signature, IReadOnlyList<ITypeReference> typeArguments, bool boxed)
        {
            var fields = new List<TypeField>();

            var typeArgumentReferences = new List<GenericTypeReference>(typeArguments.Count);
            var instantiatedClass = new InstantiatedClass(signature, typeArgumentReferences, fields, boxed);

            typeArgumentReferences.AddRange(
                signature.TypeParameters.Select((t, i) => t.Instantiate(instantiatedClass, typeArguments.Count > i ? typeArguments[i] : null)));
            fields.AddRange(signature.Fields.Select(x => x with { Type = HandleType(x.Type) }));

            return instantiatedClass;

            ITypeReference HandleType(ITypeReference type)
            {
                try
                {
                    return type switch
                    {
                        GenericTypeReference { ResolvedType: null } genericTypeReference => typeArgumentReferences.FirstOrDefault(y =>
                            y.GenericName == genericTypeReference.GenericName) ?? genericTypeReference,
                        GenericTypeReference { ResolvedType: { } resolvedType } => resolvedType,
                        GenericPlaceholder placeholder =>
                            (ITypeReference?)typeArgumentReferences.FirstOrDefault(y => y.GenericName == placeholder.GenericName) ?? placeholder,
                        InstantiatedUnion union => union.CloneWithTypeArguments([
                            ..union.TypeArguments.Select(HandleType)
                        ]),
                        InstantiatedClass klass => klass.CloneWithTypeArguments([.. klass.TypeArguments.Select(HandleType)]),
                        ArrayType { Length: not null } arrayType => new ArrayType(HandleType(arrayType.ElementType), arrayType.Boxed, arrayType.Length.Value),
                        ArrayType { Length: null } arrayType => new ArrayType(HandleType(arrayType.ElementType)),
                        _ => throw new InvalidOperationException(type.GetType().ToString())
                    };
                }
                catch (Exception e)
                {
                    _ = e;
                    throw;
                }
            }
        }

        public static InstantiatedClass UInt64 => new(ClassSignature.UInt64.Value, [], [], boxed: ClassSignature.UInt64.Value.Boxed);

        public bool Boxed { get; }

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ClassSignature Signature { get; }

        public IReadOnlyList<TypeField> Fields { get; }

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
