using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class UnionSignature : ITypeSignature
    {
        // public static readonly IReadOnlyList<UnionSignature> BuiltInTypes;

        // static UnionSignature()
        // {
        //     var variants = new TupleUnionVariant[2];
        //     var typeParameters = new GenericPlaceholder[2];
        //     var resultSignature = new UnionSignature
        //     {
        //         Id = DefId.Result,
        //         TypeParameters = typeParameters,
        //         Name = "result",
        //         Variants = variants,
        //         Functions = [],
        //         Boxed = false,
        //         IsPublic = true
        //     };

        //     typeParameters[0] = new GenericPlaceholder
        //     {
        //         GenericName = "TValue",
        //         OwnerType = resultSignature,
        //         Constraints = [],
        //     };
        //     typeParameters[1] = new GenericPlaceholder
        //     {
        //         GenericName = "TError",
        //         OwnerType = resultSignature,
        //         Constraints = [],
        //     };

        //     var boxedOkCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
        //     var boxedErrorCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
        //     var unboxedOkCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
        //     var unboxedErrorCreateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
        //     var boxedOkCreateFunction = new FunctionSignature(
        //             DefId.Result_Create_Ok,
        //             Token.Identifier("result__Create__Ok", SourceSpan.Default),
        //             [],
        //             boxedOkCreateParameters,
        //             IsStatic: true,
        //             IsMutable: false,
        //             Expressions: [],
        //             ExternName: DefId.Result.FullName + "__Create_Ok",
        //             IsMutableReturn: true,
        //             IsPublic: true)
        //     {
        //         ReturnType = InstantiatedUnion.Create(resultSignature, typeParameters, boxed: true),
        //         OwnerType = resultSignature
        //     };
        //     var boxedErrorCreateFunction = new FunctionSignature(
        //         DefId.Result_Create_Error,
        //         Token.Identifier("result__Create__Error", SourceSpan.Default),
        //         [],
        //         boxedErrorCreateParameters,
        //         IsStatic: true,
        //         IsMutable: false,
        //         Expressions: [],
        //         ExternName: DefId.Result.FullName + "__Create_Error",
        //         IsMutableReturn: true,
        //         IsPublic: true)
        //     {
        //         ReturnType = InstantiatedUnion.Create(resultSignature, typeParameters, boxed: true),
        //         OwnerType = resultSignature
        //     };

        //     var unboxedOkCreateFunction = new FunctionSignature(
        //                         DefId.Result_Unboxed_Create_Ok,
        //                         Token.Identifier("result__unboxed__Create__Ok", SourceSpan.Default),
        //                         [],
        //                         unboxedOkCreateParameters,
        //                         IsStatic: true,
        //                         IsMutable: false,
        //                         Expressions: [],
        //                         ExternName: DefId.Result.FullName + "__unboxed__Create_Ok",
        //                         IsMutableReturn: true,
        //                         IsPublic: true)
        //     {
        //         ReturnType = InstantiatedUnion.Create(resultSignature, typeParameters, boxed: false),
        //         OwnerType = resultSignature
        //     };
        //     var unboxedErrorCreateFunction = new FunctionSignature(
        //         DefId.Result_Unboxed_Create_Error,
        //         Token.Identifier("result__unboxed__Create__Error", SourceSpan.Default),
        //         [],
        //         unboxedErrorCreateParameters,
        //         IsStatic: true,
        //         IsMutable: false,
        //         Expressions: [],
        //         ExternName: DefId.Result.FullName + "__unboxed__Create_Error",
        //         IsMutableReturn: true,
        //         IsPublic: true)
        //     {
        //         ReturnType = InstantiatedUnion.Create(resultSignature, typeParameters, boxed: false),
        //         OwnerType = resultSignature
        //     };

        //     boxedOkCreateParameters["Item0"] = new FunctionSignatureParameter(
        //         boxedOkCreateFunction,
        //         Token.Identifier("Item0", SourceSpan.Default),
        //         typeParameters[0],
        //         Mutable: false,
        //         ParameterIndex: 0);

        //     boxedErrorCreateParameters["Item0"] = new FunctionSignatureParameter(
        //         boxedErrorCreateFunction,
        //         Token.Identifier("Item0", SourceSpan.Default),
        //         typeParameters[1],
        //         Mutable: false,
        //         ParameterIndex: 0);

        //     unboxedOkCreateParameters["Item0"] = new FunctionSignatureParameter(
        //         unboxedOkCreateFunction,
        //         Token.Identifier("Item0", SourceSpan.Default),
        //         typeParameters[0],
        //         Mutable: false,
        //         ParameterIndex: 0);

        //     unboxedErrorCreateParameters["Item0"] = new FunctionSignatureParameter(
        //         unboxedErrorCreateFunction,
        //         Token.Identifier("Item0", SourceSpan.Default),
        //         typeParameters[1],
        //         Mutable: false,
        //         ParameterIndex: 0);

        //     variants[0] = new TupleUnionVariant
        //     {
        //         Name = "Ok",
        //         TupleMembers = [typeParameters[0]],
        //         BoxedCreateFunction = boxedOkCreateFunction,
        //         UnboxedCreateFunction = unboxedOkCreateFunction
        //     };
        //     variants[1] = new TupleUnionVariant
        //     {
        //         Name = "Error",
        //         TupleMembers = [typeParameters[1]],
        //         BoxedCreateFunction = boxedErrorCreateFunction,
        //         UnboxedCreateFunction = unboxedErrorCreateFunction,
        //     };

        //     Result = resultSignature;
        //     BuiltInTypes = [Result];
        // }

        // public static Lazy<UnionSignature> VariablePlace { get; } = new(() => new UnionSignature()
        // {
        //     Id = DefId.VariablePlace,
        //     TypeParameters = [],
        //     Name = "VariablePlace",
        //     Variants = [
        //         new ClassUnionVariant {
        //             Name = "StackBaseOffset",
        //             Fields = [
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Offset",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.UInt16
        //                 },
        //             ]
        //         },
        //         new ClassUnionVariant {
        //             Name = "PointerTo",
        //             Fields = [
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "PointerLocationOffset",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.UInt16
        //                 },
        //             ]
        //         }
        //     ],
        //     Functions = [],
        //     Boxed = false,
        //     IsPublic = true
        // });

        // public static Lazy<UnionSignature> TypeInfo { get; } = new(() => new UnionSignature()
        // {
        //     Id = DefId.TypeInfo,
        //     TypeParameters = [],
        //     Name = "TypeInfo",
        //     Variants = [
        //         new ClassUnionVariant {
        //             Name = "Class",
        //             Fields = [
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "FullyQualifiedName",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.String
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Name",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.String
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Size",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.UInt64
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "TypeId",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.TypeId
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "StaticFields",
        //                     StaticInitializer = null,
        //                     Type = new ArrayType(
        //                         InstantiatedClass.Create(
        //                             ClassSignature.StaticFieldInfo.Value,
        //                             [],
        //                             boxed: false))
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Fields",
        //                     StaticInitializer = null,
        //                     Type = new ArrayType(
        //                         InstantiatedClass.Create(
        //                             ClassSignature.FieldInfo.Value,
        //                             [],
        //                             boxed: false))
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "ContainsPointer",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.Boolean
        //                 }
        //             ]
        //         },
        //         new ClassUnionVariant
        //         {
        //             Name = "Union",
        //             Fields = [
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "FullyQualifiedName",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.String
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Name",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.String
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Size",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.UInt64
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "TypeId",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.TypeId
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "StaticFields",
        //                     StaticInitializer = null,
        //                     Type = new ArrayType(
        //                         InstantiatedClass.Create(
        //                             ClassSignature.StaticFieldInfo.Value,
        //                             [],
        //                             boxed: false))
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Variants",
        //                     StaticInitializer = null,
        //                     Type = new ArrayType(
        //                         InstantiatedClass.Create(
        //                             ClassSignature.VariantInfo.Value,
        //                             [],
        //                             boxed: false))
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "VariantIdentifierGetter",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.Create(
        //                         ClassSignature.Function(1),
        //                         [InstantiatedClass.RawPointer, InstantiatedClass.UInt16],
        //                         boxed: false
        //                     )
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "ContainsPointer",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.Boolean
        //                 }
        //             ]
        //         },
        //         new ClassUnionVariant {
        //             Name = "Pointer",
        //             Fields = [
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "FullyQualifiedName",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.String
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "PointerTo",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.TypeId
        //                 }
        //             ]
        //         },
        //         new ClassUnionVariant {
        //             Name = "Array",
        //             Fields = [
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "FullyQualifiedName",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.String
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "ElementType",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.TypeId
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "Length",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.UInt64
        //                 },
        //                 new TypeField {
        //                     IsPublic = true,
        //                     IsMutable = false,
        //                     IsStatic = false,
        //                     Name = "IsDynamic",
        //                     StaticInitializer = null,
        //                     Type = InstantiatedClass.Boolean
        //                 },
        //             ]
        //         }
        //     ],
        //     Functions = [],
        //     Boxed = true,
        //     IsPublic = true
        // });

        // public static Lazy<IReadOnlyList<UnionSignature>> ReflectionUnions { get; } = new(() =>
        //     [
        //         TypeInfo.Value,
        //         VariablePlace.Value
        //     ]);

        // public static UnionSignature Result { get; }
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

    // private InstantiatedUnion InstantiateResult(SourceRange sourceRange, Token? boxingSpecifier)
    // {
    //     return InstantiateUnion(UnionSignature.Result, [], boxingSpecifier, sourceRange);
    // }

    public class InstantiatedUnion : ITypeReference, IInstantiatedGeneric
    {
        public InstantiatedUnion CloneWithTypeArguments(IReadOnlyList<ITypeReference> typeArguments)
        {
            Debug.Assert(typeArguments.Count == Signature.TypeParameters.Count);
            var instantiatedTypeArguments = new List<GenericTypeReference>(typeArguments.Count);
            var instantiatedUnion = new InstantiatedUnion(
                Signature,
                instantiatedTypeArguments,
                Variants,
                Boxed);

            instantiatedTypeArguments.AddRange(Signature.TypeParameters.Zip(typeArguments)
                .Select(x => x.First.Instantiate(
                    instantiatedUnion,
                    x.Second switch
                    {
                        GenericTypeReference { ResolvedType: var resolvedType } => resolvedType,
                        _ => x.Second
                    })));

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
                    GenericTypeReference { ResolvedType: { } resolvedType } => resolvedType,
                    GenericPlaceholder placeholder => typeArgumentReferences.First(z => z.GenericName == placeholder.GenericName),
                    InstantiatedUnion union => union.CloneWithTypeArguments([.. union.TypeArguments.Select(HandleType)]),
                    InstantiatedClass klass => klass.CloneWithTypeArguments([.. klass.TypeArguments.Select(HandleType)]),
                    ArrayType { Length: not null } arrayType => new ArrayType(HandleType(arrayType.ElementType), arrayType.Boxed, arrayType.Length.Value),
                    ArrayType { IsDynamic: true } arrayType => new ArrayType(HandleType(arrayType.ElementType)),
                    _ => throw new InvalidOperationException(type.GetType().ToString())
                };
            }
        }

        // public static InstantiatedUnion TypeInfo => Create(UnionSignature.TypeInfo.Value, [], UnionSignature.TypeInfo.Value.Boxed);
        // public static InstantiatedUnion VariablePlace => Create(UnionSignature.VariablePlace.Value, [], UnionSignature.VariablePlace.Value.Boxed);

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
