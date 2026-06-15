using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class SimpleExpressionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact]
    public async Task SingleTest()
    {
        var source = """
                        class MyClass
                        {
                            pub fn to_string(): string {
                                return "MyClass";
                            }
                        }
                        var a = (new MyClass{}, new MyClass{});
                        var b = a.Item0.to_string();
                        var c = a.Item1.to_string();
                        """;
        var expectedProgram = LoweredProgram(ModuleId,
            types: [
                DataType(
                                    ModuleId,
                                    "MyClass",
                                    variants: [Variant("_classVariant")])],
            methods: [
                Method(
                                    new DefId(ModuleId, $"{ModuleId}:::MyClass__to_string"),
                                    "MyClass__to_string",
                                    [
                                        new BasicBlock(
                                            BB0,
                                            [new Assign(new Local("_returnValue"), new Use(new StringConstant("MyClass")))],
                                            new Return()
                                        ),
                                    ],
                                    returnType: StringT,
                                    parameters: [
                                        (
                                            "this",
                                            new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId)))
                                        )
                                    ]),
                                Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
                                    [
                                        new BasicBlock(
                                            BB0,
                                            [
                                                new Assign(
                                                    Local0,
                                                    new CreateObject(
                                                        Tuple(
                                                            new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))),
                                                            new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))))
                                            ],
                                            AllocateMethodCall(
                                                BoxedValue(ConcreteTypeReference("MyClass", ModuleId)),
                                                new Field(Local0, "Item0", "_classVariant"),
                                                BB1)
                                        ),
                                        new BasicBlock(
                                            BB1,
                                            [
                                                ..CreateBoxedObject(
                                                    new Deref(new Field(Local0, "Item0", "_classVariant")),
                                                    ConcreteTypeReference("MyClass", ModuleId))
                                            ],
                                            AllocateMethodCall(
                                                BoxedValue(ConcreteTypeReference("MyClass", ModuleId)),
                                                new Field(Local0, "Item1", "_classVariant"),
                                                BB2)),
                                        new BasicBlock(
                                            BB2,
                                            [
                                                ..CreateBoxedObject(
                                                    new Deref(new Field(Local0, "Item1", "_classVariant")),
                                                    ConcreteTypeReference("MyClass", ModuleId))
                                            ],
                                            new MethodCall(
                                                new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__to_string"), []),
                                                [new Copy(new Field(Local0, "Item0", "_classVariant"))],
                                                Local1,
                                                BB3)),
                                        new BasicBlock(
                                            BB3,
                                            [],
                                            new MethodCall(
                                                new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__to_string"), []),
                                                [new Copy(new Field(Local0, "Item1", "_classVariant"))],
                                                Local2,
                                                BB4)),
                                        new BasicBlock(BB4, [], new Return())
                                    ],
                                    Unit,
                                    locals: [
                                        new MethodLocal(
                                            "_local0",
                                            "a",
                                            Tuple(
                                                new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))),
                                                new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))),
                                        new MethodLocal("_local1", "b", StringT),
                                        new MethodLocal("_local2", "c", StringT),
                                    ])
            ]);

        var program = await CreateProgram(ModuleId, source);
        var loweredProgram = Lower(program, ModuleId);

        TestOutput.WriteLine(source);
        TestOutput.WriteLine("=====================");
        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task SimpleExpressionAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();

        TestOutput.WriteLine(source);

        var program = await CreateProgram(ModuleId, source);
        var loweredProgram = Lower(program, ModuleId);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private static readonly ModuleId ModuleId = new("main");

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "grab value",
                """
                var a = {
                    print_string("hi");
                    grab true;
                };

                if ({grab false;}) {
                    print_string("bye");
                }

                """,
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::_Main"),
                        "_Main",
                        [
                            new BasicBlock(
                                BB0,
                                [],
                                new MethodCall(
                                    new LoweredFunctionReference(DefId.PrintString, []),
                                    [new StringConstant("hi")],
                                    Local1,
                                    BB1)
                            ),
                            new BasicBlock(
                                BB1,
                                [new Assign(Local0, new Use(new BoolConstant(true)))],
                                new SwitchInt(new BoolConstant(false), new(){ { 0, BB3 } }, BB2)
                            ),
                            new BasicBlock(
                                BB2,
                                [],
                                new MethodCall(
                                    new LoweredFunctionReference(DefId.PrintString, []),
                                    [new StringConstant("bye")],
                                    Local2,
                                    BB3)),
                            new BasicBlock(BB3, [], new Return())
                        ],
                        Unit,
                        locals: [
                            new MethodLocal("_local0", "a", BooleanT),
                            new MethodLocal("_local1", null, Unit),
                            new MethodLocal("_local2", null, Unit),
                        ])
                ])
            },
            {
                "negate value",
                """
                var a = 1;
                var b = -a;
                """,
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::_Main"),
                        "_Main",
                        [
                        new BasicBlock(
                            BB0,
                            [
                                new Assign(
                                    new Local("_local0"), new Use(new IntConstant(1, 4))),
                                new Assign(
                                    new Local("_local1"), new UnaryOperation(
                                        new Copy(new Local("_local0")),
                                        UnaryOperationKind.Negate))
                            ],
                            new GoTo(BB1)),
                        new BasicBlock(
                            BB1,
                            [],
                            new Return()),
                        ],
                        Unit,
                        locals: [
                            new MethodLocal("_local0", "a", Int32T),
                            new MethodLocal("_local1", "b", Int32T),
                        ])
                ])
            },
            {
                "box number",
                "var a: boxed i32 = box(1);",
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::_Main"),
                        "_Main",
                        [
                            new BasicBlock(
                                BB0,
                                [],
                                new MethodCall(
                                    new LoweredFunctionReference(
                                        DefId.Box, [Int32T, new LoweredPointer(BoxedValue(Int32T))]),
                                    [new IntConstant(1, 4)],
                                    new Local("_local0"),
                                    BB1)),
                            new BasicBlock(BB1, [], new Return()),
                        ],
                        Unit,
                        locals: [
                            new MethodLocal(
                            "_local0",
                            "a",
                            new LoweredPointer(BoxedValue(Int32T)))
                        ])
                ])
            },
            {
                "unbox number parameter",
                "fn SomeFn(a: boxed i32){var b = unbox(a);}",
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::SomeFn"),
                        "SomeFn",
                        [
                            new BasicBlock(
                                BB0,
                                [],
                                new MethodCall(
                                    new LoweredFunctionReference(
                                        DefId.Unbox, [new LoweredPointer(BoxedValue(Int32T)), Int32T]),
                                    [new Copy(new Local("_param0"))],
                                    new Local("_local0"),
                                    BB1)),
                            new BasicBlock(BB1, [], new Return()),
                        ],
                        Unit,
                        parameters: [
                            ("a", new LoweredPointer(BoxedValue(Int32T)))
                        ],
                        locals: [
                            new MethodLocal(
                                "_local0",
                                "b",
                                Int32T),
                        ])
                ])
            },
            {
                "unbox number",
                "var a: i32 = unbox(box(1));",
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::_Main"),
                        "_Main",
                        [
                            new BasicBlock(
                                BB0,
                                [],
                                new MethodCall(
                                    new LoweredFunctionReference(
                                        DefId.Box, [Int32T, new LoweredPointer(BoxedValue(Int32T))]),
                                    [new IntConstant(1, 4)],
                                    new Local("_local1"),
                                    BB1)),
                            new BasicBlock(
                                BB1,
                                [],
                                new MethodCall(
                                    new LoweredFunctionReference(
                                        DefId.Unbox, [new LoweredPointer(BoxedValue(Int32T)), Int32T]),
                                    [new Copy(new Local("_local1"))],
                                    new Local("_local0"),
                                    BB2
                                )
                            ),
                            new BasicBlock(BB2, [], new Return()),
                        ],
                        Unit,
                        locals: [
                            new MethodLocal(
                                "_local0",
                                "a",
                                Int32T),
                            new MethodLocal(
                                "_local1",
                                null,
                                new LoweredPointer(BoxedValue(Int32T))),
                        ])
                ])
            },
            {
                "variable declaration",
                "var a = \"\";",
                LoweredProgram(ModuleId, methods: [
                    Method(
                        new DefId(ModuleId, $"{ModuleId}:::_Main"),
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
                        locals: [new MethodLocal("_local0", "a", StringT)])
                ])
            },
            {
                "local assignment",
                "var a;a = 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::_Main"),
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
                            locals: [new MethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int plus",
                "var a = 1 + 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int minus",
                "var a = 1 - 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int divide",
                "var a = 1 / 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int multiply",
                "var a = 1 * 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "int not equals",
                "var a = 1 != 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "int equals",
                "var a = 1 == 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "int greater than",
                "var a = 1 > 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "int less than",
                "var a = 1 < 2;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "bool or",
                "var a = false || false;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "bool and",
                "var a = false && false;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "bool not",
                "var a = !true;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                                new MethodLocal("_local0", "a", BooleanT)
                            ])
                    ])
            },
            {
                "chain operations",
                "var a = 1 + 2 + 3;",
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            new MethodLocal("_local0", "a", Int32T),
                            new MethodLocal("_local1", null, Int32T),
                        ])
                ])
            },
            {
                "dead expression",
                "1;",
                LoweredProgram(ModuleId, methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", BooleanT)])
                    ])
            },
            {
                "block with multiple expressions",
                "{var a = true; var b = 1;}",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                                new MethodLocal("_local0", "a", BooleanT),
                                new MethodLocal("_local1", "b", Int32T),
                            ])
                    ])
            },
            {
                "local access",
                "var a = 1; var b = a;",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                                new MethodLocal("_local0", "a", Int32T),
                                new MethodLocal("_local1", "b", Int32T),
                            ])
                    ])
            },
            {
                "method call",
                "fn MyFn(){} MyFn();",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyFn"),
                            "MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::_Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyFn"), []),
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
                                new MethodLocal("_local0", null, Unit)
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
                LoweredProgram(ModuleId,
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyFn"),
                            "MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), []) { Terminator = new Return()}],
                            Unit,
                            typeParameters: [(new DefId(ModuleId, $"{ModuleId}:::MyFn"), "T")]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::_Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyFn"), [StringT]),
                                        [],
                                        new Local("_local0"),
                                        new BasicBlockId("bb1"))
                                },
                                new BasicBlock(new BasicBlockId("bb1"), [])
                                {
                                    Terminator = new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyFn"), [Int64T]),
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
                                new MethodLocal("_local0", null, Unit),
                                new MethodLocal("_local1", null, Unit),
                            ])
                    ])
            },
            {
                "function parameter access",
                "fn MyFn(a: string): string { return a; }",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyFn"),
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
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
                            locals: [new MethodLocal("_local0", "a", Int32T)])
                    ])
            },
            {
                "two element tuple",
                """var a = (1, "");""",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(Tuple(Int32T, StringT))),
                                    new Assign(
                                        new Field(new Local("_local0"), "Item0", "_classVariant"),
                                        new Use(new IntConstant(1, 4))),
                                    new Assign(
                                        new Field(new Local("_local0"), "Item1", "_classVariant"),
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
                                new MethodLocal("_local0", "a", Tuple(Int32T, StringT))
                            ])
                    ])
            },
            {
                "two element tuple",
                """
                class MyClass
                {
                    pub fn to_string(): string {
                        return "MyClass";
                    }
                }
                var a = (new MyClass{}, new MyClass{});
                var b = a.Item0.to_string();
                var c = a.Item1.to_string();
                """,
                LoweredProgram(ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")])],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__to_string"),
                            "MyClass__to_string",
                            [
                                new BasicBlock(
                                    BB0,
                                    [new Assign(new Local("_returnValue"), new Use(new StringConstant("MyClass")))],
                                    new Return()
                                ),
                            ],
                            returnType: StringT,
                            parameters: [
                                (
                                    "this",
                                    new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId)))
                                )
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new CreateObject(
                                                Tuple(
                                                    new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))),
                                                    new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))))
                                    ],
                                    AllocateMethodCall(
                                        BoxedValue(ConcreteTypeReference("MyClass", ModuleId)),
                                        new Field(Local0, "Item0", "_classVariant"),
                                        BB1)
                                ),
                                new BasicBlock(
                                    BB1,
                                    [
                                        ..CreateBoxedObject(
                                            new Deref(new Field(Local0, "Item0", "_classVariant")),
                                            ConcreteTypeReference("MyClass", ModuleId))
                                    ],
                                    AllocateMethodCall(
                                        BoxedValue(ConcreteTypeReference("MyClass", ModuleId)),
                                        new Field(Local0, "Item1", "_classVariant"),
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        ..CreateBoxedObject(
                                            new Deref(new Field(Local0, "Item1", "_classVariant")),
                                            ConcreteTypeReference("MyClass", ModuleId))
                                    ],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__to_string"), []),
                                        [new Copy(new Field(Local0, "Item0", "_classVariant"))],
                                        Local1,
                                        BB3)),
                                new BasicBlock(
                                    BB3,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__to_string"), []),
                                        [new Copy(new Field(Local0, "Item1", "_classVariant"))],
                                        Local2,
                                        BB4)),
                                new BasicBlock(BB4, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    Tuple(
                                        new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))),
                                        new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))),
                                new MethodLocal("_local1", "b", StringT),
                                new MethodLocal("_local2", "c", StringT),
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
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main__SomeFn"), "_Main__SomeFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            Unit),
                        Method(new DefId(ModuleId, $"{ModuleId}:::_Main"), "_Main",
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
