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

    private List<BasicBlock> _basicBlocks = [];
    private List<IStatement> _basicBlockStatements = [];
    private List<MethodLocal> _locals = [];

    public static LoweredProgram Lower(
        Dictionary<ModuleId, LangModule> modules,
        ModuleId mainModuleId,
        bool generateTestMain = false
    )
    {
        var abseil = new ProgramAbseil(modules, mainModuleId);
        return abseil.LowerInner(generateTestMain);
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

    private static LoweredMethod CreateFromBytesMethod(TypeChecker.FunctionSignature signature)
    {
        var basicBlocks = new List<BasicBlock>();

        var loweredMethod = new LoweredMethod(
            signature.Id,
            signature.Name,
            [.. signature.TypeParameters.Select(GetGenericPlaceholder)],
            basicBlocks,
            new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            [..signature.Parameters.Select((x, i) => new MethodLocal(
                ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
            []
        );

        /*

            fn from_bytes<TValue>(bytes: [u8]): TValue
                where TValue: boxed {
                return value as TValue;
            }

        */

        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb0"),
            [
                new Assign(
                    new Local(ReturnValueLocalName),
                    new Use(new Copy(new Local(ParameterLocalName(0))))
                )
            ],
            new Return()
        ));

        return loweredMethod;
    }

    private static LoweredMethod CreateAsBytesMethod(TypeChecker.FunctionSignature signature)
    {
        var basicBlocks = new List<BasicBlock>();

        var loweredMethod = new LoweredMethod(
            signature.Id,
            signature.Name,
            [.. signature.TypeParameters.Select(GetGenericPlaceholder)],
            basicBlocks,
            new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            [..signature.Parameters.Select((x, i) => new MethodLocal(
                ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
            []
        );

        /*

            fn as_bytes<TValue>(value: TValue): [u8]
                where TValue: boxed {
                return value as [u8];
            }

         */

        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb0"),
            [
                new Assign(
                     new Local(ReturnValueLocalName),
                     new Use(new Copy(new Local(ParameterLocalName(0))))
                 )
            ],
            new Return()
        ));

        return loweredMethod;
    }

    private static LoweredMethod CreateCatchUnwindIntrinsicMethod(TypeChecker.FunctionSignature signature)
    {
        var basicBlocks = new List<BasicBlock>();

        var loweredMethod = new LoweredMethod(
            signature.Id,
            signature.Name,
            [.. signature.TypeParameters.Select(GetGenericPlaceholder)],
            basicBlocks,
            new MethodLocal(ReturnValueLocalName, null, GetTypeReference(signature.ReturnType)),
            [..signature.Parameters.Select((x, i) => new MethodLocal(
                ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
            [new MethodLocal(LocalName(0), null, new LoweredConcreteTypeReference(DefId.Unit, []))],
            new BasicBlockId("bb2")
        );

        /*
            fn catch_unwind_intrinsic(callee: Fn([u8]), data_bytes: [u8]): bool {
                try {
                    callee(data_bytes);
                    return true;
                }
                catch {
                    return false;
                }
            }
         */

        var dataBytesTypeReference = loweredMethod.ParameterLocals[1].Type;

        var functionObjectCallMethod = new LoweredFunctionReference(
            DefId.FunctionObject_Call(parameterCount: 1),
            [dataBytesTypeReference, new LoweredConcreteTypeReference(DefId.Unit, [])]);

        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb0"),
            [],
            new MethodCall(
                functionObjectCallMethod,
                [
                    new Copy(new Local(ParameterLocalName(0))),
                    new Copy(new Local(ParameterLocalName(1)))
                ],
                new Local(LocalName(0)),
                new BasicBlockId("bb1")
            )
        ));
        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb1"),
            [
                new Assign(
                    new Local(ReturnValueLocalName),
                    new Use(new BoolConstant(true))
                )
            ],
            new Return()
        ));
        basicBlocks.Add(new BasicBlock(
            new BasicBlockId("bb2"),
            [
                new Assign(
                    new Local(ReturnValueLocalName),
                    new Use(new BoolConstant(false))
                )
            ],
            new Return()
        ));

        return loweredMethod;
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
                ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
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
                    new CreateObject(new LoweredConcreteTypeReference(DefId.ObjectHeader, []))
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
        _modules = modules;
        _mainModule = modules[mainModuleId];
    }

    private LoweredProgram LowerInner(bool generateTestMain)
    {
        HashSet<DefId> intrinsicFunctionIds = [];
        foreach (var (moduleId, module) in _modules)
        {
            if (moduleId == DefId.CoreLibModuleId)
            {
                var boxSignature = module.Functions.Select(x => x.Signature).First(x => x is not null && x.Id == DefId.Box).NotNull();
                var unboxSignature = module.Functions.Select(x => x.Signature).First(x => x is not null && x.Id == DefId.Unbox).NotNull();
                var asBytesSignature = module.Functions.Select(x => x.Signature).First(x => x is not null && x.Id == DefId.AsBytes).NotNull();
                var fromBytesSignature = module.Functions.Select(x => x.Signature).First(x => x is not null && x.Id == DefId.FromBytes).NotNull();
                var catchUnwindSignature = module.Functions.Select(x => x.Signature).First(x => x is not null && x.Id == DefId.CatchUnwindIntrinsic).NotNull();
                IEnumerable<(TypeChecker.FunctionSignature, LoweredMethod)> signatures = [
                    (boxSignature, CreateBoxMethod(boxSignature)),
                    (unboxSignature, CreateUnboxMethod(unboxSignature)),
                    (asBytesSignature, CreateAsBytesMethod(asBytesSignature)),
                    (fromBytesSignature, CreateFromBytesMethod(fromBytesSignature)),
                    (catchUnwindSignature, CreateCatchUnwindIntrinsicMethod(catchUnwindSignature)),
                ];
                foreach (var (signature, method) in signatures)
                {
                    intrinsicFunctionIds.Add(signature.Id);
                    _methods.Add(method, (signature, [], [], [], null, false));
                }
            }

            foreach (var union in module.Unions)
            {
                var (dataType, variantOfDataType) = LowerUnion(union.Signature.NotNull());

                _types.Add(dataType.Id, dataType);
                _types.Add(variantOfDataType.Id, variantOfDataType);

                LowerUnionMethods(union.Signature.NotNull());
            }

            foreach (var dataType in module.Classes.Select(x => LowerClass(x.Signature.NotNull())))
            {
                _types.Add(dataType.Id, dataType);
            }
        }


        var u64Signature = _modules[DefId.UInt64.ModuleId].Classes.First(x => x.Signature.NotNull().Id == DefId.UInt64).Signature.NotNull();
        var u64Type = TypeChecker.InstantiatedClass.Create(u64Signature, [], boxed: false, u64Type: null!);

        var unitSignature = _modules[DefId.Unit.ModuleId].Classes.Select(x => x.Signature.NotNull()).First(x => x.Id == DefId.Unit);
        var unit = TypeChecker.InstantiatedClass.Create(unitSignature, [], boxed: false, u64Type);

        var mainExpressions = new List<Expressions.IExpression>(_mainModule.Expressions);

        var mainSignature = new TypeChecker.FunctionSignature(
                DefId.Main(_mainModule.ModuleId),
                Token.Identifier("_Main", SourceSpan.Default),
                [],
                [],
                IsStatic: true,
                IsMutable: false,
                mainExpressions,
                ExternName: null,
                false,
                IsPublic: true,
                Attributes: [],
                SelfConstraints: [])
        {
            ReturnType = unit,
            OwnerType = null,
            LocalVariables = _mainModule.TopLevelLocalVariables,
            LocalFunctions = _mainModule.TopLevelLocalFunctions
        };

        if (generateTestMain)
        {
            var u32Signature = _modules[DefId.UInt32.ModuleId].Classes.First(
                x => x.Signature.NotNull().Id == DefId.UInt32).Signature.NotNull();
            var boolSignature = _modules[DefId.Boolean.ModuleId].Classes.First(
                x => x.Signature.NotNull().Id == DefId.Boolean).Signature.NotNull();
            var stringSignature = _modules[DefId.String.ModuleId].Classes.First(
                x => x.Signature.NotNull().Id == DefId.String).Signature.NotNull();
            var resultSignature = _modules[DefId.Result.ModuleId].Unions.First(
                x => x.Signature.NotNull().Id == DefId.Result).Signature.NotNull();

            var catchUnwindSignature = _modules[DefId.CatchUnwind.ModuleId].Functions.First(
                x => x.Signature.NotNull().Id == DefId.CatchUnwind).Signature.NotNull();

            var u32Type = TypeChecker.InstantiatedClass.Create(u32Signature, [], u32Signature.Boxed, u64Type);
            var boolType = TypeChecker.InstantiatedClass.Create(boolSignature, [], boolSignature.Boxed, u64Type);
            var printStringSignature = _modules[DefId.PrintString.ModuleId].Functions
                .First(x => x.Signature.NotNull().Id == DefId.PrintString).Signature.NotNull();
            var printu32Signature = _modules[DefId.PrintU32.ModuleId].Functions
                .First(x => x.Signature.NotNull().Id == DefId.PrintU32).Signature.NotNull();
            var functionObject1Signature = _modules[DefId.FunctionObject(1).ModuleId].Classes.First(x =>
                x.Signature.NotNull().Id == DefId.FunctionObject(1)).Signature.NotNull();
            var functionObject0Signature = _modules[DefId.FunctionObject(0).ModuleId].Classes.First(x =>
                x.Signature.NotNull().Id == DefId.FunctionObject(0)).Signature.NotNull();
            var unitType = TypeChecker.InstantiatedClass.Create(unitSignature, [], unitSignature.Boxed, u64Type);
            var stringType = TypeChecker.InstantiatedClass.Create(stringSignature, [], stringSignature.Boxed, u64Type);
            var resultUnitUnitType = TypeChecker.InstantiatedUnion.Create(
                resultSignature, [unitType, unitType], resultSignature.Boxed, u64Type);

            var functionObjectStringUnit = TypeChecker.InstantiatedClass.Create(
                functionObject1Signature,
                [stringType, unitType], functionObject1Signature.Boxed, u64Type);

            var functionObjectUnit = TypeChecker.InstantiatedClass.Create(
                functionObject0Signature,
                [unitType], functionObject0Signature.Boxed, u64Type);

            var functionObject_functionObjectUnit_resultUnitUnit = TypeChecker.InstantiatedClass.Create(
                functionObject1Signature,
                [functionObjectUnit, resultUnitUnitType], functionObject1Signature.Boxed, u64Type);

            var passedCountVariable = new TypeChecker.LocalVariable(
                mainSignature,
                Token.Identifier("passedCount", SourceSpan.Default),
                Type: u32Type,
                Instantiated: true,
                Mutable: true
            );
            var failedCountVariable = new TypeChecker.LocalVariable(
                mainSignature,
                Token.Identifier("failedCount", SourceSpan.Default),
                Type: u32Type,
                Instantiated: true,
                Mutable: true
            );

            mainSignature.LocalFunctions.Clear();
            mainSignature.LocalVariables.Clear();
            mainSignature.LocalVariables.AddRange(
                [
                    passedCountVariable, failedCountVariable
                ]
            );

            mainExpressions.Clear();

            Expressions.ValueAccessorExpression StringLiteralExpression(string value)
            {
                return new Expressions.ValueAccessorExpression(new Expressions.ValueAccessor(
                    Expressions.ValueAccessType.Literal,
                    Token.StringLiteral(value, SourceSpan.Default),
                    [],
                    [],
                    false
                ))
                {
                    ResolvedType = stringType,
                };
            }

            Expressions.ValueAccessorExpression U32LiteralExpression(uint value)
            {
                return new Expressions.ValueAccessorExpression(new Expressions.ValueAccessor(
                    Expressions.ValueAccessType.Literal,
                    Token.IntLiteral((int)value, SourceSpan.Default),
                    [],
                    [],
                    false
                ))
                {
                    ResolvedType = u32Type,
                };
            }

            Expressions.ValueAccessorExpression VariableAccessExpression(TypeChecker.LocalVariable variable)
            {
                return new Expressions.ValueAccessorExpression(new Expressions.ValueAccessor(
                    Expressions.ValueAccessType.Variable,
                    variable.Name,
                    [],
                    [],
                    false
                ))
                {
                    ReferencedVariable = variable,
                    ResolvedType = variable.Type
                };
            }

            var printStringMethodExpression = new Expressions.ValueAccessorExpression(
                                        new Expressions.ValueAccessor(
                                            Expressions.ValueAccessType.Variable,
                                            Token.Identifier("print_string", SourceSpan.Default),
                                            [], [], false
                                        ))
            {
                ResolvedType = new TypeChecker.FunctionObject(
                    [new TypeChecker.FunctionParameter(stringType, false)], unitType, false, true),
                // ResolvedType = functionObjectStringUnit,
                FunctionInstantiation = new TypeChecker.InstantiatedFunction(null, printStringSignature, [], u64Type)
            };

            var printU32MethodExpression = new Expressions.ValueAccessorExpression(
                                                    new Expressions.ValueAccessor(
                                                        Expressions.ValueAccessType.Variable,
                                                        Token.Identifier("print_u32", SourceSpan.Default),
                                                        [], [], false
                                                    ))
            {
                ResolvedType = new TypeChecker.FunctionObject(
                                [new TypeChecker.FunctionParameter(u32Type, false)], unitType, false, true),
                FunctionInstantiation = new TypeChecker.InstantiatedFunction(null, printu32Signature, [], u64Type)
            };

            var catch_unwind_instantiated_function = new TypeChecker.InstantiatedFunction(null, catchUnwindSignature, [], u64Type);
            catch_unwind_instantiated_function.TypeArguments[0].ResolvedType = unitType;

            var catchUnwindMethodExpression = new Expressions.ValueAccessorExpression(
                                        new Expressions.ValueAccessor(
                                            Expressions.ValueAccessType.Variable,
                                            Token.Identifier("catch_unwind", SourceSpan.Default),
                                            [], [], false
                                        ))
            {
                ResolvedType = new TypeChecker.FunctionObject(
                [new TypeChecker.FunctionParameter(
                    new TypeChecker.FunctionObject([], unitType, false, true), false)], resultUnitUnitType, false, true),
                FunctionInstantiation = catch_unwind_instantiated_function
            };

            var testMethods = _modules.Values.SelectMany(x =>
                x.Functions.Select(y => y.Signature.NotNull())
                    .Concat(x.Classes.SelectMany(y => y.Functions).Select(y => y.Signature.NotNull()))
                    .Concat(x.Unions.SelectMany(y => y.Functions).Select(y => y.Signature.NotNull())))
                .Where(x => x.Attributes.Any(y => y.AttributeId == DefId.Test))
                .ToArray();

            var invalidTestMethods = testMethods.Where(x => x.OwnerType is not null && !x.IsStatic).ToArray();

            if (invalidTestMethods.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Test methods cannot be instance functions, only top level or static functions can be test methods: [{string.Join(", ", invalidTestMethods.Select(x => x.Id.FullName))}]");
            }

            mainExpressions.AddRange([
                new Expressions.VariableDeclarationExpression(
                    new Expressions.VariableDeclaration(
                        passedCountVariable.Name,
                        new Expressions.MutabilityModifier(Token.Mut(SourceSpan.Default)),
                        null,
                        U32LiteralExpression(0)
                    ){Variable = passedCountVariable},
                    SourceRange.Default
                ) {
                    ResolvedType = unitType,
                },
                new Expressions.VariableDeclarationExpression(
                    new Expressions.VariableDeclaration(
                        failedCountVariable.Name,
                        new Expressions.MutabilityModifier(Token.Mut(SourceSpan.Default)),
                        null,
                        U32LiteralExpression(0)
                    ){Variable = failedCountVariable},
                    SourceRange.Default
                ) {
                    ResolvedType = unitType,
                },
                ..testMethods.SelectMany(testMethod => new Expressions.IExpression[]
                    {
                        new Expressions.MethodCallExpression(
                            new Expressions.MethodCall(
                                printStringMethodExpression,
                                [StringLiteralExpression($"{testMethod.Id.FullName} - ")]
                            ),
                            SourceRange.Default
                        )
                        {
                            ResolvedType = unitType
                        },
                        new Expressions.IfExpressionExpression(
                            new Expressions.IfExpression(
                                // catch_unwind(first_test) matches result::Error
                                CheckExpression: new Expressions.MatchesExpression(
                                    new Expressions.MethodCallExpression(
                                        new Expressions.MethodCall(
                                            catchUnwindMethodExpression,
                                            [
                                                new Expressions.ValueAccessorExpression(new Expressions.ValueAccessor(
                                                    Expressions.ValueAccessType.Variable,
                                                    testMethod.NameToken,
                                                    [],
                                                    [.. testMethod.Id.FullName.Split(":::", StringSplitOptions.RemoveEmptyEntries)
                                                        .Select(x => Token.Identifier(x, SourceSpan.Default))
                                                        // skip method name segment
                                                        .SkipLast(1)
                                                    ],
                                                    true
                                                )) {
                                                    FunctionInstantiation =
                                                        new TypeChecker.InstantiatedFunction(null, testMethod, [], u64Type),
                                                    ResolvedType = new TypeChecker.FunctionObject([], unitType, false, true)
                                                }
                                            ]
                                        ),
                                        SourceRange.Default
                                    ) {
                                        ResolvedType = resultUnitUnitType
                                    },
                                    new UnionVariantPattern(
                                        new NamedTypeIdentifier(
                                            Token.Identifier("result", SourceSpan.Default),
                                            [],
                                            null,
                                            [],
                                            false,
                                            SourceRange.Default
                                        ),
                                        Token.Identifier("Error", SourceSpan.Default),
                                        null,
                                        false,
                                        SourceRange.Default
                                    ) {
                                        IsRedundant = false,
                                        TypeReference = resultUnitUnitType
                                    },
                                    SourceRange.Default
                                ) {
                                    ResolvedType = boolType
                                },
                                Body: new Expressions.BlockExpression(
                                    new Expressions.Block(
                                        [
                                            new Expressions.BinaryOperatorExpression(new Expressions.BinaryOperator(
                                                Expressions.BinaryOperatorType.ValueAssignment,
                                                VariableAccessExpression(failedCountVariable),
                                                new Expressions.BinaryOperatorExpression(new Expressions.BinaryOperator(
                                                    Expressions.BinaryOperatorType.Plus,
                                                    VariableAccessExpression(failedCountVariable),
                                                    U32LiteralExpression(1),
                                                    Token.Plus(SourceSpan.Default)
                                                )) {
                                                    ResolvedType = u32Type
                                                },
                                                Token.Equals(SourceSpan.Default)
                                            )) {
                                                ResolvedType = unitType
                                            },
                                            new Expressions.MethodCallExpression(
                                                new Expressions.MethodCall(
                                                    printStringMethodExpression,
                                                    [StringLiteralExpression("Failed\n")]
                                                ),
                                                SourceRange.Default
                                            )
                                            {
                                                ResolvedType = unitType
                                            }
                                        ],
                                        [], []
                                    ),
                                    SourceRange.Default) {
                                        ResolvedType = unitType
                                    },
                                ElseIfs: [],
                                ElseBody: new Expressions.BlockExpression(
                                    new Expressions.Block(
                                        [
                                            new Expressions.BinaryOperatorExpression(new Expressions.BinaryOperator(
                                                Expressions.BinaryOperatorType.ValueAssignment,
                                                VariableAccessExpression(passedCountVariable),
                                                new Expressions.BinaryOperatorExpression(new Expressions.BinaryOperator(
                                                    Expressions.BinaryOperatorType.Plus,
                                                    VariableAccessExpression(passedCountVariable),
                                                    U32LiteralExpression(1),
                                                    Token.Plus(SourceSpan.Default)
                                                )) {
                                                    ResolvedType = u32Type
                                                },
                                                Token.Equals(SourceSpan.Default)
                                            )) {
                                                ResolvedType = unitType
                                            },
                                            new Expressions.MethodCallExpression(
                                                new Expressions.MethodCall(
                                                    printStringMethodExpression,
                                                    [StringLiteralExpression("Passed\n")]
                                                ),
                                                SourceRange.Default
                                            )
                                            {
                                                ResolvedType = unitType
                                            }
                                        ],
                                        [], []
                                    ),
                                    SourceRange.Default) {
                                        ResolvedType = unitType
                                    }
                            ),
                            SourceRange.Default
                        )
                }),
                new Expressions.MethodCallExpression(
                    new Expressions.MethodCall(
                        printStringMethodExpression,
                        [StringLiteralExpression("\nSummary:\nTotal: ")]
                    ),
                    SourceRange.Default
                )
                {
                    ResolvedType = unitType
                },
                new Expressions.MethodCallExpression(
                    new Expressions.MethodCall(
                        printU32MethodExpression,
                        [
                            new Expressions.BinaryOperatorExpression(new Expressions.BinaryOperator(
                                Expressions.BinaryOperatorType.Plus,
                                VariableAccessExpression(passedCountVariable),
                                VariableAccessExpression(failedCountVariable),
                                Token.Plus(SourceSpan.Default)
                            )) {
                                ResolvedType = u32Type
                            }
                        ]
                    ),
                    SourceRange.Default
                )
                {
                    ResolvedType = unitType
                },
                new Expressions.MethodCallExpression(
                    new Expressions.MethodCall(
                        printStringMethodExpression,
                        [StringLiteralExpression(", Passed: ")]
                    ),
                    SourceRange.Default
                )
                {
                    ResolvedType = unitType
                },
                new Expressions.MethodCallExpression(
                    new Expressions.MethodCall(
                        printU32MethodExpression,
                        [VariableAccessExpression(passedCountVariable)]
                    ),
                    SourceRange.Default
                )
                {
                    ResolvedType = unitType
                },
                new Expressions.MethodCallExpression(
                    new Expressions.MethodCall(
                        printStringMethodExpression,
                        [StringLiteralExpression(", Failed: ")]
                    ),
                    SourceRange.Default
                )
                {
                    ResolvedType = unitType
                },
                new Expressions.MethodCallExpression(
                    new Expressions.MethodCall(
                        printU32MethodExpression,
                        [VariableAccessExpression(failedCountVariable)]
                    ),
                    SourceRange.Default
                )
                {
                    ResolvedType = unitType
                },
            ]);
        }

        /*

                var mut passedCount = 0;
                var mut failedCount = 0;

                print_string("my:::test_id - ");
                if (catch_unwind(first_test) matches result::Error) {
                    failedCount = failedCount + 1;
                    print_string("Failed\n");
                }
                else {
                    passedCount = passedCount + 1;
                    print_string("Passed\n");
                }

                */


        foreach (var local in _mainModule.TopLevelLocalVariables)
        {
            local.ContainingFunction = mainSignature;
        }

        var fnSignaturesToGenerate = _modules.Values.SelectMany(x => x.Functions.Select(x => x.Signature.NotNull()))
            .Where(x => !intrinsicFunctionIds.Contains(x.Id));
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
                                new CreateObject(new LoweredConcreteTypeReference(DefId.ObjectHeader, []))
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

    private static (DataType unionDataType, DataType variantOfDataType) LowerUnion(TypeChecker.UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Id,
                                                    typeParameters);


        var variants = new List<DataTypeVariant>(union.Variants.Count);
        foreach (var variant in union.Variants)
        {
            var variantIdentifierField = new DataTypeField(
                    VariantIdentifierFieldName,
                    GetBuiltInTypeReference(DefId.UInt16));
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

        var variantOfDataType = new DataType(
            new DefId(union.Id.ModuleId, union.Id.FullName + "__VariantOf"),
            union.Name + "__VariantOf",
            TypeParameters: [],
            Variants: [.. variants.Select(x => new DataTypeVariant(x.Name, [.. x.Fields.Where(y => y.Name == VariantIdentifierFieldName)]))],
            StaticFields: []);

        var dataType = new DataType(
            union.Id,
            union.Name,
            typeParameters,
            Variants: variants,
            StaticFields: []);

        return (dataType, variantOfDataType);
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
                        new CreateObject(new LoweredConcreteTypeReference(DefId.ObjectHeader, []))),
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
            TypeChecker.InstantiatedFunction fn)
    {
        var functionOwnerTypeArguments = fn.OwnerType switch
        {
            TypeChecker.InstantiatedClass classOwner => classOwner.TypeArguments,
            TypeChecker.InstantiatedUnion unionOwner => unionOwner.TypeArguments,
            _ => []
        };

        return GetFunctionReference(
            fn.FunctionId,
            [.. fn.TypeArguments.Select(GetTypeReference)],
            [.. functionOwnerTypeArguments.Select(GetTypeReference)]);
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
        return new LoweredConcreteTypeReference(
            DefId.BoxedValue, [valueType]);
    }

    private static LoweredConcreteTypeReference GetBuiltInTypeReference(DefId id)
    {
        return new LoweredConcreteTypeReference(id, []);
    }

    private static ILoweredTypeReference GetTypeReference(TypeChecker.ITypeReference typeReference)
    {
        var loweredTypeReference = typeReference switch
        {
            TypeChecker.InstantiatedClass c => new LoweredConcreteTypeReference(
                c.Signature.Id,
                [.. c.TypeArguments.Select(GetTypeReference)]),
            TypeChecker.InstantiatedUnion u => new LoweredConcreteTypeReference(
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
            TypeChecker.VariantOfType v => VariantOfTypeCase(v),
            TypeChecker.ArrayType a => new LoweredArray(
                GetTypeReference(a.ElementType), a.Length),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };

        if (IsTypeReferenceBoxed(typeReference) && loweredTypeReference is not LoweredPointer)
        {
            loweredTypeReference = new LoweredPointer(BoxedValueType(loweredTypeReference));
        }

        return loweredTypeReference;

        static LoweredConcreteTypeReference VariantOfTypeCase(TypeChecker.VariantOfType v)
        {
            return new LoweredConcreteTypeReference(
                new DefId(
                    v.Union.Signature.Id.ModuleId,
                    v.Union.Signature.Id.FullName + "__VariantOf"),
                []);
        }

        static LoweredConcreteTypeReference FunctionObjectCase(TypeChecker.FunctionObject f)
        {
            return new LoweredConcreteTypeReference(
                DefId.FunctionObject(f.Parameters.Count),
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
