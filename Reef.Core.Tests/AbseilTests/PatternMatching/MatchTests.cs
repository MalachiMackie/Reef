using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests.PatternMatching;

public class MatchTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
    [Fact]
    public void Single()
    {
        var source = """
                 var a = match (1) {
                     i64 => 2
                 }
                 """;
                 var expectedProgram = LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                             ])
                     ]);
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
    

    private const string ModuleId = "MatchTests";

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
            {
                "match on union variant",
                """
                union MyUnion{A, B, C}
                var a = MyUnion::A;
                var b = match(a) {
                    MyUnion::A => 1,
                    MyUnion::B => 2,
                    MyUnion::C => 3,
                };
                """,
                LoweredProgram(
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                               Variant("A", [Field("_variantIdentifier", UInt16T)]),
                               Variant("B", [Field("_variantIdentifier", UInt16T)]),
                               Variant("C", [Field("_variantIdentifier", UInt16T)]),
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2)))
                                    ],
                                    new SwitchInt(
                                        new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                        new Dictionary<int, BasicBlockId>
                                        {
                                            { 0, new BasicBlockId("bb1") },
                                            { 1, new BasicBlockId("bb2") },
                                            { 2, new BasicBlockId("bb3") }
                                        },
                                        new BasicBlockId("bb4"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [
                                        new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))
                                    ],
                                    new GoTo(new BasicBlockId("bb4"))),
                                new BasicBlock(
                                    new BasicBlockId("bb2"),
                                    [
                                        new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))
                                    ],
                                    new GoTo(new BasicBlockId("bb4"))),
                                new BasicBlock(
                                    new BasicBlockId("bb3"),
                                    [
                                        new Assign(new Local("_local1"), new Use(new IntConstant(3, 4)))
                                    ],
                                    new GoTo(new BasicBlockId("bb4"))),
                                new BasicBlock(new BasicBlockId("bb4"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new MethodLocal("_local1", "b", Int32T)
                            ])
                    ])
            },
             {
                 "match on union variant with discard",
                 """
                 union MyUnion{A, B, C}
                 var a = MyUnion::A;
                 var b = match(a) {
                     MyUnion::A => 1,
                     _ => 2
                 };
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                                Variant("C", [Field("_variantIdentifier", UInt16T)]),
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "_variantIdentifier", "A"),
                                             new Use(new UIntConstant(0, 2))),
                                     ],
                                     new SwitchInt(
                                         new Copy(new Field(new Local("_local0"), "_variantIdentifier", "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") }
                                         },
                                         new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(new BasicBlockId("bb3"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local1", "b", Int32T)
                             ])
                     ])
             },
             {
                 "match union tuple variant sub patterns",
                 """
                 union OtherUnion {A, B, C}
                 union MyUnion {X(OtherUnion), Y}

                 var a = MyUnion::Y;
                 var b = match(a) {
                     MyUnion::X(OtherUnion::A) => 1,
                     MyUnion::X(OtherUnion::B) => 2,
                     MyUnion::X(OtherUnion::C) => 3,
                     MyUnion::Y => 4,
                 };
                 """,
                 LoweredProgram(
                     types:
                     [
                         DataType(ModuleId, "OtherUnion",
                             variants:
                             [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                                Variant("C", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                         DataType(ModuleId, "MyUnion",
                             variants:
                             [
                                Variant(
                                     "X",
                                     [
                                         Field("_variantIdentifier", UInt16T),
                                         Field("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
                                     ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                     ],
                     methods:
                     [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "_variantIdentifier", "X"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "Item0", "X"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))],
                             returnType: new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "_variantIdentifier", "Y"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Field(new Local("_local0"), "_variantIdentifier", "X")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") },
                                             { 1, new BasicBlockId("bb5") }
                                         },
                                         new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "Item0",
                                                     "X"), 
                                                 "_variantIdentifier", 
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") },
                                             { 1, new BasicBlockId("bb3") },
                                             { 2, new BasicBlockId("bb4") }
                                         },
                                         new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(1, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(3, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(4, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(new BasicBlockId("bb6"), [], new Return())
                             ],
                             Unit,
                             locals:
                             [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local1", "b", Int32T)
                             ])
                     ])
             },
             {
                 "match union tuple variant sub patterns and variant pattern",
                 """
                 union OtherUnion{A, B}
                 union MyUnion {X(OtherUnion), Y}
                 
                 var a = MyUnion::Y;
                 var b = match (a) {
                     MyUnion::X(OtherUnion::A) => 1,
                     MyUnion::X => 2,
                     MyUnion::Y => 3
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "OtherUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant(
                                     "X",
                                     [
                                         Field("_variantIdentifier", UInt16T),
                                         Field("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
                                     ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16T)])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "_variantIdentifier", "X"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "Item0", "X"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))],
                             returnType: new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "_variantIdentifier", "Y"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Field(new Local("_local0"), "_variantIdentifier", "X")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") },
                                             { 1, new BasicBlockId("bb4") }
                                         },
                                         new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(new Field(
                                             new Field(
                                                 new Local("_local0"),
                                                 "Item0",
                                                 "X"),
                                             "_variantIdentifier",
                                             "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") },
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(1, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new IntConstant(3, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(new BasicBlockId("bb5"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local1", "b", Int32T)
                             ])
                     ])
             },
             {
                 "match union tuple variant sub patterns and discard",
                 """
                 union OtherUnion {A, B, C, D}
                 union MyUnion {X(OtherUnion), Y, Z}

                 var a = MyUnion::Y;
                 var b = match(a) {
                     MyUnion::X(OtherUnion::A) => 1,
                     MyUnion::X(OtherUnion::B) => 2,
                     MyUnion::X(OtherUnion::C) => 3,
                     _ => 4,
                 };
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "OtherUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                                Variant("C", [Field("_variantIdentifier", UInt16T)]),
                                Variant("D", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant(
                                     "X",
                                     [
                                         Field("_variantIdentifier", UInt16T),
                                         Field("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
                                     ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16T)]),
                                Variant("Z", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "_variantIdentifier", "X"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "Item0", "X"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))],
                             returnType: new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "_variantIdentifier", "Y"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Field(new Local("_local0"), "_variantIdentifier", "X")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") },
                                         },
                                         new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(new Field(
                                             new Field(
                                                 new Local("_local0"),
                                                 "Item0",
                                                 "X"),
                                             "_variantIdentifier",
                                             "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") },
                                             { 1, new BasicBlockId("bb3") },
                                             { 2, new BasicBlockId("bb4") },
                                         },
                                         new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new IntConstant(3, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new IntConstant(4, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb6"))),
                                 new BasicBlock(new BasicBlockId("bb6"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local1", "b", Int32T)
                             ])
                     ])
             },
             {
                 "match tuple variant with multiple sub patterns",
                 """
                 union OtherUnion {A, B}
                 union MyUnion {X(OtherUnion, OtherUnion), Y}
                 var a = MyUnion::Y;
                 var b = match (a) {
                     MyUnion::Y => 0,
                     MyUnion::X(OtherUnion::A, OtherUnion::A) => 1,
                     MyUnion::X(OtherUnion::A, OtherUnion::B) => 2,
                     MyUnion::X(OtherUnion::B, OtherUnion::A) => 3,
                     MyUnion::X(OtherUnion::B, OtherUnion::B) => 4,
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "OtherUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant(
                                     "X",
                                     [
                                         Field("_variantIdentifier", UInt16T),
                                         Field("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
                                         Field("Item1", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
                                     ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16T)])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "_variantIdentifier", "X"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "Item0", "X"),
                                             new Use(new Copy(new Local("_param0")))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "Item1", "X"),
                                             new Use(new Copy(new Local("_param1")))),
                                     ],
                                     new Return())
                             ],
                             parameters: [
                                 ("Item0", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
                                 ("Item1", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))],
                             returnType: new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "_variantIdentifier", "Y"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Field(new Local("_local0"), "_variantIdentifier", "X")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") },
                                             { 1, new BasicBlockId("bb1") }
                                         }, new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(0, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [],
                                     new SwitchInt(
                                         new Copy(new Field(
                                             new Field(
                                                 new Local("_local0"), "Item0", "X"),
                                             "_variantIdentifier",
                                             "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb3") },
                                             { 1, new BasicBlockId("bb6") }
                                         },
                                         new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [],
                                     new SwitchInt(
                                         new Copy(new Field(
                                             new Field(
                                                 new Local("_local0"), "Item1", "X"),
                                             "_variantIdentifier",
                                             "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb4") },
                                             { 1, new BasicBlockId("bb5") }
                                         },
                                         new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb6"),
                                     [],
                                     new SwitchInt(
                                         new Copy(new Field(
                                             new Field(
                                                 new Local("_local0"), "Item1", "X"),
                                             "_variantIdentifier",
                                             "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb7") },
                                             { 1, new BasicBlockId("bb8") }
                                         },
                                         new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb7"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(3, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb8"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(4, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(new BasicBlockId("bb9"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local1", "b", Int32T),
                             ])
                     ])
             },
             {
                 "match union class variant sub patterns and discard",
                 """
                 union OtherUnion {A, B, C, D}
                 union MyUnion {X{field MyField: OtherUnion}, Y}

                 var a = MyUnion::Y;
                 var b = match(a) {
                     MyUnion::X {MyField: OtherUnion::A} var something => 1,
                     MyUnion::X {MyField: var myField} var somethingElse => 2,
                     var myUnion => 4,
                 };
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "OtherUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                                Variant("C", [Field("_variantIdentifier", UInt16T)]),
                                Variant("D", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant(
                                     "X",
                                     [
                                         Field("_variantIdentifier", UInt16T),
                                         Field("MyField", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
                                     ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "_variantIdentifier", "Y"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Field(new Local("_local0"), "_variantIdentifier", "X")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             {0, new BasicBlockId("bb1")},
                                         },
                                         new BasicBlockId("bb4"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "X"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             {0, new BasicBlockId("bb2")}
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new Copy(new Local("_local0")))),
                                         new Assign(new Local("_local5"), new Use(new IntConstant(1, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [
                                         new Assign(
                                             new Local("_local2"),
                                             new Use(
                                                 new Copy(
                                                     new Field(
                                                         new Local("_local0"),
                                                         "MyField",
                                                         "X")))),
                                         new Assign(
                                             new Local("_local3"),
                                             new Use(new Copy(new Local("_local0")))),
                                         new Assign(
                                             new Local("_local5"),
                                             new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [
                                         new Assign(
                                             new Local("_local4"),
                                             new Use(new Copy(new Local("_local0")))),
                                         new Assign(
                                             new Local("_local5"),
                                             new Use(new IntConstant(4, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [],
                                     new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local1", "something", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local2", "myField", new LoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
                                 new MethodLocal("_local3", "somethingElse", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local4", "myUnion", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new MethodLocal("_local5", "b", Int32T),
                             ])
                     ])
             },
             {
                 "match type pattern",
                 """
                 var a = match (1) {
                     i64 => 2
                 }
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                             ])
                     ])
             },
             {
                 "match type pattern 2",
                 """
                 fn GetI64(): i64 { return 1; }
                 var a = match (GetI64()) {
                     i64 => 2
                 }
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.GetI64"), "GetI64",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [new Assign(new Local("_returnValue"), new Use(new IntConstant(1, 8)))],
                                     new Return())
                             ],
                             Int64T),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.GetI64"), []),
                                         [],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                                 new MethodLocal("_local1", null, Int64T),
                             ])
                     ])
             },
             {
                 "match class pattern",
                 """
                 union MyUnion{A, B}
                 class MyClass{pub field MyField: MyUnion}

                 var a = new MyClass{MyField = MyUnion::A};
                 var b = match (a) {
                     MyClass{MyField: MyUnion::A} => 1,
                     MyClass{MyField: MyUnion::B} var something => 2,
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                             ]),
                         DataType(ModuleId, "MyClass",
                             variants: [
                                Variant("_classVariant", [Field("MyField", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "MyField", "_classVariant"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2))),
                                     ],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") },
                                             { 1, new BasicBlockId("bb2") },
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local2"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(new Local("_local1"), new Use(new Copy(new Local("_local0")))),
                                         new Assign(new Local("_local2"), new Use(new IntConstant(2, 4)))
                                     ],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(new BasicBlockId("bb3"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new MethodLocal("_local1", "something", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new MethodLocal("_local2", "b", Int32T),
                             ])
                     ])
             },
             {
                 "match partial class pattern with discard",
                 """
                 union MyUnion{A, B}
                 class MyClass{pub field MyField: MyUnion}

                 var a = new MyClass{MyField = MyUnion::A};
                 var b = match (a) {
                     MyClass{MyField: MyUnion::A} => 1,
                     _ => 2
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)])
                             ]),
                         DataType(ModuleId, "MyClass",
                             variants: [
                                Variant("_classVariant", [Field("MyField", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "MyField",
                                                 "_classVariant"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") }
                                         },
                                         new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(new BasicBlockId("bb3"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new MethodLocal("_local1", "b", Int32T),
                             ])
                     ])
             },
             {
                 "match partial class pattern with discard 2",
                 """
                 union MyUnion{A, B, C}
                 class MyClass{pub field MyField: MyUnion, pub field SecondField: MyUnion}

                 var a = new MyClass {
                     MyField = MyUnion::A,
                     SecondField = MyUnion::B,
                 };
                 var b = match (a) {
                     MyClass { MyField: MyUnion::A, SecondField: MyUnion::A } => 1,
                     MyClass { MyField: MyUnion::B, SecondField: MyUnion::A } => 2,
                     MyClass { MyField: MyUnion::C, SecondField: MyUnion::A } => 3,
                     _ => 4
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                                Variant("C", [Field("_variantIdentifier", UInt16T)])
                             ]),
                         DataType(ModuleId, "MyClass",
                             variants: [
                                Variant(
                                     "_classVariant",
                                     [
                                         Field("MyField", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                         Field("SecondField", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                     ])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "MyField",
                                                 "_classVariant"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "SecondField",
                                                 "_classVariant"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "B"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") },
                                             { 1, new BasicBlockId("bb3") },
                                             { 2, new BasicBlockId("bb5") },
                                         },
                                         new BasicBlockId("bb7"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") }
                                         },
                                         new BasicBlockId("bb7"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb4") }
                                         },
                                         new BasicBlockId("bb7"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb6") }
                                         },
                                         new BasicBlockId("bb7"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb6"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(3, 4)))],
                                     new GoTo(new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb7"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(4, 4)))],
                                     new GoTo(new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb8"),
                                     [],
                                     new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new MethodLocal("_local1", "b", Int32T),
                             ])
                     ])
             },
             {
                 "Mixture of class and union patterns",
                 """
                 union MyUnion{A, B, C}
                 class MyClass{pub field MyField: MyUnion, pub field SecondField: MyUnion}

                 var a = new MyClass {
                     MyField = MyUnion::A,
                     SecondField = MyUnion::B,
                 };
                 var b = match (a) {
                     MyClass { MyField: MyUnion::A, SecondField: MyUnion::A } => 1,
                     MyClass { MyField: MyUnion::A, SecondField: _          } => 2,
                     MyClass { MyField: MyUnion::B, SecondField: MyUnion::B } => 3,
                     MyClass { MyField: MyUnion::C, SecondField: MyUnion::A } => 4,
                     _ => 5
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [Field("_variantIdentifier", UInt16T)]),
                                Variant("C", [Field("_variantIdentifier", UInt16T)]),
                             ]),
                         DataType(ModuleId, "MyClass",
                             variants: [
                                Variant(
                                     "_classVariant",
                                     [
                                         Field("MyField", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                         Field("SecondField", new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))
                                     ])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "MyField",
                                                 "_classVariant"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "SecondField",
                                                 "_classVariant"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "B"),
                                             new Use(new UIntConstant(1, 2)))
                                     ],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "MyField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb1") },
                                             { 1, new BasicBlockId("bb4") },
                                             { 2, new BasicBlockId("bb6") },
                                         },
                                         new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") }
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 1, new BasicBlockId("bb5") }
                                         },
                                         new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(3, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb6"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Field(
                                                     new Local("_local0"),
                                                     "SecondField",
                                                     "_classVariant"),
                                                 "_variantIdentifier",
                                                 "A")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb7") }
                                         },
                                         new BasicBlockId("bb8"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb7"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(4, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb8"),
                                     [new Assign(new Local("_local1"), new Use(new IntConstant(5, 4)))],
                                     new GoTo(new BasicBlockId("bb9"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb9"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new MethodLocal("_local1", "b", Int32T),
                             ])
                     ])
             }
        };
    }
}
