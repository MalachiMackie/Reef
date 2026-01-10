using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class ClassSignature : ITypeSignature
    {
        public static string TupleFieldName(int index) => $"Item{index}";

        public static ClassSignature Unit { get; } = new()
        { Id = DefId.Unit, TypeParameters = [], Name = "Unit", Fields = [], Functions = [], Boxed = false };

        public static ClassSignature String { get; } = new()
        {
            Id = DefId.String,
            TypeParameters = [],
            Name = "string",
            Fields = [
                new TypeField
                {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "Length",
                    StaticInitializer = null,
                    Type = InstantiatedClass.UInt64
                },
                new TypeField
                {
                    IsPublic = false,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "StartChar",
                    StaticInitializer = null,
                    Type = InstantiatedClass.RawPointer
                }
            ],
            Functions = [],
            Boxed = false
        };

        public static ClassSignature Int64 => new()
        { Id = DefId.Int64, TypeParameters = [], Name = "i64", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature Int32 => new()
        { Id = DefId.Int32, TypeParameters = [], Name = "i32", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature Int16 => new()
        { Id = DefId.Int16, TypeParameters = [], Name = "i16", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature Int8 => new()
        { Id = DefId.Int8, TypeParameters = [], Name = "i8", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature UInt64 => new()
        { Id = DefId.UInt64, TypeParameters = [], Name = "u64", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature UInt32 => new()
        { Id = DefId.UInt32, TypeParameters = [], Name = "u32", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature UInt16 => new()
        { Id = DefId.UInt16, TypeParameters = [], Name = "u16", Fields = [], Functions = [], Boxed = false };
        public static ClassSignature UInt8 => new()
        { Id = DefId.UInt8, TypeParameters = [], Name = "u8", Fields = [], Functions = [], Boxed = false };

        public static ClassSignature RawPointer => new()
        { Id = DefId.RawPointer, TypeParameters = [], Name = "rawPointer", Fields = [], Functions = [], Boxed = false };

        public static ClassSignature Boolean => new()
        { Id = DefId.Boolean, TypeParameters = [], Name = "bool", Fields = [], Functions = [], Boxed = false };

        public static ClassSignature Never => new()
        { Id = DefId.Never, TypeParameters = [], Name = "!", Fields = [], Functions = [], Boxed = false };

        public static IEnumerable<ITypeSignature> BuiltInTypes { get; } = [
            Unit,
            String,
            Int64,
            Int32,
            Int16,
            Int8,
            UInt64,
            UInt32,
            UInt16,
            UInt8,
            Never,
            Boolean
        ];

        public required bool Boxed { get; init; }
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required string Name { get; init; }
        public required DefId Id { get; init; }

        private static readonly ConcurrentDictionary<int, ClassSignature> CachedFunctionClasses = [];

        public static ClassSignature Function(int parameterCount)
        {
            // plus 1 for return value
            var typeParamsCount = parameterCount + 1;

            if (CachedFunctionClasses.TryGetValue(typeParamsCount, out var cachedSignature))
            {
                return cachedSignature;
            }

            var typeParameters = new List<GenericPlaceholder>();

            var functions = new List<FunctionSignature>();

            var functionName = $"Function`{typeParamsCount}";

            var signature = new ClassSignature
            {
                Id = DefId.FunctionObject(parameterCount),
                Name = functionName,
                TypeParameters = typeParameters,
                // there are really two fields here. The function's closure or `this` argument, and the function pointer itself.
                // but these are not represented in the type system, they only happen when lowering
                Fields = [null!, null!],
                Functions = functions,
                Boxed = true
            };

            var callFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();

            var functionSignature = new FunctionSignature(
                new DefId(signature.Id.ModuleId, signature.Id.FullName + "__Call"),
                Token.Identifier("Call", SourceSpan.Default),
                [],
                callFunctionParameters,
                IsStatic: false,
                IsMutable: false,
                Expressions: [],
                true)
            {
                ReturnType = null!,
                OwnerType = signature
            };
            functions.Add(functionSignature);


            typeParameters.AddRange(Enumerable.Range(0, typeParamsCount).Select(i => new GenericPlaceholder
            {
                GenericName = i == typeParamsCount - 1 ? "TReturn" : $"TParam{i}",
                OwnerType = signature,
                Constraints = []
            }));

            functionSignature.ReturnType = typeParameters[^1];

            foreach (var i in Enumerable.Range(0, parameterCount))
            {
                var name = $"arg{i}";
                callFunctionParameters.Add(name, new FunctionSignatureParameter(
                    functionSignature,
                    Token.Identifier(name, SourceSpan.Default),
                    typeParameters[i],
                    Mutable: false,
                    ParameterIndex: (uint)i
                ));
            }


            CachedFunctionClasses[typeParamsCount] = signature;

            return signature;
        }

        private static readonly ConcurrentDictionary<ushort, ClassSignature> CachedTupleSignatures = [];
        public static ClassSignature Tuple(ushort elementCount)
        {
            if (elementCount < 2)
            {
                throw new InvalidOperationException("Tuple must have at least two items");
            }

            if (CachedTupleSignatures.TryGetValue(elementCount, out var cachedSignature))
            {
                return cachedSignature;
            }

            var typeParameters = new List<GenericPlaceholder>(elementCount);
            var fields = new List<TypeField>();
            var name = $"Tuple`{elementCount}";
            var signature = new ClassSignature
            {
                Id = DefId.Tuple(elementCount),
                TypeParameters = typeParameters,
                Name = name,
                Fields = fields,
                Functions = [],
                Boxed = false
            };
            typeParameters.AddRange(Enumerable.Range(0, elementCount).Select(x => new GenericPlaceholder
            {
                GenericName = $"T{x}",
                OwnerType = signature,
                Constraints = []
            }));

            fields.AddRange(Enumerable.Range(0, elementCount).Select(i => new TypeField
            {
                // todo: verify this
                IsMutable = false,
                Name = TupleFieldName(i),
                IsStatic = false,
                Type = typeParameters[i],
                IsPublic = true,
                StaticInitializer = null,
            }));

            CachedTupleSignatures[elementCount] = signature;

            return signature;
        }
    }

    private static InstantiatedClass InstantiateClass(ClassSignature signature, Token? boxedSpecifier)
    {
        return InstantiatedClass.Create(
            signature,
            [],
            boxedSpecifier switch
            {
                {Type: TokenType.Boxed} => true,
                {Type: TokenType.Unboxed} => false,
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
            _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        var instantiatedClass = InstantiatedClass.Create(signature, [..typeArguments.Select(x => x.Item1)], boxed);

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

    private InstantiatedClass InstantiateTuple(
        IReadOnlyList<(ITypeReference, SourceRange)> types,
        SourceRange sourceRange,
        Token? boxingSpecifier)
    {
        return types.Count switch
        {
            0 => throw new InvalidOperationException("Tuple must not be empty"),
            > 10 => throw new InvalidOperationException("Tuple can contain at most 10 items"),
            _ => InstantiateClass(ClassSignature.Tuple((ushort)types.Count), types, boxingSpecifier, sourceRange)
        };
    }

    public class InstantiatedClass : ITypeReference, IInstantiatedGeneric
    {
        public InstantiatedClass CloneWithTypeArguments(IReadOnlyList<GenericTypeReference> typeArguments)
        {
            return new InstantiatedClass(
                Signature,
                typeArguments,
                Fields,
                Boxed);
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
                return type switch
                {
                    GenericTypeReference genericTypeReference => typeArgumentReferences.First(y =>
                        y.GenericName == genericTypeReference.GenericName),
                    GenericPlaceholder placeholder =>
                        typeArgumentReferences.First(y => y.GenericName == placeholder.GenericName),
                    InstantiatedUnion union => union.CloneWithTypeArguments([
                        ..union.TypeArguments.Select(HandleType).Cast<GenericTypeReference>()
                    ]),
                    InstantiatedClass klass => klass.CloneWithTypeArguments([..klass.TypeArguments.Select(HandleType).Cast<GenericTypeReference>()]),
                    _ => type
                };
            }
        }

        public static InstantiatedClass String => new(ClassSignature.String, [], [], boxed: ClassSignature.String.Boxed);
        public static InstantiatedClass Boolean => new(ClassSignature.Boolean, [], [], boxed: ClassSignature.Boolean.Boxed);

        public static InstantiatedClass Int64 => new(ClassSignature.Int64, [], [], boxed: ClassSignature.Int64.Boxed);
        public static InstantiatedClass Int32 => new(ClassSignature.Int32, [], [], boxed: ClassSignature.Int32.Boxed);
        public static InstantiatedClass Int16 => new(ClassSignature.Int16, [], [], boxed: ClassSignature.Int16.Boxed);
        public static InstantiatedClass Int8 => new(ClassSignature.Int8, [], [], boxed: ClassSignature.Int8.Boxed);
        public static InstantiatedClass UInt64 => new(ClassSignature.UInt64, [], [], boxed: ClassSignature.UInt64.Boxed);
        public static InstantiatedClass UInt32 => new(ClassSignature.UInt32, [], [], boxed: ClassSignature.UInt32.Boxed);
        public static InstantiatedClass UInt16 => new(ClassSignature.UInt16, [], [], boxed: ClassSignature.UInt16.Boxed);
        public static InstantiatedClass UInt8 => new(ClassSignature.UInt8, [], [], boxed: ClassSignature.UInt8.Boxed);
        public static InstantiatedClass RawPointer => new(ClassSignature.RawPointer, [], [], boxed: ClassSignature.RawPointer.Boxed);

        // todo: need some sort of inferred int type

        public static IReadOnlyList<InstantiatedClass> IntTypes { get; } = [
            Int64,
            Int32,
            Int16,
            Int8,
            UInt64,
            UInt32,
            UInt16,
            UInt8,
        ];

        public static InstantiatedClass Unit { get; } = new(ClassSignature.Unit, [], [], boxed: ClassSignature.Unit.Boxed);

        public static InstantiatedClass Never { get; } = new(ClassSignature.Never, [], [], boxed: ClassSignature.Never.Boxed);
        
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

        public bool MatchesSignature(ClassSignature currentTypeSignature)
        {
            return Signature == currentTypeSignature;
        }
    }

}
