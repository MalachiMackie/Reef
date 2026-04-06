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

        public static Lazy<ClassSignature> Unit { get; } = new(() => new ClassSignature()
        { Id = DefId.Unit, TypeParameters = [], Name = "Unit", Fields = [], Functions = [], Boxed = false, IsPublic = true });

        public static Lazy<ClassSignature> BoxedValue { get; } = new(CreateBoxedValue);
        public static Lazy<ClassSignature> ObjectHeader { get; } = new(() => new ClassSignature()
        {
            Id = DefId.ObjectHeader,
            Boxed = false,
            Fields = [
                new TypeField
                {
                    Name = "TypeId",
                    IsMutable = false,
                    IsPublic = true,
                    IsStatic = false,
                    StaticInitializer = null,
                    Type = InstantiatedClass.TypeId
                },
                // todo: VTable
            ],
            Functions = [],
            IsPublic = true,
            Name = "ObjectHeader",
            TypeParameters = []
        });

        public static Lazy<ClassSignature> MethodInfo { get; } = new(() => new ClassSignature()
        {
            Id = DefId.MethodInfo,
            Boxed = false,
            Fields = [
                new TypeField {
                    Name = "Id",
                    IsMutable = false,
                    IsPublic = true,
                    IsStatic = false,
                    StaticInitializer = null,
                    Type = InstantiatedClass.MethodId
                },
                new TypeField {
                    Name = "Name",
                    IsMutable = false,
                    IsPublic = true,
                    IsStatic = false,
                    StaticInitializer = null,
                    Type = InstantiatedClass.String
                },
            ],
            Functions = [],
            IsPublic = true,
            Name = "MethodInfo",
            TypeParameters = []
        });

        private static ClassSignature CreateBoxedValue()
        {
            var typeParameters = new List<GenericPlaceholder>();
            var fields = new List<TypeField>();
            var signature = new ClassSignature
            {
                Id = DefId.BoxedValue,
                Boxed = false,
                TypeParameters = typeParameters,
                Fields = fields,
                Functions = [],
                IsPublic = true,
                Name = "BoxedValue"
            };

            typeParameters.Add(new GenericPlaceholder
            {
                GenericName = "TValue",
                Constraints = [],
                OwnerType = signature
            });

            fields.Add(new TypeField
            {
                IsMutable = false,
                IsPublic = true,
                IsStatic = false,
                Name = "ObjectHeader",
                StaticInitializer = null,
                Type = InstantiatedClass.ObjectHeader
            });
            fields.Add(new TypeField
            {
                IsMutable = false,
                IsPublic = true,
                IsStatic = false,
                Name = "Value",
                StaticInitializer = null,
                Type = typeParameters[0]
            });

            return signature;
        }

        public static Lazy<ClassSignature> FieldInfo { get; } = new(() => new ClassSignature()
        {
            Id = DefId.FieldInfo,
            TypeParameters = [],
            Name = "FieldInfo",
            Fields = [
                new TypeField {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "Name",
                    StaticInitializer = null,
                    Type = InstantiatedClass.String
                },
                new TypeField {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "TypeId",
                    StaticInitializer = null,
                    Type = InstantiatedClass.TypeId
                },
            ],
            Functions = [],
            Boxed = true,
            IsPublic = true
        });

        public static Lazy<ClassSignature> VariantInfo { get; } = new(() => new ClassSignature()
        {
            Id = DefId.VariantInfo,
            TypeParameters = [],
            Name = "VariantInfo",
            Fields = [
                new TypeField {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "Name",
                    StaticInitializer = null,
                    Type = InstantiatedClass.String
                },
                new TypeField {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "Fields",
                    StaticInitializer = null,
                    Type = new ArrayType(
                        InstantiatedClass.Create(
                            FieldInfo.Value,
                            [],
                            boxed: false
                        ), boxed: false, length: 10)
                },
            ],
            Functions = [],
            Boxed = true,
            IsPublic = true
        });

        public static Lazy<ClassSignature> MethodId { get; } = new(() => new ClassSignature()
        {
            Id = DefId.MethodId,
            TypeParameters = [],
            Name = "methodId",
            Fields = [
                new TypeField
                {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "Value",
                    StaticInitializer = null,
                    Type = InstantiatedClass.Int32
                },
            ],
            Functions = [],
            Boxed = false,
            IsPublic = true
        });

        public static Lazy<ClassSignature> TypeId { get; } = new(() => new ClassSignature()
        {
            Id = DefId.TypeId,
            TypeParameters = [],
            Name = "typeId",
            Fields = [
                new TypeField
                {
                    IsPublic = true,
                    IsMutable = false,
                    IsStatic = false,
                    Name = "Value",
                    StaticInitializer = null,
                    Type = InstantiatedClass.Int32
                },
            ],
            Functions = [],
            Boxed = false,
            IsPublic = true
        });

        public static Lazy<IReadOnlyList<ClassSignature>> ReflectionClasses { get; } = new(() =>
            [
                VariantInfo.Value,
                FieldInfo.Value,
                MethodInfo.Value,
                TypeId.Value,
                MethodId.Value,
            ]);


        public static Lazy<ClassSignature> String { get; } = new(() => new ClassSignature()
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
            Boxed = false,
            IsPublic = true
        });



        public static Lazy<ClassSignature> Int64 { get; } = new(() => new ClassSignature()
        { Id = DefId.Int64, TypeParameters = [], Name = "i64", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> Int32 { get; } = new(() => new ClassSignature()
        { Id = DefId.Int32, TypeParameters = [], Name = "i32", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> Int16 { get; } = new(() => new ClassSignature()
        { Id = DefId.Int16, TypeParameters = [], Name = "i16", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> Int8 { get; } = new(() => new ClassSignature()
        { Id = DefId.Int8, TypeParameters = [], Name = "i8", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> UInt64 { get; } = new(() => new ClassSignature()
        { Id = DefId.UInt64, TypeParameters = [], Name = "u64", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> UInt32 { get; } = new(() => new ClassSignature()
        { Id = DefId.UInt32, TypeParameters = [], Name = "u32", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> UInt16 { get; } = new(() => new ClassSignature()
        { Id = DefId.UInt16, TypeParameters = [], Name = "u16", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> UInt8 { get; } = new(() => new ClassSignature()
        { Id = DefId.UInt8, TypeParameters = [], Name = "u8", Fields = [], Functions = [], Boxed = false, IsPublic = true });

        public static Lazy<ClassSignature> RawPointer { get; } = new(() => new ClassSignature()
        { Id = DefId.RawPointer, TypeParameters = [], Name = "rawPointer", Fields = [], Functions = [], Boxed = false, IsPublic = true });
        public static Lazy<ClassSignature> MethodPointer { get; } = new(() => new ClassSignature()
        { Id = DefId.MethodPointer, TypeParameters = [], Name = "methodPointer", Fields = [], Functions = [], Boxed = false, IsPublic = true });

        public static Lazy<ClassSignature> Boolean { get; } = new(() => new ClassSignature()
        { Id = DefId.Boolean, TypeParameters = [], Name = "bool", Fields = [], Functions = [], Boxed = false, IsPublic = true });

        public static Lazy<ClassSignature> Never { get; } = new(() => new ClassSignature()
        { Id = DefId.Never, TypeParameters = [], Name = "!", Fields = [], Functions = [], Boxed = false, IsPublic = true });

        public static Lazy<IEnumerable<ClassSignature>> BuiltInTypes { get; } = new(() => [
            Unit.Value,
            String.Value,
            Int64.Value,
            Int32.Value,
            Int16.Value,
            Int8.Value,
            UInt64.Value,
            UInt32.Value,
            UInt16.Value,
            UInt8.Value,
            Never.Value,
            Boolean.Value
        ]);

        public required bool Boxed { get; init; }
        public required IReadOnlyList<GenericPlaceholder> TypeParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required string Name { get; init; }
        public required DefId Id { get; init; }
        public required bool IsPublic { get; init; }

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
                IsPublic = true,
                Id = DefId.FunctionObject(parameterCount),
                Name = functionName,
                TypeParameters = typeParameters,
                Fields = [
                    new TypeField
                    {
                        Name = "FunctionReference",
                        IsMutable = false,
                        IsPublic = false,
                        IsStatic = false,
                        StaticInitializer = null,
                        Type = InstantiatedClass.MethodPointer
                    },
                    new TypeField
                    {
                        Name = "FunctionParameter",
                        IsMutable = false,
                        IsPublic = false,
                        IsStatic = false,
                        StaticInitializer = null,
                        Type = InstantiatedClass.RawPointer
                    },
                ],
                Functions = functions,
                Boxed = true
            };

            var callFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();

            var functionSignature = new FunctionSignature(
                DefId.FunctionObject_Call(parameterCount),
                Token.Identifier("Call", SourceSpan.Default),
                [],
                callFunctionParameters,
                IsStatic: false,
                IsMutable: false,
                Expressions: [],
                true,
                IsMutableReturn: false, // todo: I don't know what to do with this, how can a function object specify mutable return?
                IsPublic: true)
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
                Boxed = false,
                IsPublic = true
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
                        GenericTypeReference { ResolvedType: null } genericTypeReference => typeArgumentReferences.First(y =>
                            y.GenericName == genericTypeReference.GenericName),
                        GenericTypeReference { ResolvedType: { } resolvedType } => resolvedType,
                        GenericPlaceholder placeholder =>
                            typeArgumentReferences.First(y => y.GenericName == placeholder.GenericName),
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

        public static InstantiatedClass String => new(ClassSignature.String.Value, [], [], boxed: ClassSignature.String.Value.Boxed);
        public static InstantiatedClass TypeId => new(ClassSignature.TypeId.Value, [], [], boxed: ClassSignature.TypeId.Value.Boxed);
        public static InstantiatedClass MethodId => new(ClassSignature.MethodId.Value, [], [], boxed: ClassSignature.MethodId.Value.Boxed);
        public static InstantiatedClass Boolean => new(ClassSignature.Boolean.Value, [], [], boxed: ClassSignature.Boolean.Value.Boxed);

        public static InstantiatedClass Int64 => new(ClassSignature.Int64.Value, [], [], boxed: ClassSignature.Int64.Value.Boxed);
        public static InstantiatedClass Int32 => new(ClassSignature.Int32.Value, [], [], boxed: ClassSignature.Int32.Value.Boxed);
        public static InstantiatedClass Int16 => new(ClassSignature.Int16.Value, [], [], boxed: ClassSignature.Int16.Value.Boxed);
        public static InstantiatedClass Int8 => new(ClassSignature.Int8.Value, [], [], boxed: ClassSignature.Int8.Value.Boxed);
        public static InstantiatedClass UInt64 => new(ClassSignature.UInt64.Value, [], [], boxed: ClassSignature.UInt64.Value.Boxed);
        public static InstantiatedClass UInt32 => new(ClassSignature.UInt32.Value, [], [], boxed: ClassSignature.UInt32.Value.Boxed);
        public static InstantiatedClass UInt16 => new(ClassSignature.UInt16.Value, [], [], boxed: ClassSignature.UInt16.Value.Boxed);
        public static InstantiatedClass UInt8 => new(ClassSignature.UInt8.Value, [], [], boxed: ClassSignature.UInt8.Value.Boxed);
        public static InstantiatedClass RawPointer => new(ClassSignature.RawPointer.Value, [], [], boxed: ClassSignature.RawPointer.Value.Boxed);
        public static InstantiatedClass MethodPointer => new(ClassSignature.MethodPointer.Value, [], [], boxed: ClassSignature.MethodPointer.Value.Boxed);
        public static InstantiatedClass VariantInfo => Create(ClassSignature.VariantInfo.Value, [], ClassSignature.VariantInfo.Value.Boxed);
        public static InstantiatedClass FieldInfo => Create(ClassSignature.FieldInfo.Value, [], ClassSignature.FieldInfo.Value.Boxed);
        public static InstantiatedClass ObjectHeader => Create(ClassSignature.ObjectHeader.Value, [], ClassSignature.ObjectHeader.Value.Boxed);
        public static InstantiatedClass BoxedValue(ITypeReference valueType) => Create(ClassSignature.BoxedValue.Value, [valueType], ClassSignature.BoxedValue.Value.Boxed);

        // todo: need some sort of inferred int type

        public static IReadOnlyList<InstantiatedClass> IntTypes => [
            Int64,
            Int32,
            Int16,
            Int8,
            UInt64,
            UInt32,
            UInt16,
            UInt8,
        ];

        public static InstantiatedClass Unit => new(ClassSignature.Unit.Value, [], [], boxed: ClassSignature.Unit.Value.Boxed);

        public static InstantiatedClass Never => new(ClassSignature.Never.Value, [], [], boxed: ClassSignature.Never.Value.Boxed);

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
            return Signature.Id == currentTypeSignature.Id;
        }
    }

}
