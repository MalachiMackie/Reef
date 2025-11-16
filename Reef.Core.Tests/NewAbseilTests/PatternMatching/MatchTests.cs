using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests.PatternMatching;

public class MatchTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "MatchTests";

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
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
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                               NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                               NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
                               NewVariant("C", [NewField("_variantIdentifier", UInt16T)]),
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
                                new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                new NewMethodLocal("_local1", "b", Int32T)
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyUnion",
                             variants: [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("C", [NewField("_variantIdentifier", UInt16T)]),
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
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new NewMethodLocal("_local1", "b", Int32T)
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
                 NewLoweredProgram(
                     types:
                     [
                         NewDataType(ModuleId, "OtherUnion",
                             variants:
                             [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("C", [NewField("_variantIdentifier", UInt16T)]),
                             ]),
                         NewDataType(ModuleId, "MyUnion",
                             variants:
                             [
                                NewVariant(
                                     "X",
                                     [
                                         NewField("_variantIdentifier", UInt16T),
                                         NewField("Item0", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
                                     ]),
                                NewVariant("Y", [NewField("_variantIdentifier", UInt16T)]),
                             ]),
                     ],
                     methods:
                     [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "_variantIdentifier", "X"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(new Local("_returnValue"), "Item0", "X"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))],
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
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                 new NewMethodLocal("_local1", "b", Int32T)
                             ])
                     ])
             },
//             {
//                 "match union tuple variant sub patterns and variant pattern",
//                 """
//                 union OtherUnion{A, B}
//                 union MyUnion {X(OtherUnion), Y}
//                 
//                 var a = MyUnion::Y;
//                 match (a) {
//                     MyUnion::X(OtherUnion::A) => 1,
//                     MyUnion::X => 2,
//                     MyUnion::Y => 3
//                 }
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "OtherUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant(
//                                     "X",
//                                     [
//                                         NewField("_variantIdentifier", UInt16T),
//                                         NewField("Item0", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                     ]),
//                                NewVariant("Y", [NewField("_variantIdentifier", UInt16T)])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
//                             [
//                                 MethodReturn(
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "X",
//                                         true,
//                                         new()
//                                         {
//                                             {"_variantIdentifier", UInt16Constant(0, true)},
//                                             {"Item0", LoadArgument(0, true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))}
//                                         }))
//                             ],
//                             parameters: [new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])],
//                             returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "Y",
//                                         true,
//                                         new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration(
//                                             "Local1",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                             false),
//                                         SwitchInt(
//                                             FieldAccess(
//                                                 LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                 "_variantIdentifier",
//                                                 "X",
//                                                 true,
//                                                 UInt16T),
//                                             new()
//                                             {
//                                                 {
//                                                     0,
//                                                     Block(
//                                                         [
//                                                             VariableDeclaration(
//                                                                 "Local2",
//                                                                 FieldAccess(
//                                                                     LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                     "Item0",
//                                                                     "X",
//                                                                     true,
//                                                                     new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                 false),
//                                                             SwitchInt(
//                                                                 FieldAccess(
//                                                                     LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                     "_variantIdentifier",
//                                                                     "A",
//                                                                     true,
//                                                                     UInt16T),
//                                                                 new()
//                                                                 {
//                                                                     {0, Int32Constant(1, true)}
//                                                                 },
//                                                                 Int32Constant(2, true),
//                                                                 true,
//                                                                 Int32T)
//                                                         ],
//                                                         Int32T,
//                                                         true)
//                                                 },
//                                                 { 1, Int32Constant(3, true) }
//                                             },
//                                             Unreachable(),
//                                             true,
//                                             Int32T)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local1", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                             ])
//                     ])
//             },
//             {
//                 "match union tuple variant sub patterns and discard",
//                 """
//                 union OtherUnion {A, B, C, D}
//                 union MyUnion {X(OtherUnion), Y}
//
//                 var a = MyUnion::Y;
//                 match(a) {
//                     MyUnion::X(OtherUnion::A) => 1,
//                     MyUnion::X(OtherUnion::B) => 2,
//                     MyUnion::X(OtherUnion::C) => 3,
//                     _ => 4,
//                 };
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "OtherUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("C", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("D", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant(
//                                     "X",
//                                     [
//                                         NewField("_variantIdentifier", UInt16T),
//                                         NewField("Item0", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
//                                     ]),
//                                NewVariant("Y", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
//                             [
//                                 MethodReturn(
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "X",
//                                         true,
//                                         new()
//                                         {
//                                             {"_variantIdentifier", UInt16Constant(0, true)},
//                                             {"Item0", LoadArgument(0, true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))}
//                                         }))
//                             ],
//                             parameters: [new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])],
//                             returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "Y",
//                                         true,
//                                         new(){
//                                             {"_variantIdentifier", UInt16Constant(1, true)}
//                                         }),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration(
//                                             "Local1",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                             false),
//                                         SwitchInt(
//                                             FieldAccess(
//                                                 LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                 "_variantIdentifier",
//                                                 "X",
//                                                 true,
//                                                 UInt16T),
//                                             new()
//                                             {
//                                                 {
//                                                     0,
//                                                     Block(
//                                                         [
//                                                             VariableDeclaration(
//                                                                 "Local2",
//                                                                 FieldAccess(
//                                                                     LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                     "Item0",
//                                                                     "X",
//                                                                     true,
//                                                                     new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                 false),
//                                                             SwitchInt(
//                                                                 FieldAccess(
//                                                                     LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                     "_variantIdentifier",
//                                                                     "A",
//                                                                     true,
//                                                                     UInt16T),
//                                                                 new()
//                                                                 {
//                                                                     { 0, Int32Constant(1, true) },
//                                                                     { 1, Int32Constant(2, true) },
//                                                                     { 2, Int32Constant(3, true) },
//                                                                 },
//                                                                 Int32Constant(4, true),
//                                                                 true,
//                                                                 Int32T)
//                                                         ],
//                                                         Int32T,
//                                                         true)
//                                                 }
//                                             },
//                                             Int32Constant(4, true),
//                                             true,
//                                             Int32T)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local1", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
//                             ])
//                     ])
//             },
//             {
//                 "match tuple variant with multiple sub patterns",
//                 """
//                 union OtherUnion {A, B}
//                 union MyUnion {X(OtherUnion, OtherUnion), Y}
//                 var a = MyUnion::Y;
//                 match (a) {
//                     MyUnion::Y => 0,
//                     MyUnion::X(OtherUnion::A, OtherUnion::A) => 1,
//                     MyUnion::X(OtherUnion::A, OtherUnion::B) => 2,
//                     MyUnion::X(OtherUnion::B, OtherUnion::A) => 3,
//                     MyUnion::X(OtherUnion::B, OtherUnion::B) => 4,
//                 }
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "OtherUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant(
//                                     "X",
//                                     [
//                                         NewField("_variantIdentifier", UInt16T),
//                                         NewField("Item0", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                         NewField("Item1", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                     ]),
//                                NewVariant("Y", [NewField("_variantIdentifier", UInt16T)])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__X"), "MyUnion__Create__X",
//                             [
//                                 MethodReturn(
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "X",
//                                         true,
//                                         new()
//                                         {
//                                             {"Item0", LoadArgument(0, true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))},
//                                             {"Item1", LoadArgument(1, true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))},
//                                             {"_variantIdentifier", UInt16Constant(0, true)},
//                                         }))
//                             ],
//                             parameters: [new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []), new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])],
//                             returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a", 
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "Y",
//                                         true,
//                                         new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration(
//                                             "Local1",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                             false),
//                                         SwitchInt(
//                                             FieldAccess(
//                                                 LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                 "_variantIdentifier",
//                                                 "X",
//                                                 true,
//                                                 UInt16T),
//                                             new()
//                                             {
//                                                 {
//                                                     1,
//                                                     Int32Constant(0, true)
//                                                 },
//                                                 {
//                                                     0,
//                                                     Block(
//                                                         [
//                                                             VariableDeclaration(
//                                                                 "Local4",
//                                                                 FieldAccess(
//                                                                     LocalAccess(
//                                                                         "Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                     "Item0",
//                                                                     "X",
//                                                                     true,
//                                                                     new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                 false),
//                                                             SwitchInt(
//                                                                 FieldAccess(
//                                                                     LocalAccess("Local4", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                     "_variantIdentifier",
//                                                                     "A",
//                                                                     true,
//                                                                     UInt16T),
//                                                                 new()
//                                                                 {
//                                                                     {
//                                                                         0,
//                                                                         Block(
//                                                                             [
//                                                                                 VariableDeclaration(
//                                                                                     "Local2",
//                                                                                     FieldAccess(
//                                                                                         LocalAccess(
//                                                                                             "Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                                         "Item1",
//                                                                                         "X",
//                                                                                         true,
//                                                                                         new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                                     false),
//                                                                                 SwitchInt(
//                                                                                     FieldAccess(
//                                                                                         LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                                         "_variantIdentifier",
//                                                                                         "A",
//                                                                                         true,
//                                                                                         UInt16T),
//                                                                                     new()
//                                                                                     {
//                                                                                         {
//                                                                                             0,
//                                                                                             Int32Constant(1, true)
//                                                                                         },
//                                                                                         {
//                                                                                             1,
//                                                                                             Int32Constant(2, true)
//                                                                                         }
//                                                                                     },
//                                                                                     Unreachable(),
//                                                                                     true,
//                                                                                     Int32T)
//                                                                             ],
//                                                                             Int32T,
//                                                                             true)
//                                                                     },
//                                                                     {
//                                                                         1,
//                                                                         Block(
//                                                                             [
//                                                                                 VariableDeclaration(
//                                                                                     "Local3",
//                                                                                     FieldAccess(
//                                                                                         LocalAccess(
//                                                                                             "Local1", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                                         "Item1",
//                                                                                         "X",
//                                                                                         true,
//                                                                                         new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                                     false),
//                                                                                 SwitchInt(
//                                                                                     FieldAccess(
//                                                                                         LocalAccess("Local3", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                                         "_variantIdentifier",
//                                                                                         "A",
//                                                                                         true,
//                                                                                         UInt16T),
//                                                                                     new()
//                                                                                     {
//                                                                                         {
//                                                                                             0,
//                                                                                             Int32Constant(3, true)
//                                                                                         },
//                                                                                         {
//                                                                                             1,
//                                                                                             Int32Constant(4, true)
//                                                                                         }
//                                                                                     },
//                                                                                     Unreachable(),
//                                                                                     true,
//                                                                                     Int32T)
//                                                                             ],
//                                                                             Int32T,
//                                                                             true)
//                                                                     }
//                                                                 },
//                                                                 Unreachable(),
//                                                                 true,
//                                                                 Int32T)
//                                                         ],
//                                                         Int32T,
//                                                         true)
//                                                 }
//                                             },
//                                             Unreachable(),
//                                             true,
//                                             Int32T)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local1", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local3", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local4", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                             ])
//                     ])
//             },
//             {
//                 "match union class variant sub patterns and discard",
//                 """
//                 union OtherUnion {A, B, C, D}
//                 union MyUnion {X{field MyField: OtherUnion}, Y}
//
//                 var a = MyUnion::Y;
//                 match(a) {
//                     MyUnion::X {MyField: OtherUnion::A} var something => 1,
//                     MyUnion::X {MyField: var myField} var somethingElse => 2,
//                     var myUnion => 4,
//                 };
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "OtherUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("C", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("D", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant(
//                                     "X",
//                                     [
//                                         NewField("_variantIdentifier", UInt16T),
//                                         NewField("MyField", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), []))
//                                     ]),
//                                NewVariant("Y", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                         "Y",
//                                         true,
//                                         new(){
//                                             {"_variantIdentifier", UInt16Constant(1, true)}
//                                         }),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration(
//                                             "Local5",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                             false),
//                                         SwitchInt(
//                                             FieldAccess(
//                                                 LocalAccess("Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                 "_variantIdentifier",
//                                                 "X",
//                                                 true,
//                                                 UInt16T),
//                                             new()
//                                             {
//                                                 {
//                                                     0,
//                                                     Block(
//                                                         [
//                                                             VariableDeclaration("Local6",
//                                                                 FieldAccess(
//                                                                     LocalAccess(
//                                                                         "Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                     "MyField",
//                                                                     "X",
//                                                                     true,
//                                                                     new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                 false),
//                                                             SwitchInt(
//                                                                 FieldAccess(
//                                                                     LocalAccess("Local6", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                     "_variantIdentifier",
//                                                                     "A",
//                                                                     true,
//                                                                     UInt16T),
//                                                                 new()
//                                                                 {
//                                                                     {
//                                                                         0,
//                                                                         Block(
//                                                                             [
//                                                                                 VariableDeclaration(
//                                                                                     "something",
//                                                                                     LocalAccess("Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                                     false),
//                                                                                 Int32Constant(1, true)
//                                                                             ],
//                                                                             Int32T,
//                                                                             true)
//                                                                     }
//                                                                 },
//                                                                 Block(
//                                                                     [
//                                                                         VariableDeclaration(
//                                                                             "myField",
//                                                                             LocalAccess("Local6", true, new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                                                             false),
//                                                                         Block(
//                                                                             [
//                                                                                 VariableDeclaration(
//                                                                                     "somethingElse",
//                                                                                     LocalAccess("Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                                     false),
//                                                                                 Int32Constant(2, true)
//                                                                             ],
//                                                                             Int32T,
//                                                                             true),
//                                                                     ],
//                                                                     Int32T,
//                                                                     true),
//                                                                 true,
//                                                                 Int32T)
//                                                         ],
//                                                         Int32T,
//                                                         true)
//                                                 }
//                                             },
//                                             Block(
//                                                 [
//                                                     VariableDeclaration(
//                                                         "myUnion",
//                                                         LocalAccess("Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                         false),
//                                                     Int32Constant(4, true)
//                                                 ],
//                                                 Int32T,
//                                                 true),
//                                             true,
//                                             Int32T)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "something", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "somethingElse", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "myUnion", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "myField", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local5", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local6", new NewLoweredConcreteTypeReference("OtherUnion", new DefId(ModuleId, $"{ModuleId}.OtherUnion"), [])),
//                             ])
//                     ])
//             },
//             {
//                 "match type pattern",
//                 """
//                 match (1) {
//                     i64 => 2
//                 }
//                 """,
//                 NewLoweredProgram(
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 Block(
//                                     [
//                                         VariableDeclaration("Local0", Int64Constant(1, true), false),
//                                         Int32Constant(2, true)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "Local0", Int64T)
//                             ])
//                     ])
//             },
//             {
//                 "match class pattern",
//                 """
//                 union MyUnion{A, B}
//                 class MyClass{pub field MyField: MyUnion}
//
//                 var a = new MyClass{MyField = MyUnion::A};
//                 match (a) {
//                     MyClass{MyField: MyUnion::A} => 1,
//                     MyClass{MyField: MyUnion::B} var something => 2,
//                 }
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
//                             ]),
//                         NewDataType(ModuleId, "MyClass",
//                             variants: [
//                                NewVariant("_classVariant", [NewField("MyField", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration("a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "MyField",
//                                                 CreateObject(
//                                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                                     "A",
//                                                     true,
//                                                     new()
//                                                     {
//                                                         {"_variantIdentifier", UInt16Constant(0, true)}
//                                                     })
//                                             }
//                                         }),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration("Local2",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                             false),
//                                         Block(
//                                             [
//                                                 VariableDeclaration(
//                                                     "Local3",
//                                                     FieldAccess(
//                                                         LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                         "MyField",
//                                                         "_classVariant",
//                                                         true,
//                                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                     false),
//                                                 SwitchInt(
//                                                     FieldAccess(
//                                                         LocalAccess("Local3", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                         "_variantIdentifier",
//                                                         "A",
//                                                         true,
//                                                         UInt16T),
//                                                     new()
//                                                     {
//                                                         {
//                                                             0,
//                                                             Int32Constant(1, true)
//                                                         },
//                                                         {
//                                                             1,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "something",
//                                                                         LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                         false),
//                                                                     Int32Constant(2, true)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         }
//                                                     },
//                                                     Unreachable(),
//                                                     true,
//                                                     Int32T)
//                                             ],
//                                             Int32T,
//                                             true),
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "something", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local3", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))
//                             ])
//                     ])
//             },
//             {
//                 "match partial class pattern with discard",
//                 """
//                 union MyUnion{A, B}
//                 class MyClass{pub field MyField: MyUnion}
//
//                 var a = new MyClass{MyField = MyUnion::A};
//                 match (a) {
//                     MyClass{MyField: MyUnion::A} => 1,
//                     _ => 2
//                 }
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)])
//                             ]),
//                         NewDataType(ModuleId, "MyClass",
//                             variants: [
//                                NewVariant("_classVariant", [NewField("MyField", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration("a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "MyField",
//                                                 CreateObject(
//                                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                                     "A",
//                                                     true,
//                                                     new()
//                                                     {
//                                                         {"_variantIdentifier", UInt16Constant(0, true)}
//                                                     })
//                                             }
//                                         }),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration(
//                                             "Local1",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                             false),
//                                         Block(
//                                             [
//                                                 VariableDeclaration(
//                                                     "Local2",
//                                                     FieldAccess(
//                                                         LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                         "MyField",
//                                                         "_classVariant",
//                                                         true,
//                                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                     false),
//                                                 SwitchInt(
//                                                     FieldAccess(
//                                                         LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                         "_variantIdentifier",
//                                                         "A",
//                                                         true,
//                                                         UInt16T),
//                                                     new()
//                                                     {
//                                                         {
//                                                             0,
//                                                             Int32Constant(1, true)
//                                                         }
//                                                     },
//                                                     Int32Constant(2, true),
//                                                     true,
//                                                     Int32T)
//                                             ],
//                                             Int32T,
//                                             true)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local1", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                             ])
//                     ])
//             },
//             {
//                 "match partial class pattern with discard 2",
//                 """
//                 union MyUnion{A, B, C}
//                 class MyClass{pub field MyField: MyUnion, pub field SecondField: MyUnion}
//
//                 var a = new MyClass {
//                     MyField = MyUnion::A,
//                     SecondField = MyUnion::B,
//                 };
//                 match (a) {
//                     MyClass { MyField: MyUnion::A, SecondField: MyUnion::A } => 1,
//                     MyClass { MyField: MyUnion::B, SecondField: MyUnion::A } => 2,
//                     MyClass { MyField: MyUnion::C, SecondField: MyUnion::A } => 4,
//                     _ => 3
//                 }
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("C", [NewField("_variantIdentifier", UInt16T)])
//                             ]),
//                         NewDataType(ModuleId, "MyClass",
//                             variants: [
//                                NewVariant(
//                                     "_classVariant",
//                                     [
//                                         NewField("MyField", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                         NewField("SecondField", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                     ])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration("a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "MyField",
//                                                 CreateObject(
//                                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                                     "A",
//                                                     true,
//                                                     new()
//                                                     {
//                                                         {"_variantIdentifier", UInt16Constant(0, true)}
//                                                     })
//                                             },
//                                             {
//                                                 "SecondField",
//                                                 CreateObject(
//                                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                                     "B",
//                                                     true,
//                                                     new()
//                                                     {
//                                                         {"_variantIdentifier", UInt16Constant(1, true)}
//                                                     })
//                                             }
//                                         }),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration(
//                                             "Local1",
//                                             LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                             false),
//                                         Block(
//                                             [
//                                                 VariableDeclaration(
//                                                     "Local5",
//                                                     FieldAccess(
//                                                         LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                         "MyField",
//                                                         "_classVariant",
//                                                         true,
//                                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                     false),
//                                                 SwitchInt(
//                                                     FieldAccess(
//                                                         LocalAccess("Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                         "_variantIdentifier",
//                                                         "A",
//                                                         true,
//                                                         UInt16T),
//                                                     new()
//                                                     {
//                                                         {
//                                                             0,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "Local2",
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                             "SecondField",
//                                                                             "_classVariant",
//                                                                             true,
//                                                                             new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                         false),
//                                                                     SwitchInt(
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                             "_variantIdentifier",
//                                                                             "A",
//                                                                             true,
//                                                                             UInt16T),
//                                                                         new()
//                                                                         {
//                                                                             {
//                                                                                 0,
//                                                                                 Int32Constant(1, true)
//                                                                             }
//                                                                         },
//                                                                         Int32Constant(3, true),
//                                                                         true,
//                                                                         Int32T)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         },
//                                                         {
//                                                             1,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "Local3",
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                             "SecondField",
//                                                                             "_classVariant",
//                                                                             true,
//                                                                             new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                         false),
//                                                                     SwitchInt(
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local3", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                             "_variantIdentifier",
//                                                                             "A",
//                                                                             true,
//                                                                             UInt16T),
//                                                                         new()
//                                                                         {
//                                                                             {
//                                                                                 0,
//                                                                                 Int32Constant(2, true)
//                                                                             }
//                                                                         },
//                                                                         Int32Constant(3, true),
//                                                                         true,
//                                                                         Int32T)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         },
//                                                         {
//                                                             2,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "Local4",
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                             "SecondField",
//                                                                             "_classVariant",
//                                                                             true,
//                                                                             new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                         false),
//                                                                     SwitchInt(
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local4", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                             "_variantIdentifier",
//                                                                             "A",
//                                                                             true,
//                                                                             UInt16T),
//                                                                         new()
//                                                                         {
//                                                                             {
//                                                                                 0,
//                                                                                 Int32Constant(4, true)
//                                                                             }
//                                                                         },
//                                                                         Int32Constant(3, true),
//                                                                         true,
//                                                                         Int32T)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         }
//                                                     },
//                                                     Int32Constant(3, true),
//                                                     true,
//                                                     Int32T)
//                                             ],
//                                             Int32T,
//                                             true)
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local1", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local3", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local4", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local5", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                             ])
//                     ])
//             },
//             {
//                 "Mixture of class and union patterns",
//                 """
//                 union MyUnion{A, B, C}
//                 class MyClass{pub field MyField: MyUnion, pub field SecondField: MyUnion}
//
//                 var a = new MyClass {
//                     MyField = MyUnion::A,
//                     SecondField = MyUnion::B,
//                 };
//                 match (a) {
//                     MyClass { MyField: MyUnion::A, SecondField: MyUnion::A } => 1,
//                     MyClass { MyField: MyUnion::A, SecondField: _          } => 2,
//                     MyClass { MyField: MyUnion::B, SecondField: MyUnion::B } => 3,
//                     MyClass { MyField: MyUnion::C, SecondField: MyUnion::A } => 4,
//                     _ => 5
//                 }
//                 """,
//                 NewLoweredProgram(
//                     types: [
//                         NewDataType(ModuleId, "MyUnion",
//                             variants: [
//                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
//                                NewVariant("C", [NewField("_variantIdentifier", UInt16T)]),
//                             ]),
//                         NewDataType(ModuleId, "MyClass",
//                             variants: [
//                                NewVariant(
//                                     "_classVariant",
//                                     [
//                                         NewField("MyField", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                         NewField("SecondField", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))
//                                     ])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "MyField",
//                                                 CreateObject(
//                                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                                     "A",
//                                                     true,
//                                                     new(){{"_variantIdentifier", UInt16Constant(0, true)}})
//                                             },
//                                             {
//                                                 "SecondField",
//                                                 CreateObject(
//                                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
//                                                     "B",
//                                                     true,
//                                                     new(){{"_variantIdentifier", UInt16Constant(1, true)}})
//                                             },
//                                         }),
//                                     false),
//                                 Block(
//                                     [
//                                         VariableDeclaration("Local1", LocalAccess("a", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])), false),
//                                         Block(
//                                             [
//                                                 VariableDeclaration("Local5", 
//                                                     FieldAccess(
//                                                         LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                         "MyField",
//                                                         "_classVariant",
//                                                         true,
//                                                         new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                     false),
//                                                 SwitchInt(
//                                                     FieldAccess(
//                                                         LocalAccess("Local5", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                         "_variantIdentifier",
//                                                         "A",
//                                                         true,
//                                                         UInt16T),
//                                                     new()
//                                                     {
//                                                         {
//                                                             0,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "Local2",
//                                                                         FieldAccess(LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                             "SecondField",
//                                                                             "_classVariant",
//                                                                             true,
//                                                                             new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                         false),
//                                                                     SwitchInt(
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local2", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                             "_variantIdentifier",
//                                                                             "A",
//                                                                             true,
//                                                                             UInt16T),
//                                                                         new()
//                                                                         {
//                                                                             {
//                                                                                 0,
//                                                                                 Int32Constant(1, true)
//                                                                             }
//                                                                         },
//                                                                         Int32Constant(2, true),
//                                                                         true,
//                                                                         Int32T)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         },
//                                                         {
//                                                             1,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "Local3",
//                                                                         FieldAccess(LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                             "SecondField",
//                                                                             "_classVariant",
//                                                                             true,
//                                                                             new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                         false),
//                                                                     SwitchInt(
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local3", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                             "_variantIdentifier",
//                                                                             "A",
//                                                                             true,
//                                                                             UInt16T),
//                                                                         new()
//                                                                         {
//                                                                             {
//                                                                                 1,
//                                                                                 Int32Constant(3, true)
//                                                                             }
//                                                                         },
//                                                                         Int32Constant(5, true),
//                                                                         true,
//                                                                         Int32T)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         },
//                                                         {
//                                                             2,
//                                                             Block(
//                                                                 [
//                                                                     VariableDeclaration(
//                                                                         "Local4",
//                                                                         FieldAccess(LocalAccess("Local1", true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                                                             "SecondField",
//                                                                             "_classVariant",
//                                                                             true,
//                                                                             new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                         false),
//                                                                     SwitchInt(
//                                                                         FieldAccess(
//                                                                             LocalAccess("Local4", true, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                                                             "_variantIdentifier",
//                                                                             "A",
//                                                                             true,
//                                                                             UInt16T),
//                                                                         new()
//                                                                         {
//                                                                             {
//                                                                                 0,
//                                                                                 Int32Constant(4, true)
//                                                                             }
//                                                                         },
//                                                                         Int32Constant(5, true),
//                                                                         true,
//                                                                         Int32T)
//                                                                 ],
//                                                                 Int32T,
//                                                                 true)
//                                                         },
//                                                     },
//                                                     Int32Constant(5, true),
//                                                     true,
//                                                     Int32T)
//                                             ],
//                                             Int32T,
//                                             true),
//                                         
//                                     ],
//                                     Int32T,
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 new NewMethodLocal("_localX", "Local1", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 // MyField 
//                                 new NewMethodLocal("_localX", "Local5", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 // SecondField
//                                 new NewMethodLocal("_localX", "Local2", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local3", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                                 new NewMethodLocal("_localX", "Local4", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
//                             ])
//                     ])
//             }
        };
    }
}
