using System.Diagnostics;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    private const string ReturnValueLocalName = "_returnValue";
    private const string LocalsObjectLocalName = "_localsObject";
    private const string ClassVariantName = "_classVariant";
    private const string ClosureThisFieldName = "this";
    private const string ThisParameterName = "this";
    private const string ClosureParameterName = "closure";
    private const string VariantIdentifierFieldName = "_variantIdentifier";
    private const string TempReturnBasicBlockId = "TempReturnBasicBlockId";
    private static string ParameterLocalName(uint parameterIndex) => $"_param{parameterIndex}";
    private static string LocalName(uint localIndex) => $"_local{localIndex}";
    private static string TupleElementName(uint memberIndex) => $"Item{memberIndex}";
        
    private readonly Dictionary<
        LoweredMethod,
        (
            TypeChecker.FunctionSignature fnSignature,
            List<BasicBlock> basicBlocks,
            List<MethodLocal> locals,
            IReadOnlyList<Expressions.IExpression> highLevelExpressions,
            LoweredConcreteTypeReference? ownerType,
            bool needsLowering
        )> _methods = [];
    private readonly Dictionary<DefId, DataType> _types = [];
    private readonly LangProgram _program;
    private LoweredConcreteTypeReference? _currentType;
    private (LoweredMethod LoweredMethod, TypeChecker.FunctionSignature FunctionSignature)? _currentFunction;

    private readonly IReadOnlyList<LoweredModule> _importedModules;
    private List<BasicBlock> _basicBlocks = [];
    private List<IStatement> _basicBlockStatements = [];
    private List<MethodLocal> _locals = [];

    public static (LoweredModule Module, IReadOnlyList<LoweredModule> ImportedModules) Lower(LangProgram program)
    {
        var abseil = new ProgramAbseil(program);
        return (abseil.LowerInner(), abseil._importedModules);
    }

    private LoweredExternMethod ExternMethodFromSignature(TypeChecker.FunctionSignature signature)
    {
        return new LoweredExternMethod(
            signature.Id,
            signature.Name,
            [..signature.TypeParameters.Select(x => new LoweredGenericPlaceholder(signature.Id, x.GenericName))],
            ReturnValue: new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            ParameterLocals: [..signature.Parameters.Select((x, i) => new MethodLocal(
                ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))]);
    }

    private ProgramAbseil(LangProgram program)
    {
        var importedDataTypes = new List<DataType>();
        var importedMethods = new List<IMethod>
        {
            ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintString),
        };

        var rawPointerType = new LoweredConcreteTypeReference(
                TypeChecker.ClassSignature.RawPointer.Name,
                TypeChecker.ClassSignature.RawPointer.Id,
                []);

        for (var i = 0; i < 7; i++)
        {
            var fnClass = TypeChecker.ClassSignature.Function(i);
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
                                    new LoweredFunctionReference(
                                        fnClass.Id,
                                        [..fnClass.TypeParameters.SkipLast(1).Select(GetGenericPlaceholder)])),
                                new DataTypeField(
                                    "FunctionParameter",
                                    rawPointerType)
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
                    [],
                    ReturnValue: new MethodLocal(ReturnValueLocalName, null, GetTypeReference(call.ReturnType)),
                    ParameterLocals: [..call.Parameters.Values.Select((x, j) => new MethodLocal(ParameterLocalName((uint)j), x.Name.StringValue, GetTypeReference(x.Type)))],
                    []));
        }

        var errorGeneric = new LoweredGenericPlaceholder(
                                    TypeChecker.UnionSignature.Result.Id,
                                    "TError");
        var valueGeneric = new LoweredGenericPlaceholder(
                                    TypeChecker.UnionSignature.Result.Id,
                                    "TValue");
        var intRef = new LoweredConcreteTypeReference(
                                    TypeChecker.ClassSignature.Int64.Name,
                                    TypeChecker.ClassSignature.Int64.Id,
                                    []);
        
        var resultDataType = new DataType(
            TypeChecker.UnionSignature.Result.Id,
            TypeChecker.UnionSignature.Result.Name,
            [..TypeChecker.UnionSignature.Result.TypeParameters.Select(GetGenericPlaceholder)],
            [
                new DataTypeVariant(
                    "Ok",
                    [
                        new DataTypeField(
                            VariantIdentifierFieldName,
                            intRef),
                        new DataTypeField(
                            "Item0",
                            valueGeneric)
                    ]),
                new DataTypeVariant(
                    "Error",
                    [
                        new DataTypeField(
                            VariantIdentifierFieldName,
                            intRef),
                        new DataTypeField(
                            "Item0",
                            errorGeneric)
                    ])
            ],
            []);
        importedDataTypes.Add(resultDataType);
        foreach (var variant in TypeChecker.UnionSignature.Result.Variants.OfType<TypeChecker.TupleUnionVariant>())
        {
            importedMethods.Add(
                new LoweredMethod(
                    variant.CreateFunction.Id,
                    variant.CreateFunction.Name,
                    resultDataType.TypeParameters,
                    [],
                    new MethodLocal(ReturnValueLocalName, null, GetTypeReference(variant.CreateFunction.ReturnType)),
                    [..variant.CreateFunction.Parameters.Values.Select((x, i) => new MethodLocal(ParameterLocalName((uint)i), x.Name.StringValue, GetTypeReference(x.Type)))],
                    []));
        }

        var stringSignature = TypeChecker.InstantiatedClass.String.Signature;

        importedDataTypes.Add(new DataType(
            DefId.String,
            stringSignature.Name,
            [],
            [
                new DataTypeVariant(
                    ClassVariantName,
                    [..stringSignature.Fields.Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)))])
            ],
            []));
        
        _importedModules = [
            new LoweredModule
            {
                DataTypes = importedDataTypes,
                Methods = importedMethods
            }
        ];
        _program = program;
    }

    private LoweredModule LowerInner()
    {
        foreach (var dataType in _program.Unions.Select(x => LowerUnion(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        foreach (var dataType in _program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        var mainSignature = new TypeChecker.FunctionSignature(
                DefId.Main(_program.ModuleId),
                Token.Identifier("_Main", SourceSpan.Default),
                [],
                [],
                IsStatic: true,
                IsMutable: false,
                _program.Expressions,
                Extern: false)
        {
            ReturnType = TypeChecker.InstantiatedClass.Unit,
            OwnerType = null,
            LocalVariables = _program.TopLevelLocalVariables,
            LocalFunctions = _program.TopLevelLocalFunctions
        };
        foreach (var local in _program.TopLevelLocalVariables)
        {
            local.ContainingFunction = mainSignature;
        }

        var fnSignaturesToGenerate = _program.Functions.Select<LangFunction, TypeChecker.FunctionSignature>(x => x.Signature.NotNull());
        if (mainSignature.Expressions.Count > 0)
        {
            fnSignaturesToGenerate = fnSignaturesToGenerate.Prepend(mainSignature);
        }

        foreach (var fnSignature in fnSignaturesToGenerate)
        {
            var (method, basicBlocks, locals, expressions) = GenerateLoweredMethod(null, fnSignature, null, null);
            _methods.Add(method, (fnSignature, basicBlocks, locals, expressions, null, true));
        }

        foreach (var (method, (fnSignature, basicBlocks, locals, expressions, ownerTypeReference, _)) in _methods.Where(x => x.Value.needsLowering))
        {
            LowerMethod(method, fnSignature, basicBlocks, locals, expressions, ownerTypeReference);

        }

        return new LoweredModule
        {
            DataTypes = [.._types.Values],
            Methods = [.._methods.Keys]
        };
    }

    private void LowerMethod(
        LoweredMethod method,
        TypeChecker.FunctionSignature fnSignature,
        List<BasicBlock> basicBlocks,
        List<MethodLocal> locals,
        IReadOnlyList<Expressions.IExpression> expressions,
        LoweredConcreteTypeReference? ownerTypeReference)
    {
        _currentType = ownerTypeReference;
        _currentFunction = (method, fnSignature);
        _basicBlocks = basicBlocks;
        _locals = locals;

        if (basicBlocks.Count == 0)
        {
            _basicBlockStatements = [];
            basicBlocks.Add(new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements));
        }
        else
        {
            var basicBlock = basicBlocks[^1];
            _basicBlockStatements = [..basicBlock.Statements];
            basicBlocks[^1] = new BasicBlock(basicBlock.Id, _basicBlockStatements, basicBlock.Terminator);
        }

        foreach (var expression in expressions)
        {
            NewLowerExpression(expression, destination: null);
        }
        
        TerminateBasicBlocks();
    }

    private void TerminateBasicBlocks()
    {
        var lastBasicBlock = _basicBlocks[^1];
        var isLastBasicBlockEmpty = lastBasicBlock.Statements.Count == 0 && lastBasicBlock.Terminator is null;
        
        var returnBasicBlockId = (isLastBasicBlockEmpty || lastBasicBlock.Terminator is Return)
            ? lastBasicBlock.Id
            : new BasicBlockId($"bb{_basicBlocks.Count}");

        for (var i = 0; i < _basicBlocks.Count; i++)
        {
            var basicBlock = _basicBlocks[i];
            basicBlock.Terminator ??= new GoTo(new BasicBlockId($"bb{i + 1}"));

            switch (basicBlock.Terminator)
            {
                case GoTo goTo:
                {
                    if (goTo.BasicBlockId.Id == TempReturnBasicBlockId)
                    {
                        goTo.BasicBlockId.Id = returnBasicBlockId.Id;
                    }
                    break;
                }
                case MethodCall methodCall:
                {
                    if (methodCall.GoToAfter.Id == TempReturnBasicBlockId)
                    {
                        methodCall.GoToAfter.Id = returnBasicBlockId.Id;
                    }
                    break;
                }
                case Return:
                    break;
                case SwitchInt switchInt:
                {
                    foreach (var bbId in switchInt.Cases.Values.Append(switchInt.Otherwise).Where(x => x.Id == TempReturnBasicBlockId))
                    {
                        bbId.Id = returnBasicBlockId.Id;
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (!isLastBasicBlockEmpty)
        {
            if (lastBasicBlock.Terminator is not Return)
            {
                _basicBlocks.Add(new BasicBlock(returnBasicBlockId, [])
                {
                    Terminator = new Return()
                });
            }
        }
        else
        {
            lastBasicBlock.Terminator = new Return();
        }
    }

    private DataType LowerClass(TypeChecker.ClassSignature klass)
    {
        var typeParameters = klass.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var classTypeReference = new LoweredConcreteTypeReference(
                klass.Name,
                klass.Id,
                typeParameters);

        var staticFields = klass.Fields.Where(x => x.IsStatic)
            .Select<TypeChecker.TypeField, StaticDataTypeField>(x =>
            {
                _currentFunction = null;
                _currentType = classTypeReference;
                _basicBlockStatements = [];
                _basicBlocks = [new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements)];
                _locals = [];
                
                NewLowerExpression(x.StaticInitializer.NotNull(), destination: new Local(ReturnValueLocalName));
                
                TerminateBasicBlocks();

                var type = GetTypeReference(x.Type);


                return new StaticDataTypeField(
                    x.Name,
                    type,
                    _basicBlocks,
                    _locals,
                    new MethodLocal(ReturnValueLocalName, null, type));
            }).ToArray();

        var fields = klass.Fields.Where(x => !x.IsStatic)
            .Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)));

        foreach (var method in klass.Functions)
        {
            var (loweredMethod, basicBlocks, locals, expressions) = GenerateLoweredMethod(klass.Name, method, classTypeReference, classTypeReference);
            _methods.Add(loweredMethod, (method, basicBlocks, locals, expressions, classTypeReference, true));
        }

        return new DataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [new DataTypeVariant("_classVariant", [.. fields])],
                [.. staticFields]);
    }

    private DataType LowerUnion(TypeChecker.UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        foreach (var function in union.Functions)
        {
            var (loweredMethod, basicBlocks, locals, expressions) = GenerateLoweredMethod(union.Name, function, unionTypeReference, unionTypeReference);

            _methods.Add(loweredMethod, (function, basicBlocks, locals, expressions, unionTypeReference, true));
        }

        var variants = new List<DataTypeVariant>(union.Variants.Count);
        foreach (var variant in union.Variants)
        {
            var variantIdentifierField = new DataTypeField(
                    VariantIdentifierFieldName,
                    GetTypeReference(TypeChecker.InstantiatedClass.UInt16));
            var fields = new List<DataTypeField> { variantIdentifierField };
            switch (variant)
            {
                case TypeChecker.UnitUnionVariant:
                    break;
                case TypeChecker.ClassUnionVariant u:
                    {
                        fields.AddRange(u.Fields.Select(x => new DataTypeField(
                                        x.Name,
                                        GetTypeReference(x.Type))));
                        break;
                    }
                case TypeChecker.TupleUnionVariant u:
                    {
                        var memberTypes = u.TupleMembers.NotNull().Select(GetTypeReference).ToArray();
                        fields.AddRange(memberTypes.Select((x, i) => new DataTypeField(
                                        TupleElementName((uint)i),
                                        x)));

                        var createMethodFieldInitializations = fields.Skip(1)
                            .Select(
                                (IOperand operand, string fieldName) (_, i) => (new Copy(new Local($"_param{i}")), TupleElementName((uint)i)));
                        createMethodFieldInitializations = createMethodFieldInitializations.Prepend((new UIntConstant((ulong)variants.Count, 2), VariantIdentifierFieldName));

                        LoweredMethod method;

                        if (union.Boxed)
                        {
                            method = new LoweredMethod(
                                u.CreateFunction.Id,
                                u.CreateFunction.Name,
                                typeParameters,
                                [
                                    new BasicBlock(
                                        new BasicBlockId("bb0"),
                                        [],
                                        new MethodCall(
                                            new LoweredFunctionReference(DefId.Allocate, []),
                                            [new SizeOf(unionTypeReference)],
                                            new Local(ReturnValueLocalName),
                                            new BasicBlockId("bb1"))),
                                    new BasicBlock(new BasicBlockId("bb1"), [
                                        new Assign(new Deref(new Local(ReturnValueLocalName)),
                                            new CreateObject(unionTypeReference)),
                                        ..createMethodFieldInitializations.Select(x => new Assign(
                                            new Field(new Deref(new Local(ReturnValueLocalName)), x.fieldName, u.Name),
                                            new Use(x.operand)))
                                    ])
                                    {
                                        Terminator = new Return()
                                    },
                                ],
                                new MethodLocal(ReturnValueLocalName, null, new LoweredPointer(unionTypeReference)),
                                [
                                    ..memberTypes.Select((x, i) =>
                                        new MethodLocal(ParameterLocalName((uint)i), TupleElementName((uint)i), x))
                                ],
                                []);
                        }
                        else
                        {
                            method = new LoweredMethod(
                                u.CreateFunction.Id,
                                u.CreateFunction.Name,
                                typeParameters,
                                [
                                    new BasicBlock(new BasicBlockId("bb0"), [
                                        new Assign(new Local(ReturnValueLocalName),
                                            new CreateObject(unionTypeReference)),
                                        ..createMethodFieldInitializations.Select(x => new Assign(
                                            new Field(new Local(ReturnValueLocalName), x.fieldName, u.Name),
                                            new Use(x.operand)))
                                    ])
                                    {
                                        Terminator = new Return()
                                    },
                                ],
                                new MethodLocal(ReturnValueLocalName, null, unionTypeReference),
                                [
                                    ..memberTypes.Select((x, i) =>
                                        new MethodLocal(ParameterLocalName((uint)i), TupleElementName((uint)i), x))
                                ],
                                []);
                        }
                        
                        // add the tuple variant as a method
                        _methods.Add(
                                method,
                                // pass null as the signature because it's never used as the current function
                                (null!, [], [], [], unionTypeReference, false));
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
    private (LoweredMethod, List<BasicBlock>, List<MethodLocal>, IReadOnlyList<Expressions.IExpression>) GenerateLoweredMethod(
            string? ownerName,
            TypeChecker.FunctionSignature fnSignature,
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
                fnSignature.Id with { FullName = fnSignature.Id.FullName + "__Locals" },
                $"{name}__Locals",
                [],
                [
                    new DataTypeVariant(
                        "_classVariant",
                        [
                            ..localsAccessedInClosure
                                .Concat(parametersAccessedInClosure.Cast<TypeChecker.IVariable>())
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
            var fields = new Dictionary<DefId, DataTypeField>();

            foreach (var variable in fnSignature.AccessedOuterVariables)
            {
                switch (variable)
                {
                    case TypeChecker.LocalVariable localVariable:
                        {
                            var containingFunction = localVariable.ContainingFunction.NotNull();
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull(expectedReason: "the containing function containing the referenced local should have already been lowered");
                            var localType = _types[localTypeId];
                            var localTypeReference = new LoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            // try to add because we might have already added this function's 
                            // local type if we're referencing multiple variables from
                            // the owner function
                            fields.TryAdd(
                                localTypeId,
                                new DataTypeField(localType.Name, new LoweredPointer(localTypeReference))); 
                            break;
                        }
                    case TypeChecker.FunctionSignatureParameter parameterVariable:
                        {
                            var containingFunction = parameterVariable.ContainingFunction.NotNull(); 
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull();
                            var localType = _types[localTypeId];
                            var localTypeReference = new LoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            // try to add because we might have already added this function's 
                            // local type if we're referencing multiple variables from
                            // the owner function
                            fields.TryAdd(
                                    localTypeId,
                                    new DataTypeField(localType.Name, new LoweredPointer(localTypeReference)));
                            break;
                        }
                    case TypeChecker.FieldVariable fieldVariable:
                        {
                            var signature = fieldVariable.ContainingSignature;
                            var typeReference = new LoweredConcreteTypeReference(
                                    signature.Name,
                                    signature.Id,
                                    [..signature.TypeParameters
                                        .Select(GetTypeReference)]);

                            fields.TryAdd(
                                signature.Id,
                                new DataTypeField(ClosureThisFieldName, new LoweredPointer(typeReference)));

                            break;
                        }
                    case TypeChecker.ThisVariable:
                        {
                            Debug.Assert(parentTypeReference is not null);
                            fields.TryAdd(
                                parentTypeReference.DefinitionId,
                                new DataTypeField(ClosureThisFieldName, new LoweredPointer(parentTypeReference)));
                            break;
                        }
                }
            }

            closureType = new DataType(
                new DefId(fnSignature.Id.ModuleId, fnSignature.Id.FullName + "__Closure"),
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
            var (localMethod, localFnBasicBlocks, localFnLocals, localExpressions) = 
                GenerateLoweredMethod(ownerName: name, localSignature, null, parentTypeReference);

            _methods.Add(localMethod, (localSignature, localFnBasicBlocks, localFnLocals, localExpressions, parentTypeReference, true));
        }

        var locals = new List<MethodLocal>(
                fnSignature.LocalVariables.Count
                - localsAccessedInClosure.Length
                + (localsAccessedInClosure.Length > 0 ? 1 : 0));
        var basicBlocks = new List<BasicBlock>();

        if (localsType is not null)
        {
            var localsTypeReference = new LoweredConcreteTypeReference(
                            localsType.Name,
                            localsType.Id,
                            []);

            locals.Add(new MethodLocal(
                        LocalsObjectLocalName,
                        null,
                        new LoweredPointer(localsTypeReference)));
            var hasCompilerInsertedFirstParameter = 
                (!fnSignature.IsStatic && ownerTypeReference is not null)
                || closureType is not null;

            basicBlocks.Add(new BasicBlock(
                new BasicBlockId("bb0"),
                [],
                new MethodCall(
                    new LoweredFunctionReference(DefId.Allocate, []),
                    [new SizeOf(localsTypeReference)],
                    new Local(LocalsObjectLocalName),
                    new BasicBlockId("bb1"))));

            basicBlocks.Add(new BasicBlock(
                new BasicBlockId("bb1"),
                [
                new Assign(
                    new Deref(new Local(LocalsObjectLocalName)),
                    new CreateObject(
                        localsTypeReference)),
                ..parametersAccessedInClosure.Select(
                    parameter => new Assign(
                        new Field(
                            new Deref(new Local(LocalsObjectLocalName)),
                            parameter.Name.StringValue,
                            ClassVariantName),
                        new Use(new Copy(new Local(ParameterLocalName(parameter.ParameterIndex + (uint)(hasCompilerInsertedFirstParameter ? 1 : 0)))))))
            ]));
        }

        foreach (var localVariable in fnSignature.LocalVariables.Where(x => !x.ReferencedInClosure))
        {
            locals.Add(new MethodLocal(LocalName((uint)locals.Count), localVariable.Name.StringValue, GetTypeReference(localVariable.Type)));
        }

        var parameters = fnSignature.Parameters.Select(y =>
            (userGivenName: (string?)y.Key, paramType: GetTypeReference(y.Value.Type)));

        if (!fnSignature.IsStatic && ownerTypeReference is not null)
        {
            parameters = parameters.Prepend((ThisParameterName, new LoweredPointer(ownerTypeReference)));
        }
        else if (closureType is not null)
        {
            parameters = parameters.Prepend((ClosureParameterName, new LoweredPointer(new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []))));
        }

        return (
            new LoweredMethod(
                fnSignature.Id,
                name,
                [
                    .. fnSignature.TypeParameters.Select(GetGenericPlaceholder),
                    .. ownerTypeReference?.TypeArguments.Select(x => (x as LoweredGenericPlaceholder).NotNull()) ?? []
                ],
                basicBlocks,
                new MethodLocal(ReturnValueLocalName, null, GetTypeReference(fnSignature.ReturnType)),
                [.. parameters.Select((x, i) => new MethodLocal(ParameterLocalName((uint)i), x.userGivenName, x.paramType))],
                locals),
            basicBlocks,
            locals,
            fnSignature.Expressions);
    }

    private LoweredGenericPlaceholder GetGenericPlaceholder(TypeChecker.GenericPlaceholder placeholder)
    {
        return new LoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
    }

    private DataType GetDataType(
            DefId typeId)
    {
        if (_types.TryGetValue(typeId, out var dataType))
            return dataType;

        return _importedModules.Select(x => x.DataTypes.FirstOrDefault(y => y.Id == typeId))
            .FirstOrDefault(x => x is not null)
            ?? throw new InvalidOperationException($"No data type with id {typeId} was found");
    }

    private LoweredFunctionReference GetFunctionReference(
            DefId functionId,
            IReadOnlyList<ILoweredTypeReference> typeArguments,
            IReadOnlyList<ILoweredTypeReference> ownerTypeArguments)
    {
        var loweredMethod = _methods.Keys.FirstOrDefault(x => x.Id == functionId)
            ?? _importedModules.SelectMany(x => x.Methods)
                .First(x => x.Id == functionId);

        IReadOnlyList<ILoweredTypeReference> resultingTypeArguments = [..typeArguments, ..ownerTypeArguments];
        
        Debug.Assert(resultingTypeArguments.Count == loweredMethod.TypeParameters.Count);

        return new LoweredFunctionReference(
                functionId,
                resultingTypeArguments);
    }

    private LoweredConcreteTypeReference GetConcreteTypeReference(ILoweredTypeReference typeReference)
    {
        return typeReference switch
        {
            LoweredConcreteTypeReference concrete => concrete,
            LoweredPointer(var pointerTo) => GetConcreteTypeReference(pointerTo),
            _ => throw new InvalidOperationException($"{typeReference.GetType()}")
        };
    }

    private ILoweredTypeReference GetTypeReference(TypeChecker.ITypeReference typeReference)
    {
        var loweredTypeReference = typeReference switch
        {
            TypeChecker.InstantiatedClass c => new LoweredConcreteTypeReference(
                c.Signature.Name,
                c.Signature.Id,
                [.. c.TypeArguments.Select(GetTypeReference)]),
            TypeChecker.InstantiatedUnion u => new LoweredConcreteTypeReference(
                u.Signature.Name,
                u.Signature.Id,
                [.. u.TypeArguments.Select(GetTypeReference)]),
            TypeChecker.GenericTypeReference {ResolvedType: {} resolvedType} => GetTypeReference(resolvedType),
            TypeChecker.GenericTypeReference {ResolvedType: null} g => new LoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            TypeChecker.GenericPlaceholder g => new LoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            TypeChecker.UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            TypeChecker.FunctionObject f => FunctionObjectCase(f),
            TypeChecker.UnspecifiedSizedIntType i => GetTypeReference(i.ResolvedIntType.NotNull()),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };

        if (IsTypeReferenceBoxed(typeReference))
        {
            loweredTypeReference = new LoweredPointer(loweredTypeReference);
        }

        return loweredTypeReference;

        LoweredConcreteTypeReference FunctionObjectCase(TypeChecker.FunctionObject f)
        {
            var type = _importedModules
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