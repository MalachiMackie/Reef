using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class ClassSignature : ITypeSignature
    {
        public static readonly Dictionary<int, string> TupleFieldNames = new()
        {
            { 0, "First" },
            { 1, "Second" },
            { 2, "Third" },
            { 3, "Fourth" },
            { 4, "Fifth" },
            { 5, "Sixth" },
            { 6, "Seventh" },
            { 7, "Eighth" },
            { 8, "Ninth" },
            { 9, "Tenth" }
        };

        public static ClassSignature Unit { get; } = new()
        { TypeParameters = [], Name = "Unit", Fields = [], Functions = [] };

        public static ClassSignature String { get; } = new()
        { TypeParameters = [], Name = "string", Fields = [], Functions = [] };

        public static ClassSignature Int { get; } = new()
        { TypeParameters = [], Name = "int", Fields = [], Functions = [] };

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

        private static readonly Dictionary<int, ClassSignature> CachedFunctionClasses = [];

        public static ClassSignature Function(IReadOnlyList<FunctionParameter> parameters)
        {
            // plus 1 for return value
            var typeParamsCount = parameters.Count + 1;

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
                // but these are not represented in the type system, they only happen when compiling to IL
                Fields = [null!, null!],
                Functions = functions
            };

            var callFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();

            var functionSignature = new FunctionSignature(
                Token.Identifier("Call", SourceSpan.Default),
                [],
                callFunctionParameters,
                isStatic: false,
                isMutable: false,
                expressions: [],
                functionIndex: 0)
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

            foreach (var i in Enumerable.Range(0, parameters.Count))
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

        private static readonly Dictionary<int, ClassSignature> CachedTupleSignatures = [];
        public static ClassSignature Tuple(IReadOnlyList<ITypeReference> elements)
        {
            if (CachedTupleSignatures.TryGetValue(elements.Count, out var cachedSignature))
            {
                return cachedSignature;
            }

            var typeParameters = new List<GenericPlaceholder>(elements.Count);
            var fields = new List<TypeField>();
            var signature = new ClassSignature
            {
                TypeParameters = typeParameters,
                Name = $"Tuple`{elements.Count}",
                Fields = fields,
                Functions = []
            };
            typeParameters.AddRange(Enumerable.Range(0, elements.Count).Select(x => new GenericPlaceholder
            {
                GenericName = $"T{x}",
                OwnerType = signature
            }));

            fields.AddRange(elements.Select((_, i) => new TypeField
            {
                // todo: verify this
                IsMutable = false,
                Name = TupleFieldNames.TryGetValue(i, out var name)
                    ? name
                    : throw new InvalidOperationException("Tuple can only contain at most 10 elements"),
                IsStatic = false,
                Type = typeParameters[i],
                IsPublic = true,
                StaticInitializer = null,
                FieldIndex = (uint)i
            }));

            CachedTupleSignatures[elements.Count] = signature;

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
        [NotNullWhen(true)] out InstantiatedFunction? function,
        [NotNullWhen(true)] out uint? functionIndex)
    {
        var signature = @class.Signature.Functions.FirstOrDefault(x => x.Name == functionName);

        if (signature is null)
        {
            function = null;
            functionIndex = null;
            return false;
        }

        function = InstantiateFunction(signature, @class, typeArguments, typeArgumentsSourceRange, inScopeTypeParameters: []);
        functionIndex = signature.FunctionIndex ?? throw new InvalidOperationException("Class function should have index");
        return true;
    }

    private InstantiatedClass InstantiateTuple(IReadOnlyList<(ITypeReference, SourceRange)> types, SourceRange sourceRange)
    {
        return types.Count switch
        {
            0 => throw new InvalidOperationException("Tuple must not be empty"),
            > 10 => throw new InvalidOperationException("Tuple can contain at most 10 items"),
            _ => InstantiateClass(ClassSignature.Tuple([.. types.Select(x => x.Item1)]), types, sourceRange)
        };
    }

    public class InstantiatedClass : ITypeReference
    {
        public InstantiatedClass(ClassSignature signature, IReadOnlyList<GenericTypeReference> typeArguments)
        {
            Signature = signature;
            TypeArguments = typeArguments;

            Fields =
            [
                ..signature.Fields.Select(x => x with { Type = x.Type switch
                {
                    GenericTypeReference genericTypeReference => typeArguments.First(y =>
                        y.GenericName == genericTypeReference.GenericName),
                    GenericPlaceholder placeholder => typeArguments.First(y => y.GenericName == placeholder.GenericName),
                    var type => type
                } })
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
