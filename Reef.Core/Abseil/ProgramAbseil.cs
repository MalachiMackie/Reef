using System.Diagnostics;
using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    private readonly Dictionary<
        LoweredMethod,
        (
            FunctionSignature fnSignature,
            List<ILoweredExpression> loweredExpressions,
            IReadOnlyList<Expressions.IExpression> highLevelExpressions,
            LoweredConcreteTypeReference? ownerType,
            bool needsLowering
        )> _methods = [];
    private readonly Dictionary<Guid, DataType> _types = [];
    private readonly LangProgram _program;
    private LoweredConcreteTypeReference? _currentType;
    private (LoweredMethod LoweredMethod, FunctionSignature FunctionSignature)? _currentFunction;

    private readonly IReadOnlyList<LoweredProgram> _importedPrograms;

    public static LoweredProgram Lower(LangProgram program)
    {
        return new ProgramAbseil(program).LowerInner();
    }

    private ProgramAbseil(LangProgram program)
    {
        var importedDataTypes = new List<DataType>();
        var importedMethods = new List<LoweredMethod>();

        var ptrType = new LoweredConcreteTypeReference(
                ClassSignature.Ptr.Name,
                ClassSignature.Ptr.Id,
                []);

        for (var i = 0; i < 7; i++)
        {
            var fnClass = ClassSignature.Function(i);
            importedDataTypes.Add(
                new DataType(
                    fnClass.Id,
                    fnClass.Name,
                    [..fnClass.TypeParameters.Select(GetGenericPlaceholder)],
                    [
                        new DataTypeVariant(
                            "_classVariant",
                            [
                                new DataTypeField(
                                    "FunctionReference",
                                    new LoweredFunctionType(
                                        [..fnClass.TypeParameters.SkipLast(1).Select(GetGenericPlaceholder)],
                                        GetTypeReference(fnClass.TypeParameters[^1]))),
                                new DataTypeField(
                                    "FunctionParameter",
                                    ptrType)
                            ])
                    ],
                    []));

            /*
             * pub class Function`1<TReturn>
             * {
             *     pub field FunctionReference: FnType<TReturn>,
             *     pub field FunctionParameter: Option<Ptr>,
             *
             *     pub fn Call(): TReturn
             *     {
             *         if (FunctionParameter matches Option::Some(ptr)) {
             *           return ((FnType<Ptr, TReturn>)FunctionReference)(ptr);
             *         }
             *         return FunctionReference();
             *     }
             * }
             *
             */

            var call = fnClass.Functions[0];

            importedMethods.Add(
                new LoweredMethod(
                    call.Id,
                    $"{fnClass.Name}__{call.Name}",
                    [..fnClass.TypeParameters.Select(GetGenericPlaceholder)],
                    [..call.Parameters.Values.Select(x => GetTypeReference(x.Type))],
                    GetTypeReference(call.ReturnType),
                    [],// todo
                    []));
        }

        var errorGeneric = new LoweredGenericPlaceholder(
                                    UnionSignature.Result.Id,
                                    "TError");
        var valueGeneric = new LoweredGenericPlaceholder(
                                    UnionSignature.Result.Id,
                                    "TValue");
        var intRef = new LoweredConcreteTypeReference(
                                    ClassSignature.Int.Name,
                                    ClassSignature.Int.Id,
                                    []);
        
        var resultDataType = new DataType(
            UnionSignature.Result.Id,
            UnionSignature.Result.Name,
            [..UnionSignature.Result.TypeParameters.Select(GetGenericPlaceholder)],
            [
                new DataTypeVariant(
                    "Ok",
                    [
                        new DataTypeField(
                            "_variantIdentifier",
                            intRef),
                        new DataTypeField(
                            "Item0",
                            valueGeneric)
                    ]),
                new DataTypeVariant(
                    "Error",
                    [
                        new DataTypeField(
                            "_variantIdentifier",
                            intRef),
                        new DataTypeField(
                            "Item0",
                            errorGeneric)
                    ])
            ],
            []);
        importedDataTypes.Add(resultDataType);
        foreach (var variant in UnionSignature.Result.Variants.OfType<TypeChecking.TypeChecker.TupleUnionVariant>())
        {
            importedMethods.Add(
                new LoweredMethod(
                    variant.CreateFunction.Id,
                    variant.CreateFunction.Name,
                    resultDataType.TypeParameters,
                    [..variant.CreateFunction.Parameters.Values.Select(x => GetTypeReference(x.Type))],
                    GetTypeReference(variant.CreateFunction.ReturnType),
                    [],
                    []));
        }

        _importedPrograms = [
            new LoweredProgram()
            {
                DataTypes = importedDataTypes,
                Methods = importedMethods
            }
        ];
        _program = program;
    }

    private LoweredProgram LowerInner()
    {
        foreach (var dataType in _program.Unions.Select(x => LowerUnion(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        foreach (var dataType in _program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        foreach (var fnSignature in _program.Functions
                .Select(x => x.Signature.NotNull())
                .Where(x => x.AccessedOuterVariables.Count == 0))
        {
            var (method, loweredExpressions, expressions) = GenerateLoweredMethod(null, fnSignature, null, null);
            _methods.Add(method, (fnSignature, loweredExpressions, expressions, null, true));
        }

        var mainSignature = new FunctionSignature(
                Token.Identifier("_Main", SourceSpan.Default),
                [],
                [],
                IsStatic: true,
                IsMutable: false,
                _program.Expressions,
                FunctionIndex: null)
        {
            ReturnType = InstantiatedClass.Unit,
            OwnerType = null,
            LocalVariables = _program.TopLevelLocalVariables,
            LocalFunctions = [.. _program.Functions
                .Select(x => x.Signature.NotNull())
                .Where(x => x.AccessedOuterVariables.Count > 0)
                .Concat(_program.TopLevelLocalFunctions)]
        };
        foreach (var local in _program.TopLevelLocalVariables)
        {
            local.ContainingFunction = mainSignature;
        }

        if (mainSignature.Expressions.Count > 0)
        {
            var (method, loweredExpressions, expressions) = GenerateLoweredMethod(
                    null, mainSignature, null, null);
            _methods.Add(method, (mainSignature, loweredExpressions, expressions, null, true));
        }

        foreach (var (method, (fnSignature, loweredExpressions, expressions, ownerTypeReference, _)) in _methods.Where(x => x.Value.needsLowering))
        {
            _currentType = ownerTypeReference;
            _currentFunction = (method, fnSignature);

            loweredExpressions.AddRange(expressions.Select(LowerExpression));

            if (expressions.Count == 0 || !expressions[^1].Diverges)
            {
                loweredExpressions.Add(new MethodReturnExpression(
                            new UnitConstantExpression(true)));
            }
        }

        return new LoweredProgram()
        {
            DataTypes = [.._types.Values],
            Methods = [.._methods.Keys]
        };
    }

    private DataType LowerClass(ClassSignature klass)
    {
        var typeParameters = klass.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var classTypeReference = new LoweredConcreteTypeReference(
                klass.Name,
                klass.Id,
                typeParameters);

        var staticFields = klass.Fields.Where(x => x.IsStatic)
            .Select(x => new StaticDataTypeField(
                        x.Name,
                        GetTypeReference(x.Type),
                        LowerExpression(x.StaticInitializer.NotNull())));

        var fields = klass.Fields.Where(x => !x.IsStatic)
            .Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)));

        foreach (var method in klass.Functions)
        {
            var (loweredMethod, loweredExpressions, expressions) = GenerateLoweredMethod(klass.Name, method, classTypeReference, classTypeReference);
            _methods.Add(loweredMethod, (method, loweredExpressions, expressions, classTypeReference, true));
        }

        return new DataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [new DataTypeVariant("_classVariant", [.. fields])],
                [.. staticFields]);
    }

    private DataType LowerUnion(UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        foreach (var function in union.Functions)
        {
            var (loweredMethod, loweredExpressions, expressions) = GenerateLoweredMethod(union.Name, function, unionTypeReference, unionTypeReference);

            _methods.Add(loweredMethod, (function, loweredExpressions, expressions, unionTypeReference, true));
        }

        var variants = new List<DataTypeVariant>(union.Variants.Count);
        foreach (var variant in union.Variants)
        {
            var variantIdentifierField = new DataTypeField(
                    "_variantIdentifier",
                    GetTypeReference(InstantiatedClass.Int));
            var fields = new List<DataTypeField>() { variantIdentifierField };
            switch (variant)
            {
                case TypeChecking.TypeChecker.UnitUnionVariant u:
                    break;
                case TypeChecking.TypeChecker.ClassUnionVariant u:
                    {
                        fields.AddRange(u.Fields.Select(x => new DataTypeField(
                                        x.Name,
                                        GetTypeReference(x.Type))));
                        break;
                    }
                case TypeChecking.TypeChecker.TupleUnionVariant u:
                    {
                        var memberTypes = u.TupleMembers.NotNull().Select(GetTypeReference).ToArray();
                        fields.AddRange(memberTypes.Select((x, i) => new DataTypeField(
                                        $"Item{i}",
                                        x)));

                        var createMethodFieldInitializations = fields.Skip(1).Index().ToDictionary(x => x.Item.Name, x => (ILoweredExpression)new LoadArgumentExpression((uint)x.Index, true, x.Item.Type));
                        createMethodFieldInitializations["_variantIdentifier"] = new IntConstantExpression(true, variants.Count);

                        List<ILoweredExpression> expressions = [
                                        new MethodReturnExpression(
                                            new CreateObjectExpression(
                                                unionTypeReference,
                                                variant.Name,
                                                true,
                                                createMethodFieldInitializations
                                                ))
                                    ];

                        var method = new LoweredMethod(
                                    u.CreateFunction.Id,
                                    u.CreateFunction.Name,
                                    typeParameters,
                                    memberTypes,
                                    unionTypeReference,
                                    expressions,
                                    []);

                        // add the tuple variant as a method
                        _methods.Add(
                                method,
                                // pass null as the signature because it's never used as the current function
                                (null!, expressions, [], unionTypeReference, false));
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Invalid union variant {variant}");
            }
            variants.Add(new DataTypeVariant(
                        variant.Name,
                        fields));
        }

        return new DataType(
                union.NotNull().Id,
                union.Name,
                typeParameters,
                Variants: variants,
                StaticFields: []);
    }

    // instead of lowering the methods expressions right here, we return the list of expressions 
    // to add to later so that all the type and function references are available to be used
    private (LoweredMethod, List<ILoweredExpression>, IReadOnlyList<Expressions.IExpression>) GenerateLoweredMethod(
            string? ownerName,
            FunctionSignature fnSignature,
            LoweredConcreteTypeReference? ownerTypeReference,
            LoweredConcreteTypeReference? parentTypeReference)
    {
        var name = ownerName is null
            ? fnSignature.Name
            : $"{ownerName}__{fnSignature.Name}";

        var localsAccessedInClosure = fnSignature.LocalVariables.Where(x => x.ReferencedInClosure).ToArray();
        var parametersAccessedInClosure = fnSignature.Parameters.Values.Where(x => x.ReferencedInClosure).ToArray();
        DataType? localsType = null;
        if (localsAccessedInClosure.Length > 0 || parametersAccessedInClosure.Length > 0)
        {
            localsType = new DataType(
                Guid.NewGuid(),
                $"{name}__Locals",
                [],
                [
                    new DataTypeVariant(
                        "_classVariant",
                        [
                            ..localsAccessedInClosure.Cast<IVariable>()
                                .Concat(parametersAccessedInClosure.Cast<IVariable>())
                                .Select(
                                    x => new DataTypeField(
                                        x.Name.StringValue,
                                        GetTypeReference(x.Type))),
                        ])
                ],
                []);
            _types.Add(localsType.Id, localsType);

            fnSignature.LocalsTypeId = localsType.Id;
        }

        DataType? closureType = null;
        if (fnSignature.AccessedOuterVariables.Count > 0)
        {
            var fields = new Dictionary<Guid, DataTypeField>();

            foreach (var variable in fnSignature.AccessedOuterVariables)
            {
                switch (variable)
                {
                    case LocalVariable localVariable:
                        {
                            var containingFunction = localVariable.ContainingFunction.NotNull();
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull(expectedReason: "the containing function containing the referenced local should have already been lowered");
                            var localType = _types[localTypeId];
                            var localTypeReference = new LoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            // try add because we might have already added this function's 
                            // local type if we're referencing multiple variables from
                            // the owner function
                            fields.TryAdd(
                                localTypeId,
                                new DataTypeField(localType.Name, localTypeReference)); 
                            break;
                        }
                    case FunctionSignatureParameter parameterVariable:
                        {
                            var containingFunction = parameterVariable.ContainingFunction.NotNull(); 
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull();
                            var localType = _types[localTypeId];
                            var localTypeReference = new LoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            // try add because we might have already added this function's 
                            // local type if we're referencing multiple variables from
                            // the owner function
                            fields.TryAdd(
                                    localTypeId,
                                    new DataTypeField(localType.Name, localTypeReference));
                            break;
                        }
                    case FieldVariable fieldVariable:
                        {
                            var signature = fieldVariable.ContainingSignature;
                            var typeReference = new LoweredConcreteTypeReference(
                                    signature.Name,
                                    signature.Id,
                                    [..signature.TypeParameters
                                        .Select(GetTypeReference)]);

                            fields.TryAdd(
                                signature.Id,
                                new DataTypeField("this", typeReference));

                            break;
                        }
                    case ThisVariable thisVariable:
                        {
                            Debug.Assert(parentTypeReference is not null);
                            fields.TryAdd(
                                parentTypeReference.DefinitionId,
                                new DataTypeField("this", parentTypeReference));
                            break;
                        }
                }
            }

            closureType = new DataType(
                Guid.NewGuid(),
                $"{name}__Closure",
                [],
                [
                    new DataTypeVariant(
                        "_classVariant",
                        [..fields.Values])
                ],
                []);

            fnSignature.ClosureTypeId = closureType.Id;

            _types.Add(closureType.Id, closureType);
        }

        foreach (var localSignature in fnSignature.LocalFunctions)
        {
            var (localMethod, localLoweredExpressions, localExpressions) = 
                GenerateLoweredMethod(ownerName: name, localSignature, null, parentTypeReference);

            _methods.Add(localMethod, (localSignature, localLoweredExpressions, localExpressions, parentTypeReference, true));
        }

        var locals = new List<MethodLocal>(
                fnSignature.LocalVariables.Count
                - localsAccessedInClosure.Length
                + (localsAccessedInClosure.Length > 0 ? 1 : 0));
        var expressions = new List<ILoweredExpression>(fnSignature.Expressions.Count);

        if (localsType is not null)
        {
            var localsTypeReference = new LoweredConcreteTypeReference(
                            localsType.Name,
                            localsType.Id,
                            []);

            locals.Add(new MethodLocal(
                        "__locals",
                        localsTypeReference));
            var hasCompilerInsertedFirstParameter = 
                (!fnSignature.IsStatic && ownerTypeReference is not null)
                || closureType is not null;


            var fieldInitializers = new Dictionary<string, ILoweredExpression>();
            foreach (var parameter in parametersAccessedInClosure)
            {
                
                fieldInitializers.Add(
                        parameter.Name.StringValue,
                        new LoadArgumentExpression(
                            (uint)(parameter.ParameterIndex
                            + (hasCompilerInsertedFirstParameter ? 1 : 0)),
                            true,
                            GetTypeReference(parameter.Type)));
            }

            expressions.Add(
                new VariableDeclarationAndAssignmentExpression(
                    "__locals",
                    new CreateObjectExpression(
                        localsTypeReference,
                        "_classVariant",
                        true,
                        fieldInitializers),
                    false));

        }

        locals.AddRange(fnSignature.LocalVariables.Where(x => !x.ReferencedInClosure)
                .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type))));

        var parameters = fnSignature.Parameters.Values.Select(y => GetTypeReference(y.Type));

        if (!fnSignature.IsStatic && ownerTypeReference is not null)
        {
            parameters = parameters.Prepend(ownerTypeReference);
        }
        else if (closureType is not null)
        {
            parameters = parameters.Prepend(new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []));
        }

        return (new LoweredMethod(
            fnSignature.Id,
            name,
            [
                .. fnSignature.TypeParameters.Select(GetGenericPlaceholder),
                .. ownerTypeReference?.TypeArguments.Select(x => (x as LoweredGenericPlaceholder).NotNull()) ?? []
            ],
            [.. parameters],
            GetTypeReference(fnSignature.ReturnType),
            expressions,
            locals), expressions, fnSignature.Expressions);
    }

    private LoweredGenericPlaceholder GetGenericPlaceholder(GenericPlaceholder placeholder)
    {
        return new LoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
    }

    private DataType GetDataType(
            Guid typeId,
            IReadOnlyList<ILoweredTypeReference> typeArguments)
    {
        if (_types.TryGetValue(typeId, out var dataType))
            return dataType;

        return _importedPrograms.Select(x => x.DataTypes.FirstOrDefault(y => y.Id == typeId))
            .FirstOrDefault(x => x is not null)
            ?? throw new InvalidOperationException($"No data type with id {typeId} was found");
    }

    private LoweredFunctionReference GetFunctionReference(
            Guid functionId,
            IReadOnlyList<ILoweredTypeReference> typeArguments,
            IReadOnlyList<ILoweredTypeReference> ownerTypeArguments)
    {
        var loweredMethod = _methods.Keys.FirstOrDefault(x => x.Id == functionId)
            ?? _importedPrograms.SelectMany(x => x.Methods)
                .First(x => x.Id == functionId);

        IReadOnlyList<ILoweredTypeReference> resultingTypeArguments = [..typeArguments, ..ownerTypeArguments];
        
        Debug.Assert(resultingTypeArguments.Count == loweredMethod.TypeParameters.Count);

        return new(
                loweredMethod.Name,
                functionId,
                resultingTypeArguments);
    }

    private ILoweredTypeReference GetTypeReference(ITypeReference typeReference)
    {
        return typeReference switch
        {
            InstantiatedClass c => new LoweredConcreteTypeReference(
                c.Signature.Name,
                c.Signature.Id,
                [.. c.TypeArguments.Select(GetTypeReference)]),
            InstantiatedUnion u => new LoweredConcreteTypeReference(
                u.Signature.Name,
                u.Signature.Id,
                [.. u.TypeArguments.Select(GetTypeReference)]),
            GenericTypeReference {ResolvedType: {} resolvedType} => GetTypeReference(resolvedType),
            GenericTypeReference {ResolvedType: null} g => new LoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            GenericPlaceholder g => new LoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            FunctionObject f => FunctionObjectCase(f),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };

        LoweredConcreteTypeReference FunctionObjectCase(FunctionObject f)
        {
            var type = _importedPrograms
                .SelectMany(x => x.DataTypes.Where(y => y.Name == $"Function`{f.Parameters.Count + 1}"))
                .First();
            
            return new LoweredConcreteTypeReference(
                type.Name,
                type.Id,
                [..f.Parameters.Select(x => GetTypeReference(x.Type))
                    .Append(GetTypeReference(f.ReturnType))]);
        }
    }

    private static bool EqualTypeReferences(ILoweredTypeReference a, ILoweredTypeReference b)
    {
        return (a, b) switch
        {
            (LoweredConcreteTypeReference concreteA, LoweredConcreteTypeReference concreteB)
                when concreteA.DefinitionId == concreteB.DefinitionId
                && concreteA.TypeArguments.Zip(concreteB.TypeArguments)
                    .All(x => EqualTypeReferences(x.First, x.Second)) => true,
            (LoweredGenericPlaceholder genericA, LoweredGenericPlaceholder genericB)
                when genericA.OwnerDefinitionId == genericB.OwnerDefinitionId => true,
            _ => false
        };
    }
}
