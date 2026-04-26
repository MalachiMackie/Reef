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
    private readonly List<LoweredExternMethod> _externMethods = [];
    private readonly Dictionary<DefId, DataType> _types = [];
    private readonly Dictionary<ModuleId, LangModule> _modules;
    private readonly LangModule _mainModule;
    private LoweredConcreteTypeReference? _currentType;
    private (LoweredMethod LoweredMethod, TypeChecker.FunctionSignature FunctionSignature)? _currentFunction;

    // private static readonly LoweredProgram ReefCoreModule;
    // static ProgramAbseil()
    // {
    //     var coreLibDataTypes = new List<DataType>();
    //     var coreLibMethods = new List<IMethod>
    //     {
    //         // ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintString),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintI8),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintI16),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintI32),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintI64),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintU8),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintU16),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintU32),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.PrintU64),
    //         ExternMethodFromSignature(TypeChecker.FunctionSignature.Allocate),
    //         CreateBoxMethod(TypeChecker.FunctionSignature.Box),
    //         CreateUnboxMethod(TypeChecker.FunctionSignature.Unbox),
    //     };

    //     var rawPointerType = new LoweredConcreteTypeReference(
    //             TypeChecker.ClassSignature.RawPointer.Value.Name,
    //             TypeChecker.ClassSignature.RawPointer.Value.Id,
    //             []);

    //     for (var i = 0; i < 7; i++)
    //     {
    //         var fnClass = TypeChecker.ClassSignature.Function(i);
    //         coreLibDataTypes.Add(
    //             new DataType(
    //                 fnClass.Id,
    //                 fnClass.Name,
    //                 [.. fnClass.TypeParameters.Select(GetGenericPlaceholder)],
    //                 [
    //                     new DataTypeVariant(
    //                         "_classVariant",
    //                         [
    //                             new DataTypeField(
    //                                 "FunctionReference",
    //                                 new RawPointer()),
    //                             new DataTypeField(
    //                                 "FunctionParameter",
    //                                 rawPointerType)
    //                         ])
    //                 ],
    //                 []));

    //         /*
    //             * pub class Function`1<TReturn>
    //             * {
    //             *     pub field FunctionReference: FnType<TReturn>,
    //             *     pub field FunctionParameter: Option<Ptr>,
    //             *
    //             *     pub fn Call(): TReturn
    //             *     {
    //             *         if (FunctionParameter matches Option::Some(ptr)) {
    //             *           return ((FnType<Ptr, TReturn>)FunctionReference)(ptr);
    //             *         }
    //             *         return FunctionReference();
    //             *     }
    //             * }
    //             *
    //             */

    //         var call = fnClass.Functions[0];

    //         coreLibMethods.Add(
    //             new LoweredMethod(
    //                 call.Id,
    //                 $"{fnClass.Name}__{call.Name}",
    //                 [.. fnClass.TypeParameters.Select(GetGenericPlaceholder)],
    //                 [],
    //                 ReturnValue: new MethodLocal(ReturnValueLocalName, null, GetTypeReference(call.ReturnType)),
    //                 ParameterLocals: [.. call.Parameters.Values.Select((x, j) => new MethodLocal(ParameterLocalName((uint)j), x.Name.StringValue, GetTypeReference(x.Type)))],
    //                 []));
    //     }

    //     var errorGeneric = new LoweredGenericPlaceholder(
    //                                 TypeChecker.UnionSignature.Result.Id,
    //                                 "TError");
    //     var valueGeneric = new LoweredGenericPlaceholder(
    //                                 TypeChecker.UnionSignature.Result.Id,
    //                                 "TValue");
    //     var intRef = new LoweredConcreteTypeReference(
    //                                 TypeChecker.ClassSignature.Int64.Value.Name,
    //                                 TypeChecker.ClassSignature.Int64.Value.Id,
    //                                 []);

    //     var resultDataType = new DataType(
    //         TypeChecker.UnionSignature.Result.Id,
    //         TypeChecker.UnionSignature.Result.Name,
    //         [.. TypeChecker.UnionSignature.Result.TypeParameters.Select(GetGenericPlaceholder)],
    //         [
    //             new DataTypeVariant(
    //                 "Ok",
    //                 [
    //                     new DataTypeField(
    //                         VariantIdentifierFieldName,
    //                         intRef),
    //                     new DataTypeField(
    //                         "Item0",
    //                         valueGeneric)
    //                 ]),
    //             new DataTypeVariant(
    //                 "Error",
    //                 [
    //                     new DataTypeField(
    //                         VariantIdentifierFieldName,
    //                         intRef),
    //                     new DataTypeField(
    //                         "Item0",
    //                         errorGeneric)
    //                 ])
    //         ],
    //         []);
    //     coreLibDataTypes.Add(resultDataType);
    //     foreach (var variant in TypeChecker.UnionSignature.Result.Variants.OfType<TypeChecker.TupleUnionVariant>())
    //     {
    //         coreLibMethods.Add(
    //             new LoweredMethod(
    //                 variant.BoxedCreateFunction.Id,
    //                 variant.BoxedCreateFunction.Name,
    //                 resultDataType.TypeParameters,
    //                 [],
    //                 new MethodLocal(ReturnValueLocalName, null, GetTypeReference(variant.BoxedCreateFunction.ReturnType)),
    //                 [.. variant.BoxedCreateFunction.Parameters.Values.Select((x, i) => new MethodLocal(ParameterLocalName((uint)i), x.Name.StringValue, GetTypeReference(x.Type)))],
    //                 []));

    //         coreLibMethods.Add(
    //                         new LoweredMethod(
    //                             variant.UnboxedCreateFunction.Id,
    //                             variant.UnboxedCreateFunction.Name,
    //                             resultDataType.TypeParameters,
    //                             [],
    //                             new MethodLocal(ReturnValueLocalName, null, GetTypeReference(variant.BoxedCreateFunction.ReturnType)),
    //                             [.. variant.UnboxedCreateFunction.Parameters.Values.Select((x, i) => new MethodLocal(ParameterLocalName((uint)i), x.Name.StringValue, GetTypeReference(x.Type)))],
    //                             []));
    //     }

    //     coreLibDataTypes.AddRange(
    //         new[] {
    //             TypeChecker.ClassSignature.Unit.Value,
    //             TypeChecker.ClassSignature.String.Value,
    //             TypeChecker.ClassSignature.Int8.Value,
    //             TypeChecker.ClassSignature.Int16.Value,
    //             TypeChecker.ClassSignature.Int32.Value,
    //             TypeChecker.ClassSignature.Int64.Value,
    //             TypeChecker.ClassSignature.UInt8.Value,
    //             TypeChecker.ClassSignature.UInt16.Value,
    //             TypeChecker.ClassSignature.UInt32.Value,
    //             TypeChecker.ClassSignature.UInt64.Value,
    //             TypeChecker.ClassSignature.Boolean.Value,
    //             TypeChecker.ClassSignature.RawPointer.Value,
    //             TypeChecker.ClassSignature.BoxedValue.Value,
    //             TypeChecker.ClassSignature.ObjectHeader.Value,
    //         }.Select(x => new DataType(
    //             x.Id,
    //             x.Name,
    //             [.. x.TypeParameters.Select(GetGenericPlaceholder)],
    //             [new DataTypeVariant(ClassVariantName, [.. x.Fields.Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)))])],
    //             []
    //         ))
    //     );

    //     // ReefCoreModule = new LoweredProgram
    //     // {
    //     //     Id = DefId.CoreLibModuleId,
    //     //     DataTypes = coreLibDataTypes,
    //     //     Methods = coreLibMethods
    //     // };
    // }


    // private readonly IReadOnlyList<LoweredModule> _importedModules;
    private List<BasicBlock> _basicBlocks = [];
    private List<IStatement> _basicBlockStatements = [];
    private List<MethodLocal> _locals = [];

    public static LoweredProgram Lower(
        Dictionary<ModuleId, LangModule> modules,
        ModuleId mainModuleId)
    {
        var abseil = new ProgramAbseil(modules, mainModuleId);
        return abseil.LowerInner();
    }

    private static LoweredExternMethod ExternMethodFromSignature(TypeChecker.FunctionSignature signature)
    {
        Debug.Assert(signature.ExternName is not null);

        return new LoweredExternMethod(
            signature.Id,
            signature.ExternName,
            [.. signature.TypeParameters.Select(x => new LoweredGenericPlaceholder(signature.Id, x.GenericName))],
            ReturnValue: new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            ParameterLocals: [..signature.Parameters.Select((x, i) => new MethodLocal(
                ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))]);
    }

    private static LoweredMethod CreateBoxMethod(TypeChecker.FunctionSignature signature)
    {
        var basicBlocks = new List<BasicBlock>();

        var loweredMethod = new LoweredMethod(
            signature.Id,
            signature.Name,
            [.. signature.TypeParameters.Select(GetGenericPlaceholder)],
            basicBlocks,
            new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            [..signature.Parameters.Select((x, i) => new MethodLocal(
                LocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
            []);

        /*
         *
         * pub fn box<TParam, TReturn>(TParam param): TReturn
         *     where TParam:  unboxed TReturn,
         *           TReturn: boxed TParam
         * {
         *     _returnValue = allocate(sizeof(BoxedValue::<TParam>));
         *     *_returnValue = new BoxedValue { ObjectHeader = new ObjectHeader { TypeId = typeof(TParam) }, Value = param }
         *     return;
         * }
         *
         */

        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb0"),
            [],
            new MethodCall(
                new LoweredFunctionReference(
                    DefId.Allocate, []),
                [new SizeOf(BoxedValueType(loweredMethod.ParameterLocals[0].Type))],
                new Local(loweredMethod.ReturnValue.CompilerGivenName),
                new BasicBlockId("bb1"))));
        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb1"),
            [
                new Assign(
                    new Deref(new Local(loweredMethod.ReturnValue.CompilerGivenName)),
                    new CreateObject(BoxedValueType(loweredMethod.ParameterLocals[0].Type))
                ),
                new Assign(
                    new Field(new Deref(new Local(loweredMethod.ReturnValue.CompilerGivenName)), "ObjectHeader", ClassVariantName),
                    new CreateObject((GetTypeReference(TypeChecker.InstantiatedClass.ObjectHeader) as LoweredConcreteTypeReference).NotNull())
                ),
                new Assign(
                    new Field(new Field(new Deref(new Local(loweredMethod.ReturnValue.CompilerGivenName)), "ObjectHeader", ClassVariantName), "TypeId", ClassVariantName),
                    new Use(new TypeIdOf(loweredMethod.ParameterLocals[0].Type))
                ),
                new Assign(
                    new Field(new Deref(new Local(loweredMethod.ReturnValue.CompilerGivenName)), "Value", ClassVariantName),
                    new Use(new Copy(new Local(loweredMethod.ParameterLocals[0].CompilerGivenName)))
                )
            ],
            new Return()));

        return loweredMethod;
    }

    private static LoweredMethod CreateUnboxMethod(TypeChecker.FunctionSignature signature)
    {
        var basicBlocks = new List<BasicBlock>();

        var loweredMethod = new LoweredMethod(
            signature.Id,
            signature.Name,
            [.. signature.TypeParameters.Select(GetGenericPlaceholder)],
            basicBlocks,
            new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            [..signature.Parameters.Select((x, i) => new MethodLocal(
                LocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
            []);

        /*
         *
         * pub fn unbox<TParam, TReturn>(TParam param): TReturn
         *     where TParam:  boxed TReturn,
         *           TReturn: unboxed TParam
         * {
         *     _returnValue = (*param as BoxedValue).Value;
         *     return;
         * }
         *
         */

        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb0"),
            [new Assign(
                new Local(loweredMethod.ReturnValue.CompilerGivenName),
                new Use(new Copy(new Field(new Deref(new Local(loweredMethod.ParameterLocals[0].CompilerGivenName)), "Value", ClassVariantName))))],
            new Return()));

        return loweredMethod;
    }

    private ProgramAbseil(Dictionary<ModuleId, LangModule> modules, ModuleId mainModuleId)
    {
        // _importedModules = importedModules.Select(x => new LoweredModule
        // {
        //     Id = x.ModuleId,
        //     DataTypes = [.. x.Classes.Select(x => new DataType { }).Concat(x.Unions.Select(x => new DataType))],
        //     Methods
        // });
        // _importedModules = [
        //     ReefCoreModule,
        //     new LoweredModule
        //     {
        //         Id = DefId.DiagnosticsModuleId,
        //         DataTypes = [],
        //         Methods = [..TypeChecker.FunctionSignature.DiagnosticFunctions.Select(ExternMethodFromSignature)]
        //     },
        //     new LoweredModule
        //     {
        //         Id = DefId.ReflectionModuleId,
        //         DataTypes = [
        //             .. TypeChecker.ClassSignature.ReflectionClasses.Value.Select(x =>
        //                 new DataType(
        //                     x.Id,
        //                     x.Name,
        //                     [],
        //                     [
        //                         new DataTypeVariant(
        //                             ClassVariantName,
        //                             [.. x.Fields.Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)))])
        //                     ],
        //                     []
        //                 )),
        //             ..TypeChecker.UnionSignature.ReflectionUnions.Value.Select(LowerUnion)
        //         ],
        //         Methods = []
        //     }
        // ];
        _modules = modules;
        _mainModule = modules[mainModuleId];
    }

    private LoweredProgram LowerInner()
    {
        foreach (var (moduleId, module) in _modules)
        {
            foreach (var union in module.Unions)
            {
                var dataType = LowerUnion(union.Signature.NotNull());

                _types.Add(dataType.Id, dataType);

                LowerUnionMethods(union.Signature.NotNull());

            }

            foreach (var dataType in module.Classes.Select(x => LowerClass(x.Signature.NotNull())))
            {
                _types.Add(dataType.Id, dataType);
            }
        }

        var mainSignature = new TypeChecker.FunctionSignature(
                DefId.Main(_mainModule.ModuleId),
                Token.Identifier("_Main", SourceSpan.Default),
                [],
                [],
                IsStatic: true,
                IsMutable: false,
                _mainModule.Expressions,
                ExternName: null,
                false,
                IsPublic: true)
        {
            ReturnType = TypeChecker.InstantiatedClass.Unit,
            OwnerType = null,
            LocalVariables = _mainModule.TopLevelLocalVariables,
            LocalFunctions = _mainModule.TopLevelLocalFunctions
        };
        foreach (var local in _mainModule.TopLevelLocalVariables)
        {
            local.ContainingFunction = mainSignature;
        }

        var fnSignaturesToGenerate = _modules.Values.SelectMany(x => x.Functions.Select(x => x.Signature.NotNull()));
        if (mainSignature.Expressions.Count > 0)
        {
            fnSignaturesToGenerate = fnSignaturesToGenerate.Prepend(mainSignature);
        }

        foreach (var fnSignature in fnSignaturesToGenerate)
        {
            if (fnSignature.ExternName is null)
            {
                var (method, basicBlocks, locals, expressions) = GenerateLoweredMethod(null, fnSignature, null, null);
                _methods.Add(method, (fnSignature, basicBlocks, locals, expressions, null, true));
            }
            else
            {
                _externMethods.Add(ExternMethodFromSignature(fnSignature));
            }
        }

        foreach (var (method, (fnSignature, basicBlocks, locals, expressions, ownerTypeReference, _)) in _methods.Where(x => x.Value.needsLowering))
        {
            LowerMethod(method, fnSignature, basicBlocks, locals, expressions, ownerTypeReference);
        }

        return new LoweredProgram
        {
            DataTypes = [.. _types.Values],
            Methods = [.. _methods.Keys, .. _externMethods]
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
            _basicBlockStatements = [.. basicBlock.Statements];
            basicBlocks[^1] = new BasicBlock(basicBlock.Id, _basicBlockStatements, basicBlock.Terminator);
        }

        foreach (var expression in expressions)
        {
            LowerExpression(expression, destination: null);
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
                case Assert assert:
                    {
                        if (assert.GoTo.Id == TempReturnBasicBlockId)
                        {
                            assert.GoTo.Id = returnBasicBlockId.Id;
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
            .Select(x =>
            {
                _currentFunction = null;
                _currentType = classTypeReference;
                _basicBlockStatements = [];
                _basicBlocks = [new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements)];
                _locals = [];

                LowerExpression(x.StaticInitializer.NotNull(), destination: new Local(ReturnValueLocalName));

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

    private void LowerUnionMethods(TypeChecker.UnionSignature union)
    {
        var dataType = _types[union.Id];
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

        foreach (var (index, variant) in union.Variants.Index())
        {
            if (variant is not TypeChecker.TupleUnionVariant u)
            {
                continue;
            }

            var memberTypes = u.TupleMembers.NotNull().Select(GetTypeReference).ToArray();

            var fields = dataType.Variants.First(x => x.Name == variant.Name).Fields;

            IEnumerable<(IOperand operand, string fieldName)> createMethodFieldInitializations = [
                (new UIntConstant((ulong)index, 2), VariantIdentifierFieldName),
                ..fields.Skip(1)
                    .Select((IOperand operand, string fieldName) (_, i) => (new Copy(new Local($"_param{i}")), TupleElementName((uint)i)))
            ];

            var boxedMethod = new LoweredMethod(
                u.BoxedCreateFunction.Id,
                u.BoxedCreateFunction.Name,
                typeParameters,
                [
                    new BasicBlock(
                            new BasicBlockId("bb0"),
                            [],
                            new MethodCall(
                                new LoweredFunctionReference(DefId.Allocate, []),
                                [new SizeOf(BoxedValueType(unionTypeReference))],
                                new Local(ReturnValueLocalName),
                                new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [
                            new Assign(
                                new Deref(new Local(ReturnValueLocalName)),
                                new CreateObject(BoxedValueType(unionTypeReference))),
                            new Assign(
                                new Field(new Deref(new Local(ReturnValueLocalName)), "ObjectHeader", ClassVariantName),
                                new CreateObject((GetTypeReference(TypeChecker.InstantiatedClass.ObjectHeader) as LoweredConcreteTypeReference).NotNull())
                            ),
                            new Assign(
                                new Field(new Field(new Deref(new Local(ReturnValueLocalName)), "ObjectHeader", ClassVariantName), "TypeId", ClassVariantName),
                                new Use(new TypeIdOf(unionTypeReference))
                            ),
                            new Assign(
                                new Field(new Deref(new Local(ReturnValueLocalName)), "Value", ClassVariantName),
                                new CreateObject(unionTypeReference)
                            ),
                            ..createMethodFieldInitializations.Select(x => new Assign(
                                new Field(new Field(new Deref(new Local(ReturnValueLocalName)), "Value", ClassVariantName), x.fieldName, u.Name),
                                new Use(x.operand)))
                        ])
                        {
                            Terminator = new Return()
                        },
                ],
                new MethodLocal(ReturnValueLocalName, null, new LoweredPointer(BoxedValueType(unionTypeReference))),
                [
                    ..memberTypes.Select((x, i) =>
                            new MethodLocal(ParameterLocalName((uint)i), TupleElementName((uint)i), x))
                ],
                []);

            var unboxedMethod = new LoweredMethod(
                u.UnboxedCreateFunction.Id,
                u.UnboxedCreateFunction.Name,
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

            // add the tuple variant as a method
            _methods.Add(
                    boxedMethod,
                    // pass null as the signature because it's never used as the current function
                    (null!, [], [], [], unionTypeReference, false));
            _methods.Add(
                    unboxedMethod,
                    // pass null as the signature because it's never used as the current function
                    (null!, [], [], [], unionTypeReference, false));
        }
    }

    private static DataType LowerUnion(TypeChecker.UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);


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
                                new DataTypeField(localType.Name, new LoweredPointer(BoxedValueType(localTypeReference))));
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
                                    new DataTypeField(localType.Name, new LoweredPointer(BoxedValueType(localTypeReference))));
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
                                new DataTypeField(ClosureThisFieldName, new LoweredPointer(BoxedValueType(typeReference))));

                            break;
                        }
                    case TypeChecker.ThisVariable:
                        {
                            Debug.Assert(parentTypeReference is not null);
                            fields.TryAdd(
                                parentTypeReference.DefinitionId,
                                new DataTypeField(ClosureThisFieldName, new LoweredPointer(BoxedValueType(parentTypeReference))));
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
                        new LoweredPointer(BoxedValueType(localsTypeReference))));
            var hasCompilerInsertedFirstParameter =
                (!fnSignature.IsStatic && ownerTypeReference is not null)
                || closureType is not null;

            basicBlocks.Add(new BasicBlock(
                new BasicBlockId("bb0"),
                [],
                new MethodCall(
                    new LoweredFunctionReference(DefId.Allocate, []),
                    [new SizeOf(BoxedValueType(localsTypeReference))],
                    new Local(LocalsObjectLocalName),
                    new BasicBlockId("bb1"))));

            basicBlocks.Add(new BasicBlock(
                new BasicBlockId("bb1"),
                [
                    new Assign(
                        new Deref(new Local(LocalsObjectLocalName)),
                        new CreateObject(
                            BoxedValueType(localsTypeReference))
                    ),
                    new Assign(
                        new Field(new Deref(new Local(LocalsObjectLocalName)), "ObjectHeader", ClassVariantName),
                        new CreateObject(GetConcreteTypeReference(GetTypeReference(TypeChecker.InstantiatedClass.ObjectHeader)))),
                    new Assign(
                        new Field(
                            new Field(new Deref(new Local(LocalsObjectLocalName)), "ObjectHeader", ClassVariantName),
                            "TypeId",
                            ClassVariantName),
                        new Use(new TypeIdOf(localsTypeReference))
                    ),
                    new Assign(
                        new Field(new Deref(new Local(LocalsObjectLocalName)), "Value", ClassVariantName),
                        new CreateObject(
                            localsTypeReference)),
                    ..parametersAccessedInClosure.Select(
                        parameter => new Assign(
                            new Field(
                                new Field(new Deref(new Local(LocalsObjectLocalName)), "Value", ClassVariantName),
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
            parameters = parameters.Prepend((ThisParameterName, new LoweredPointer(BoxedValueType(ownerTypeReference))));
        }
        else if (closureType is not null)
        {
            parameters = parameters.Prepend((ClosureParameterName, new LoweredPointer(BoxedValueType(new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                [])))));
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

    private static LoweredGenericPlaceholder GetGenericPlaceholder(TypeChecker.GenericPlaceholder placeholder)
    {
        return new LoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
    }

    private DataType GetDataType(
            DefId typeId)
    {
        return _types[typeId];
    }

    private LoweredFunctionReference GetFunctionReference(
            DefId functionId,
            IReadOnlyList<ILoweredTypeReference> typeArguments,
            IReadOnlyList<ILoweredTypeReference> ownerTypeArguments)
    {
        var loweredMethod = _methods.Keys.FirstOrDefault<IMethod>(x => x.Id == functionId)
            ?? _externMethods.FirstOrDefault<IMethod>(x => x.Id == functionId)
            ?? throw new InvalidOperationException($"No function found with id {functionId}");

        IReadOnlyList<ILoweredTypeReference> resultingTypeArguments = [.. ownerTypeArguments, .. typeArguments];

        Debug.Assert(resultingTypeArguments.Count == loweredMethod.TypeParameters.Count);

        return new LoweredFunctionReference(
                functionId,
                resultingTypeArguments);
    }

    private static LoweredConcreteTypeReference GetConcreteTypeReference(ILoweredTypeReference typeReference)
    {
        switch (typeReference)
        {
            case LoweredConcreteTypeReference concrete:
                return concrete;
            case LoweredPointer(LoweredConcreteTypeReference pointerTo):
                Debug.Assert(pointerTo.DefinitionId == DefId.BoxedValue);
                return GetConcreteTypeReference(pointerTo.TypeArguments[0]);
            default:
                throw new InvalidOperationException($"{typeReference.GetType()}");
        }
    }

    private static LoweredConcreteTypeReference BoxedValueType(ILoweredTypeReference valueType)
    {
        return new LoweredConcreteTypeReference(TypeChecker.ClassSignature.BoxedValue.Value.Name, DefId.BoxedValue, [valueType]);
    }

    private static ILoweredTypeReference GetTypeReference(TypeChecker.ITypeReference typeReference)
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
            TypeChecker.GenericTypeReference { ResolvedType: { } resolvedType } => GetTypeReference(resolvedType),
            TypeChecker.GenericTypeReference { ResolvedType: null } g => new LoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            TypeChecker.GenericPlaceholder g => new LoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            TypeChecker.UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            TypeChecker.FunctionObject f => FunctionObjectCase(f),
            TypeChecker.UnspecifiedSizedIntType i => GetTypeReference(i.ResolvedIntType.NotNull()),
            TypeChecker.ArrayType a => new LoweredArray(
                GetTypeReference(a.ElementType), a.Length),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };

        if (IsTypeReferenceBoxed(typeReference) && loweredTypeReference is not LoweredPointer)
        {
            loweredTypeReference = new LoweredPointer(BoxedValueType(loweredTypeReference));
        }

        return loweredTypeReference;

        static LoweredConcreteTypeReference FunctionObjectCase(TypeChecker.FunctionObject f)
        {
            var id = DefId.FunctionObject(f.Parameters.Count);

            return new LoweredConcreteTypeReference(
                id.FullName[(id.FullName.LastIndexOf(':') + 1)..],
                id,
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
            (LoweredPointer pointerA, LoweredPointer pointerB) => EqualTypeReferences(pointerA.PointerTo, pointerB.PointerTo),
            _ => false
        };
    }
}
