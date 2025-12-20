using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests.PatternMatching;

public class MatchesTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchesAbseilTest(string description, string source, NewLoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "MatchesTests";

    public static TheoryData<string, string, NewLoweredModule> TestCases()
    {
        return new()
        {
            {
                "matches - discard pattern",
                """
                var b = 1 matches _;
                """,
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - variable declaration pattern",
                """
                var b = 1 matches var a;
                """,
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(new Local("_local0"), new Use(new IntConstant(1, 4))),
                                        new Assign(new Local("_local1"), new Use(new BoolConstant(true))),
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", Int32T),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "pattern variable used in closure",
                """
                var b = 1 matches var a;
                
                fn SomeFn() {
                    var c = a;
                }
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(
                            ModuleId,
                            "_Main__Locals",
                            variants: [
                                NewVariant("_classVariant",
                                    [
                                        NewField("a", Int32T) 
                                    ])
                            ]),
                        NewDataType(
                            ModuleId,
                            "SomeFn__Closure",
                            variants: [
                                NewVariant(
                                    "_classVariant",
                                    [
                                        NewField(
                                            "_Main__Locals",
                                            new NewLoweredConcreteTypeReference(
                                                "_Main__Locals",
                                                new DefId(ModuleId, $"{ModuleId}._Main__Locals"),
                                                []))
                                    ])
                            ])
                    ],
                    methods: [
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}.SomeFn"),
                            "SomeFn",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new Use(new Copy(
                                                new Field(
                                                    new Field(
                                                        new Local("_param0"),
                                                        "_Main__Locals",
                                                        "_classVariant"),
                                                    "a",
                                                    "_classVariant"))))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "c", Int32T)
                            ],
                            parameters: [
                                (
                                    "closure",
                                    new NewLoweredConcreteTypeReference(
                                        "SomeFn__Closure",
                                        new DefId(ModuleId, $"{ModuleId}.SomeFn__Closure"),
                                        []))
                            ]),
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_localsObject"),
                                            new CreateObject(new NewLoweredConcreteTypeReference(
                                                "_Main__Locals",
                                                new DefId(ModuleId, $"{ModuleId}._Main__Locals"),
                                                []))),
                                        new Assign(
                                            new Field(
                                                new Local("_localsObject"),
                                                "a",
                                                "_classVariant"),
                                            new Use(new IntConstant(1, 4))),
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal(
                                    "_localsObject",
                                    null,
                                    new NewLoweredConcreteTypeReference(
                                        "_Main__Locals",
                                        new DefId(ModuleId, $"{ModuleId}._Main__Locals"),
                                        [])),
                                new NewMethodLocal("_local1", "b", BooleanT)
                            ])
                    ])
            },
            {
                "matches - type pattern",
                """
                var b = 1 matches i64;
                """,
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - type pattern with variable declaration",
                """
                var b = 1 matches i64 var a;
                """,
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new Use(new IntConstant(1, 8))),
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", Int64T),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union variant pattern",
                """
                union MyUnion{A, B};
                var a = MyUnion::A;
                var b = a matches MyUnion::B;
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Local("_local1"),
                                            new BinaryOperation(
                                                new Copy(new Field(
                                                    new Local("_local0"),
                                                    "_variantIdentifier",
                                                    "B")),
                                                new UIntConstant(1, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal(
                                    "_local0",
                                    "a",
                                    new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union variant pattern with variable declaration",
                """
                union MyUnion{A, B};
                var a = MyUnion::A;
                var b = a matches MyUnion::B var c;
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new Copy(new Local("_local0")))),
                                        new Assign(
                                            new Local("_local2"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "B")),
                                                new UIntConstant(1, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "c", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local2", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union tuple pattern",
                """
                union MyUnion{A(i64), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_);
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T), NewField("Item0", Int64T)]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_returnValue"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item0", "A"),
                                            new Use(new Copy(new Local("_param0"))))
                                    ],
                                    new Return())
                            ],
                            parameters: [("Item0", Int64T)],
                            returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Local("_local1"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local1")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            {0, new BasicBlockId("bb2")}
                                        },
                                        new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(new Local("_local1"), new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb2"))),
                                new BasicBlock(new BasicBlockId("bb2"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with variable declaration",
                """
                union MyUnion{A(i64), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_) var c;
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T), NewField("Item0", Int64T)]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_returnValue"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item0", "A"),
                                            new Use(new Copy(new Local("_param0"))))
                                    ],
                                    new Return())
                            ],
                            parameters: [("Item0", Int64T)],
                            returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new Copy(new Local("_local0")))),
                                        new Assign(
                                            new Local("_local2"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local2")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb2") }
                                        },
                                        new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(new Local("_local2"), new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb2"))),
                                new BasicBlock(new BasicBlockId("bb2"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "c", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local2", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with multiple members ",
                """
                union MyUnion{A(i64, string, bool), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_, var c, _);
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant(
                                    "A",
                                    [
                                        NewField("_variantIdentifier", UInt16T),
                                        NewField("Item0", Int64T),
                                        NewField("Item1", StringT),
                                        NewField("Item2", BooleanT),
                                    ]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_returnValue"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item0", "A"),
                                            new Use(new Copy(new Local("_param0")))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item1", "A"),
                                            new Use(new Copy(new Local("_param1")))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item2", "A"),
                                            new Use(new Copy(new Local("_param2")))),
                                    ],
                                    new Return())
                            ],
                            parameters: [("Item0", Int64T), ("Item1", StringT), ("Item2", BooleanT)],
                            returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Local("_local2"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local2")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb4") }
                                        },
                                        new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local2")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb4") }
                                        },
                                        new BasicBlockId("bb2"))),
                                new BasicBlock(
                                    new BasicBlockId("bb2"),
                                    [
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new Copy(new Field(new Local("_local0"), "Item1", "A")))),
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local2")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb4") }
                                        },
                                        new BasicBlockId("bb3"))),
                                new BasicBlock(
                                    new BasicBlockId("bb3"),
                                    [
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb4"))),
                                new BasicBlock(new BasicBlockId("bb4"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "c", StringT),
                                new NewMethodLocal("_local2", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union class variant pattern",
                """
                union MyUnion{
                    A {field FieldA: i64},
                    B
                }
                var a = MyUnion::B;
                var b = a matches MyUnion::A{FieldA: _};
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant(
                                    "A",
                                    [
                                        NewField("_variantIdentifier", UInt16T),
                                        NewField("FieldA", Int64T)
                                    ]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Local("_local1"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal)),
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local1")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb2")}
                                        },
                                        new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(new Local("_local1"), new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb2"))),
                                new BasicBlock(new BasicBlockId("bb2"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union class variant pattern with discarded fields",
                """
                union MyUnion{
                    A {field FieldA: i64},
                    B
                }
                var a = MyUnion::B;
                var b = a matches MyUnion::A{_};
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant(
                                    "A",
                                    [
                                        NewField("_variantIdentifier", UInt16T),
                                        NewField("FieldA", Int64T)
                                    ]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Local("_local1"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union class variant pattern multiple fields",
                """
                union MyUnion{
                    A {field FieldA: i64, field FieldB: string, field FieldC: bool},
                    B
                }
                var a = MyUnion::B;
                var b = a matches MyUnion::A{FieldA: var c, FieldB, FieldC: _};
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant(
                                    "A",
                                    [
                                        NewField("_variantIdentifier", UInt16T),
                                        NewField("FieldA", Int64T),
                                        NewField("FieldB", StringT),
                                        NewField("FieldC", BooleanT)
                                    ]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Local("_local3"),
                                            new BinaryOperation(
                                                new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local3")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb4")}
                                        },
                                        new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new Copy(
                                                new Field(new Local("_local0"), "FieldA", "A")))),
                                        new Assign(
                                            new Local("_local3"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local3")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb4") }
                                        },
                                        new BasicBlockId("bb2"))),
                                new BasicBlock(
                                    new BasicBlockId("bb2"),
                                    [
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new Copy(
                                                new Field(new Local("_local0"), "FieldB", "A")))),
                                        new Assign(
                                            new Local("_local3"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local3")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb4") }
                                        },
                                        new BasicBlockId("bb3"))),
                                new BasicBlock(
                                    new BasicBlockId("bb3"),
                                    [
                                        new Assign(
                                            new Local("_local3"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb4"))),
                                new BasicBlock(new BasicBlockId("bb4"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "c", Int64T),
                                new NewMethodLocal("_local2", "FieldB", StringT),
                                new NewMethodLocal("_local3", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - class pattern with no fields",
                """
                class MyClass {}
                var a = new MyClass{};
                var b = a matches MyClass {};
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyClass",
                            variants: [
                                NewVariant("_classVariant",
                                    [
                                    ])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                new NewMethodLocal("_local1", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - class pattern",
                """
                class MyClass { pub field Field0: i64, pub field Field1: i64, pub field Field2: bool }
                var a = new MyClass{Field0 = 0, Field1 = 1, Field2 = true };
                var b = a matches MyClass {Field0: var c, Field1: _, Field2: _};
                """,
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyClass",
                            variants: [
                                NewVariant("_classVariant",
                                    [
                                        NewField("Field0", Int64T),
                                        NewField("Field1", Int64T),
                                        NewField("Field2", BooleanT)
                                    ])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "Field0", "_classVariant"),
                                            new Use(new IntConstant(0, 8))),
                                        new Assign(
                                            new Field(new Local("_local0"), "Field1", "_classVariant"),
                                            new Use(new IntConstant(1, 8))),
                                        new Assign(
                                            new Field(new Local("_local0"), "Field2", "_classVariant"),
                                            new Use(new BoolConstant(true))),
                                        new Assign(
                                            new Local("_local1"),
                                            new Use(new Copy(new Field(new Local("_local0"), "Field0", "_classVariant")))),
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local2")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb3") }
                                        },
                                        new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Local("_local2")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb3") }
                                        },
                                        new BasicBlockId("bb2"))),
                                new BasicBlock(
                                    new BasicBlockId("bb2"),
                                    [
                                        new Assign(
                                            new Local("_local2"),
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(new BasicBlockId("bb3"))),
                                new BasicBlock(new BasicBlockId("bb3"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                new NewMethodLocal("_local1", "c", Int64T),
                                new NewMethodLocal("_local2", "b", BooleanT),
                            ])
                    ])
            }
        };
    }
}
