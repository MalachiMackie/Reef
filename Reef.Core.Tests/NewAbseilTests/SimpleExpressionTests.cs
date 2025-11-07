using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;


public class SimpleExpressionTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{

    [Fact]
    public void SingleTest()
    {
        var source = "fn MyFn(a: string): string { return a; }";
        var expectedProgram = NewLoweredProgram(
            methods:
            [
                NewMethod(
                    new DefId(ModuleId, $"{ModuleId}.MyFn"),
                    "MyFn",
                    [
                        new BasicBlock(new BasicBlockId("bb0"), [
                            new Assign(new Local("_returnValue"), new Use(new Copy(new Local("_param0"))))
                        ])
                        {
                            Terminator = new Return()
                        }
                    ],
                    StringT,
                    parameters: [("a", StringT)])
            ]);
        
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
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
                "var a = 1 + 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int minus",
                "var a = 1 - 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int divide",
                "var a = 1 / 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int multiply",
                "var a = 1 * 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int not equals",
                "var a = 1 != 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "int equals",
                "var a = 1 == 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "int greater than",
                "var a = 1 > 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "int less than",
                "var a = 1 < 2;",
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
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "bool or",
                "var a = false || false",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [])
                                {
                                    Terminator = new SwitchInt(
                                        new BoolConstant(false),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb1") }
                                        },
                                        new BasicBlockId("bb2"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new BoolConstant(false)))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb3"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb2"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new BoolConstant(true)))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb3"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb3"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "bool and",
                "var a = false && false",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [])
                                {
                                    Terminator = new SwitchInt(
                                        new BoolConstant(false),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb2") }
                                        },
                                        new BasicBlockId("bb1"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new BoolConstant(false)))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb3"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb2"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new BoolConstant(false)))
                                    ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb3"))
                                },
                                new BasicBlock(
                                    new BasicBlockId("bb3"),
                                    [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "bool not",
                "var a = !true",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"),
                                    [
                                        new Assign(new Local("_local0"), new UnaryOperation(new BoolConstant(true), UnaryOperationKind.Not))
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
                            locals: [
                                new NewMethodLocal("_local0", "a", BooleanT)
                            ])
                    ])
            },
            {
                "chain operations",
                "var a = 1 + 2 + 3",
                NewLoweredProgram(methods: [
                    NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                        [
                            new BasicBlock(new BasicBlockId("bb0"), [
                                new Assign(
                                    new Local("_local1"),
                                    new BinaryOperation(new IntConstant(1, 4), new IntConstant(2, 4), BinaryOperationKind.Add)),
                                new Assign(
                                    new Local("_local0"),
                                    new BinaryOperation(new Copy(new Local("_local1")), new IntConstant(3, 4), BinaryOperationKind.Add)),
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
                        locals: [
                            new NewMethodLocal("_local0", "a", Int32T),
                            new NewMethodLocal("_local1", null, Int32T),
                        ])
                ])
            },
            {
                "dead expression",
                "1",
                NewLoweredProgram(methods: [
                    NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                        [
                            new BasicBlock(new BasicBlockId("bb0"), [])
                            {
                                Terminator = new Return()
                            }
                        ],
                        Unit)
                ])
            },
            {
                "empty block",
                "{}",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit)
                    ])
            },
            {
                "block with one expression",
                "{var a = true;}",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(new Local("_local0"), new Use(new BoolConstant(true)))
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
                            locals: [new NewMethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "block with multiple expressions",
                "{var a = true; var b = 1;}",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(new Local("_local0"), new Use(new BoolConstant(true))),
                                    new Assign(new Local("_local1"), new Use(new IntConstant(1, 4))),
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
                            locals: [
                                new NewMethodLocal("_local0", "a", BooleanT),
                                new NewMethodLocal("_local1", "b", Int32T),
                            ])
                    ])
            },
            {
                "local access",
                "var a = 1; var b = a;",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new IntConstant(1, 4))),
                                        new Assign(new Local("_local1"), new Use(new Copy(new Local("_local0"))))
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
                            locals: [
                                new NewMethodLocal("_local0", "a", Int32T),
                                new NewMethodLocal("_local1", "b", Int32T),
                            ])
                    ])
            },
            {
                "method call",
                "fn MyFn(){} MyFn();",
                NewLoweredProgram(
                    methods: [
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}.MyFn"),
                            "MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit),
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new MethodCall(
                                        new NewLoweredFunctionReference("MyFn", new DefId(ModuleId, $"{ModuleId}.MyFn"), []),
                                        [],
                                        new Local("_local0"),
                                        new BasicBlockId("bb1"))
                                },
                                new BasicBlock(new BasicBlockId("bb1"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", null, Unit)
                            ])
                    ])
            },
            {
                "generic method call",
                """
                fn MyFn<T>(){}
                MyFn::<string>();
                MyFn::<i64>();
                """,
                NewLoweredProgram(
                    methods: [
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}.MyFn"),
                            "MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), []) { Terminator = new Return()}],
                            Unit,
                            typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyFn"), "T")]),
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new MethodCall(
                                        new NewLoweredFunctionReference("MyFn", new DefId(ModuleId, $"{ModuleId}.MyFn"), [StringT]),
                                        [],
                                        new Local("_local0"),
                                        new BasicBlockId("bb1"))
                                },
                                new BasicBlock(new BasicBlockId("bb1"), [])
                                {
                                    Terminator = new MethodCall(
                                        new NewLoweredFunctionReference("MyFn", new DefId(ModuleId, $"{ModuleId}.MyFn"), [Int64T]),
                                        [],
                                        new Local("_local1"),
                                        new BasicBlockId("bb2"))
                                },
                                new BasicBlock(new BasicBlockId("bb2"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", null, Unit),
                                new NewMethodLocal("_local1", null, Unit),
                            ])
                    ])
            },
            {
                "function parameter access",
                "fn MyFn(a: string): string { return a; }",
                NewLoweredProgram(
                    methods: [
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}.MyFn"),
                            "MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(new Local("_returnValue"), new Use(new Copy(new Local("_param0"))))
                                ])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            StringT,
                            parameters: [("a", StringT)])
                    ])
            },
            {
                "single element tuple",
                "var a = (1);",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new Use(new IntConstant(1, 4)))
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
                "two element tuple",
                """var a = (1, "");""",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(Tuple(Int32T, StringT))),
                                    new Assign(
                                        new Field("_local0", "Item0", "_classVariant"),
                                        new Use(new IntConstant(1, 4))),
                                    new Assign(
                                        new Field("_local0", "Item1", "_classVariant"),
                                        new Use(new StringConstant(""))),
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
                            locals: [
                                new NewMethodLocal("_local0", "a", Tuple(Int32T, StringT))
                            ])
                    ])
            },
            {
                "local function in block",
                """
                {
                    fn SomeFn(){}
                }
                """,
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main__SomeFn"), "_Main__SomeFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit)
                    ])
            },
        };
    }
}
