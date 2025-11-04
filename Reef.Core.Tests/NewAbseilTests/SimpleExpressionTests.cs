using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;


public class SimpleExpressionTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{

    [Theory]
    [MemberData(nameof(TestCases))]
    public void SimpleExpressionAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "SimpleExpressionTests";

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
    {
        return new()
        {
            {
                "variable declaration",
                "var a = \"\";",
                NewLoweredProgram(methods: [
                    NewMethod(
                        new DefId(ModuleId, $"{ModuleId}._Main"),
                        "_Main",
                        [
                            new BasicBlock(
                                new BasicBlockId("bb0"),
                                [
                                    new Assign(new Local("_local0"), new Use(new StringConstant("")))
                                ])
                            {
                                Terminator = new GoTo(new BasicBlockId("bb1"))
                            },
                            new BasicBlock(
                                new BasicBlockId("bb1"),
                                [])
                            {
                                Terminator = new Return()
                            }
                        ],
                        Unit,
                        locals: [new NewMethodLocal("_local0", "a", StringT)])
                ])
            },
            {
                "local assignment",
                "var a;a = 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(new BasicBlockId("bb1"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int plus",
                "1 + 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.Add))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, Int32T)])
                    ])
            },
            {
                "int minus",
                "1 - 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.Subtract))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, Int32T)])
                    ])
            },
            {
                "int divide",
                "1 / 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.Divide))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, Int32T)])
                    ])
            },
            {
                "int multiply",
                "1 * 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.Multiply))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, Int32T)])
                    ])
            },
            {
                "int not equals",
                "1 != 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.NotEqual))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, BooleanT)])
                    ])
            },
            {
                "int equals",
                "1 == 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.Equal))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, BooleanT)])
                    ])
            },
            {
                "int greater than",
                "1 > 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.GreaterThan))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, BooleanT)])
                    ])
            },
            {
                "int less than",
                "1 < 2;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new BinaryOperation(
                                                new IntConstant(1, 4),
                                                new IntConstant(2, 4),
                                                BinaryOperationKind.LessThan))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, BooleanT)])
                    ])
            }
            // {
            //     "bool or",
            //     "true || true",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     BoolOr(BoolConstant(true, true), BoolConstant(true, true), false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "bool and",
            //     "true && true",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     BoolAnd(BoolConstant(true, true), BoolConstant(true, true), false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "bool not",
            //     "!true",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     BoolNot(BoolConstant(true, true), false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "empty block",
            //     "{}",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     Block([], Unit, false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "block with one expression",
            //     "{true;}",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     Block([BoolConstant(true, false)], Unit, false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "block with multiple expressions",
            //     "{true; 1;}",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     Block([
            //                         BoolConstant(true, false),
            //                         Int32Constant(1, false),
            //                     ], Unit, false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "local access",
            //     "var a = 1; var b = a;",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration("a", Int32Constant(1, true), valueUseful: false),
            //                     VariableDeclaration("b", LocalAccess("a", true, Int32_t), valueUseful: false),
            //                     MethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     Local("a", Int32_t),
            //                     Local("b", Int32_t),
            //                 ])
            //         ])
            // },
            // {
            //     "method call",
            //     "fn MyFn(){} MyFn();",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [MethodReturnUnit()]),
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main", [
            //                 MethodCall(FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn"), [], false, Unit),
            //                 MethodReturnUnit()
            //             ])
            //         ])
            // },
            // {
            //     "generic method call",
            //     """
            //     fn MyFn<T>(){}
            //     MyFn::<string>();
            //     MyFn::<i64>();
            //     """,
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [MethodReturnUnit()], typeParameters: [(new DefId(_moduleId, $"{_moduleId}.MyFn"), "T")]),
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     MethodCall(
            //                         FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [StringType]),
            //                         [],
            //                         false,
            //                         Unit),
            //                     MethodCall(
            //                         FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [Int64_t]),
            //                         [],
            //                         false,
            //                         Unit),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "function parameter access",
            //     "fn MyFn(a: string): string { return a; }",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn",
            //                 [
            //                     MethodReturn(
            //                         LoadArgument(0, true, StringType))
            //                 ],
            //                 parameters: [StringType],
            //                 returnType: StringType)
            //         ])
            // },
            // {
            //     "single element tuple",
            //     "(1)",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     Int32Constant(1, false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "two element tuple",
            //     "(1, \"\")",
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     CreateObject(
            //                         ConcreteTypeReference("Tuple`2", DefId.Tuple(2), [Int32_t, StringType]),
            //                         "_classVariant",
            //                         false,
            //                         new()
            //                         {
            //                             {"Item0", Int32Constant(1, true)},
            //                             {"Item1", StringConstant("", true)},
            //                         }),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "local function in block",
            //     """
            //     {
            //         fn SomeFn(){}
            //     }
            //     """,
            //     LoweredProgram(
            //         methods: [
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main__SomeFn"), "_Main__SomeFn",
            //                 [MethodReturnUnit()]),
            //             Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
            //                 [
            //                     Block([], Unit, false),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
        };
    }
}
