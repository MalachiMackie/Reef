using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests.PatternMatching;

public class MatchesTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchesAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "MatchesTests";

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
            {
                "matches - discard pattern",
                """
                var b = 1 matches _;
                """,
                LoweredProgram(ModuleId, 
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - variable declaration pattern",
                """
                var b = 1 matches var a;
                """,
                LoweredProgram(ModuleId, 
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(Local0, new Use(new IntConstant(1, 4))),
                                        new Assign(Local1, new Use(new BoolConstant(true))),
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", Int32T),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(
                            ModuleId,
                            "_Main__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("a", Int32T) 
                                    ])
                            ]),
                        DataType(
                            ModuleId,
                            "SomeFn__Closure",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field(
                                            "_Main__Locals",
                                            new LoweredPointer(new LoweredConcreteTypeReference(
                                                "_Main__Locals",
                                                new DefId(ModuleId, $"{ModuleId}._Main__Locals"),
                                                [])))
                                    ])
                            ])
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}.SomeFn"),
                            "SomeFn",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new Use(new Copy(
                                                new Field(
                                                    new Deref(new Field(
                                                        new Deref(Param0),
                                                        "_Main__Locals",
                                                        "_classVariant")),
                                                    "a",
                                                    "_classVariant"))))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "c", Int32T)
                            ],
                            parameters: [
                                (
                                    "closure",
                                    new LoweredPointer(new LoweredConcreteTypeReference(
                                        "SomeFn__Closure",
                                        new DefId(ModuleId, $"{ModuleId}.SomeFn__Closure"),
                                        [])))
                            ]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("_Main__Locals", ModuleId),
                                        LocalsObject,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(LocalsObject),
                                            new CreateObject(new LoweredConcreteTypeReference(
                                                "_Main__Locals",
                                                new DefId(ModuleId, $"{ModuleId}._Main__Locals"),
                                                []))),
                                        new Assign(
                                            new Field(
                                                new Deref(LocalsObject),
                                                "a",
                                                "_classVariant"),
                                            new Use(new IntConstant(1, 4))),
                                        new Assign(
                                            Local1,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_localsObject",
                                    null,
                                    new LoweredPointer(new LoweredConcreteTypeReference(
                                        "_Main__Locals",
                                        new DefId(ModuleId, $"{ModuleId}._Main__Locals"),
                                        []))),
                                new MethodLocal("_local1", "b", BooleanT)
                            ])
                    ])
            },
            {
                "matches - type pattern",
                """
                var b = 1 matches i64;
                """,
                LoweredProgram(ModuleId, 
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - type pattern with variable declaration",
                """
                var b = 1 matches i64 var a;
                """,
                LoweredProgram(ModuleId, 
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new Use(new IntConstant(1, 8))),
                                        new Assign(
                                            Local1,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", Int64T),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            Local1,
                                            new BinaryOperation(
                                                new Copy(new Field(
                                                    new Deref(Local0),
                                                    "_variantIdentifier",
                                                    "B")),
                                                new UIntConstant(1, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(Local0))),
                                        new Assign(
                                            Local2,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "B")),
                                                new UIntConstant(1, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "c", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local2", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T), Field("Item0", Int64T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        ReturnValue,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(ReturnValue),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item0", "A"),
                                            new Use(new Copy(Param0)))
                                    ],
                                    new Return())
                            ],
                            parameters: [("Item0", Int64T)],
                            returnType: new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local1,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local1),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            {0, BB3}
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(Local1, new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB3)),
                                new BasicBlock(BB3, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T), Field("Item0", Int64T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        ReturnValue,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(ReturnValue),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item0", "A"),
                                            new Use(new Copy(Param0)))
                                    ],
                                    new Return())
                            ],
                            parameters: [("Item0", Int64T)],
                            returnType: new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(Local0))),
                                        new Assign(
                                            Local2,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local2),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB3 }
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(Local2, new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB3)),
                                new BasicBlock(BB3, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "c", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local2", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with multiple members",
                """
                union MyUnion{A(i64, string, bool), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_, var c, _);
                """,
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("Item0", Int64T),
                                        Field("Item1", StringT),
                                        Field("Item2", BooleanT),
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        ReturnValue,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(ReturnValue),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item0", "A"),
                                            new Use(new Copy(Param0))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item1", "A"),
                                            new Use(new Copy(Param1))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item2", "A"),
                                            new Use(new Copy(Param2))),
                                    ],
                                    new Return())
                            ],
                            parameters: [("Item0", Int64T), ("Item1", StringT), ("Item2", BooleanT)],
                            returnType: new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local2,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local2),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB5 }
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(
                                            Local2,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local2),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB5 }
                                        },
                                        BB3)),
                                new BasicBlock(
                                    BB3,
                                    [
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(new Field(new Deref(Local0), "Item1", "A")))),
                                        new Assign(
                                            Local2,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local2),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB5 }
                                        },
                                        BB4)),
                                new BasicBlock(
                                    BB4,
                                    [
                                        new Assign(
                                            Local2,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB5)),
                                new BasicBlock(BB5, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "c", StringT),
                                new MethodLocal("_local2", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("FieldA", Int64T)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local1,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal)),
                                    ],
                                    new SwitchInt(
                                        new Copy(Local1),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB3}
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(Local1, new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB3)),
                                new BasicBlock(BB3, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("FieldA", Int64T)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local1,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("FieldA", Int64T),
                                        Field("FieldB", StringT),
                                        Field("FieldC", BooleanT)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local3,
                                            new BinaryOperation(
                                                new Copy(new Field(new Deref(Local0), "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local3),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB5}
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(
                                                new Field(new Deref(Local0), "FieldA", "A")))),
                                        new Assign(
                                            Local3,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local3),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB5 }
                                        },
                                        BB3)),
                                new BasicBlock(
                                    BB3,
                                    [
                                        new Assign(
                                            Local2,
                                            new Use(new Copy(
                                                new Field(new Deref(Local0), "FieldB", "A")))),
                                        new Assign(
                                            Local3,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local3),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB5 }
                                        },
                                        BB4)),
                                new BasicBlock(
                                    BB4,
                                    [
                                        new Assign(
                                            Local3,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB5)),
                                new BasicBlock(BB5, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new MethodLocal("_local1", "c", Int64T),
                                new MethodLocal("_local2", "FieldB", StringT),
                                new MethodLocal("_local3", "b", BooleanT),
                            ])
                    ])
            },
            {
                "matches - unboxed union class variant pattern multiple fields",
                """
                union MyUnion{
                    A {field FieldA: i64, field FieldB: string, field FieldC: bool},
                    B
                }
                var a = unboxed MyUnion::B;
                var b = a matches unboxed MyUnion::A{FieldA: var c, FieldB, FieldC: _};
                """,
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("FieldA", Int64T),
                                        Field("FieldB", StringT),
                                        Field("FieldC", BooleanT)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(Local0, "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            Local3,
                                            new BinaryOperation(
                                                new Copy(new Field(Local0, "_variantIdentifier", "A")),
                                                new UIntConstant(0, 2),
                                                BinaryOperationKind.Equal))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local3),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB4}
                                        },
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(
                                                new Field(Local0, "FieldA", "A")))),
                                        new Assign(
                                            Local3,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local3),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB4 }
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(
                                            Local2,
                                            new Use(new Copy(
                                                new Field(Local0, "FieldB", "A")))),
                                        new Assign(
                                            Local3,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local3),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB4 }
                                        },
                                        BB3)),
                                new BasicBlock(
                                    BB3,
                                    [
                                        new Assign(
                                            Local3,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB4)),
                                new BasicBlock(BB4, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new MethodLocal("_local1", "c", Int64T),
                                new MethodLocal("_local2", "FieldB", StringT),
                                new MethodLocal("_local3", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyClass", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                        new Assign(
                                            Local1,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                new MethodLocal("_local1", "b", BooleanT),
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
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("Field0", Int64T),
                                        Field("Field1", Int64T),
                                        Field("Field2", BooleanT)
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyClass", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "Field0", "_classVariant"),
                                            new Use(new IntConstant(0, 8))),
                                        new Assign(
                                            new Field(new Deref(Local0), "Field1", "_classVariant"),
                                            new Use(new IntConstant(1, 8))),
                                        new Assign(
                                            new Field(new Deref(Local0), "Field2", "_classVariant"),
                                            new Use(new BoolConstant(true))),
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(new Field(new Deref(Local0), "Field0", "_classVariant")))),
                                        new Assign(
                                            Local2,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local2),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB4 }
                                        },
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(
                                            Local2,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new SwitchInt(
                                        new Copy(Local2),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, BB4 }
                                        },
                                        BB3)),
                                new BasicBlock(
                                    BB3,
                                    [
                                        new Assign(
                                            Local2,
                                            new Use(new BoolConstant(true)))
                                    ],
                                    new GoTo(BB4)),
                                new BasicBlock(BB4, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                new MethodLocal("_local1", "c", Int64T),
                                new MethodLocal("_local2", "b", BooleanT),
                            ])
                    ])
            }
        };
    }
}
