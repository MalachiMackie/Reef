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
        { TypeParameters = [], Name = "Unit", Fields = [], Functions = [] };

        public static ClassSignature String { get; } = new()
        { TypeParameters = [], Name = "string", Fields = [], Functions = [] };

        public static ClassSignature Int { get; } = new()
        { TypeParameters = [], Name = "int", Fields = [], Functions = [] };

        public static ClassSignature RawPointer { get; } = new()
        { TypeParameters = [], Name = "rawPointer", Fields = [], Functions = [] };

        public static ClassSignature Boolean { get; } = new()
        { TypeParameters = [], Name = "bool", Fields = [], Functions = [] };

        public static ClassSignature Never { get; } = new()
        { TypeParameters = [], Name = "!", Fields = [], Functions = [] };

        public static IEnumerable<ITypeSignature> BuiltInTypes { get; } = [Unit, String, Int, Never, Boolean];

        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required string Name { get; init; }
        public Guid Id { get; } = Guid.NewGuid();

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

            var signature = new ClassSignature
            {
                Name = $"Function`{typeParamsCount}",
                TypeParameters = typeParameters,
                // there are really two fields here. The function's closure or `this` argument, and the function pointer itself.
                // but these are not represented in the type system, they only happen when lowering
                Fields = [null!, null!],
                Functions = functions
            };

            var callFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();

            var functionSignature = new FunctionSignature(
                Token.Identifier("Call", SourceSpan.Default),
                [],
                callFunctionParameters,
                IsStatic: false,
                IsMutable: false,
                Expressions: [])
            {
                ReturnType = null!,
                OwnerType = signature
            };
            functions.Add(functionSignature);


            typeParameters.AddRange(Enumerable.Range(0, typeParamsCount).Select(i => new GenericPlaceholder
            {
                GenericName = i == typeParamsCount - 1 ? "TReturn" : $"TParam{i}",
                OwnerType = signature
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

        private static readonly Dictionary<ushort, ClassSignature> CachedTupleSignatures = [];
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
            var signature = new ClassSignature
            {
                TypeParameters = typeParameters,
                Name = $"Tuple`{elementCount}",
                Fields = fields,
                Functions = []
            };
            typeParameters.AddRange(Enumerable.Range(0, elementCount).Select(x => new GenericPlaceholder
            {
                GenericName = $"T{x}",
                OwnerType = signature
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

    private InstantiatedClass InstantiateClass(ClassSignature signature)
    {
        return new InstantiatedClass(signature, [..signature.TypeParameters.Select(x => x.Instantiate())]);
    }

    private InstantiatedClass InstantiateClass(ClassSignature signature, IReadOnlyList<(ITypeReference, SourceRange)> typeArguments, SourceRange sourceRange)
    {
        GenericTypeReference[] typeArgumentReferences =
        [
            ..signature.TypeParameters.Select(x => x.Instantiate())
        ];

        if (typeArguments.Count <= 0)
        {
            return new InstantiatedClass(signature, typeArgumentReferences);
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        for (var i = 0; i < Math.Min(typeArguments.Count, typeArgumentReferences.Length); i++)
        {
            var (typeReference, referenceSourceRange) = typeArguments[i];
            ExpectType(typeReference, typeArgumentReferences[i], referenceSourceRange);
        }

        return new InstantiatedClass(signature, typeArgumentReferences);
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

    private InstantiatedClass InstantiateTuple(IReadOnlyList<(ITypeReference, SourceRange)> types, SourceRange sourceRange)
    {
        return types.Count switch
        {
            0 => throw new InvalidOperationException("Tuple must not be empty"),
            > 10 => throw new InvalidOperationException("Tuple can contain at most 10 items"),
            _ => InstantiateClass(ClassSignature.Tuple((ushort)types.Count), types, sourceRange)
        };
    }

    public class InstantiatedClass : ITypeReference
    {
        public InstantiatedClass CloneWithTypeArguments(IReadOnlyList<GenericTypeReference> typeArguments)
        {
            return new InstantiatedClass(
                Signature,
                typeArguments,
                Fields);
        }

        private InstantiatedClass(
            ClassSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            IReadOnlyList<TypeField> fields)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Fields = fields;
        }
        
        public InstantiatedClass(ClassSignature signature, IReadOnlyList<GenericTypeReference> typeArguments)
        {
            ITypeReference HandleType(ITypeReference type)
            {
                return type switch
                {
                    GenericTypeReference genericTypeReference => typeArguments.First(y =>
                        y.GenericName == genericTypeReference.GenericName),
                    GenericPlaceholder placeholder =>
                        typeArguments.First(y => y.GenericName == placeholder.GenericName),
                    InstantiatedUnion union => union.CloneWithTypeArguments([
                        ..union.TypeArguments.Select(HandleType).Cast<GenericTypeReference>()
                    ]),
                    InstantiatedClass klass => klass.CloneWithTypeArguments([..klass.TypeArguments.Select(HandleType).Cast<GenericTypeReference>()]),
                    _ => type
                };
            }
            
            Signature = signature;
            TypeArguments = typeArguments;

            Fields =
            [
                ..signature.Fields.Select(x => x with { Type = HandleType(x.Type)})
            ];
        }

        public static InstantiatedClass String { get; } = new(ClassSignature.String, []);
        public static InstantiatedClass Boolean { get; } = new(ClassSignature.Boolean, []);

        public static InstantiatedClass Int { get; } = new(ClassSignature.Int, []);

        public static InstantiatedClass Unit { get; } = new(ClassSignature.Unit, []);

        public static InstantiatedClass Never { get; } = new(ClassSignature.Never, []);

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
