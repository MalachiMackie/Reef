using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests.PatternMatching;

public class MatchesTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchesAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "MatchesTests";

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
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
            // {
            //     "matches - type pattern with variable declaration",
            //     """
            //     var b = 1 matches i64 var a;
            //     """,
            //     NewLoweredProgram(
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration("a", Int64Constant(1, true) ,false),
            //                                 BoolConstant(true, true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "a", Int64_t)
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union variant pattern",
            //     """
            //     union MyUnion{A, B};
            //     var a = MyUnion::A;
            //     var b = a matches MyUnion::B;
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant("A", [Field("_variantIdentifier", UInt16_t)]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "A",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(0, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local2",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 UInt16Equals(
            //                                     FieldAccess(
            //                                         LocalAccess(
            //                                             "Local2",
            //                                             true,
            //                                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                         "_variantIdentifier",
            //                                         "B",
            //                                         true,
            //                                         UInt16_t),
            //                                     UInt16Constant(1, true),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit(),
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "Local2", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")))
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union variant pattern with variable declaration",
            //     """
            //     union MyUnion{A, B};
            //     var a = MyUnion::A;
            //     var b = a matches MyUnion::B var c;
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant("A", [Field("_variantIdentifier", UInt16_t)]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "A",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(0, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "c",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 UInt16Equals(
            //                                     FieldAccess(
            //                                         LocalAccess(
            //                                             "c",
            //                                             true,
            //                                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                         "_variantIdentifier",
            //                                         "B",
            //                                         true,
            //                                         UInt16_t),
            //                                     UInt16Constant(1, true),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit(),
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "c", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")))
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union tuple pattern",
            //     """
            //     union MyUnion{A(i64), B}
            //     var a = MyUnion::B;
            //     var b = a matches MyUnion::A(_);
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant("A", [Field("_variantIdentifier", UInt16_t), Field("Item0", Int64_t)]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
            //                 [
            //                     NewMethodReturn(
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "A",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(0, true)}, {"Item0", LoadArgument(0, true, Int64_t)}}))
            //                 ],
            //                 parameters: [Int64_t],
            //                 returnType: ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local2",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 BoolAnd(
            //                                     UInt16Equals(
            //                                         FieldAccess(
            //                                             LocalAccess(
            //                                                 "Local2",
            //                                                 true,
            //                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                             "_variantIdentifier",
            //                                             "A",
            //                                             true,
            //                                             UInt16_t),
            //                                         UInt16Constant(0, true),
            //                                         true),
            //                                     Block(
            //                                         [
            //                                             VariableDeclaration(
            //                                                 "Local3",
            //                                                 FieldAccess(
            //                                                     LocalAccess("Local2",
            //                                                         true,
            //                                                         ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                     "Item0",
            //                                                     "A",
            //                                                     true,
            //                                                     Int64_t),
            //                                                 false),
            //                                             BoolConstant(true, true)
            //                                         ],
            //                                         BooleanT,
            //                                         true),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "Local2", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "Local3", Int64_t)
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union tuple pattern with variable declaration",
            //     """
            //     union MyUnion{A(i64), B}
            //     var a = MyUnion::B;
            //     var b = a matches MyUnion::A(_) var c;
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant("A", [Field("_variantIdentifier", UInt16_t), Field("Item0", Int64_t)]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
            //                 [
            //                     NewMethodReturn(
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "A",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(0, true)}, {"Item0", LoadArgument(0, true, Int64_t)}}))
            //                 ],
            //                 parameters: [Int64_t],
            //                 returnType: ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "c",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 BoolAnd(
            //                                     UInt16Equals(
            //                                         FieldAccess(
            //                                             LocalAccess(
            //                                                 "c",
            //                                                 true,
            //                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                             "_variantIdentifier",
            //                                             "A",
            //                                             true,
            //                                             UInt16_t),
            //                                         UInt16Constant(0, true),
            //                                         true),
            //                                     Block(
            //                                         [
            //                                             VariableDeclaration(
            //                                                 "Local3",
            //                                                 FieldAccess(
            //                                                     LocalAccess("c",
            //                                                         true,
            //                                                         ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                     "Item0",
            //                                                     "A",
            //                                                     true,
            //                                                     Int64_t),
            //                                                 false),
            //                                             BoolConstant(true, true)
            //                                         ],
            //                                         BooleanT,
            //                                         true),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "c", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "Local3", Int64_t)
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union tuple pattern with multiple members ",
            //     """
            //     union MyUnion{A(i64, string, bool), B}
            //     var a = MyUnion::B;
            //     var b = a matches MyUnion::A(_, var c, _);
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant(
            //                         "A",
            //                         [
            //                             Field("_variantIdentifier", UInt16_t),
            //                             Field("Item0", Int64_t),
            //                             Field("Item1", StringType),
            //                             Field("Item2", BooleanT),
            //                         ]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
            //                 [
            //                     NewMethodReturn(
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "A",
            //                             true,
            //                             new() {
            //                                 {"Item0", LoadArgument(0, true, Int64_t)},
            //                                 {"Item1", LoadArgument(1, true, StringType)},
            //                                 {"Item2", LoadArgument(2, true, BooleanT)},
            //                                 {"_variantIdentifier", UInt16Constant(0, true)},
            //                             }))
            //                 ],
            //                 parameters: [Int64_t, StringType, BooleanT],
            //                 returnType: ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local3",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 BoolAnd(
            //                                     UInt16Equals(
            //                                         FieldAccess(
            //                                             LocalAccess(
            //                                                 "Local3",
            //                                                 true,
            //                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                             "_variantIdentifier",
            //                                             "A",
            //                                             true,
            //                                             UInt16_t),
            //                                         UInt16Constant(0, true),
            //                                         true),
            //                                     BoolAnd(
            //                                         Block(
            //                                             [
            //                                                 VariableDeclaration(
            //                                                     "Local4",
            //                                                     FieldAccess(
            //                                                         LocalAccess("Local3",
            //                                                             true,
            //                                                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                         "Item0",
            //                                                         "A",
            //                                                         true,
            //                                                         Int64_t),
            //                                                     false),
            //                                                 BoolConstant(true, true)
            //                                             ],
            //                                             BooleanT,
            //                                             true),
            //                                         BoolAnd(
            //                                             Block(
            //                                                 [
            //                                                     VariableDeclaration(
            //                                                         "c",
            //                                                         FieldAccess(
            //                                                             LocalAccess("Local3",
            //                                                                 true,
            //                                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                             "Item1",
            //                                                             "A",
            //                                                             true,
            //                                                             StringType),
            //                                                         false),
            //                                                     BoolConstant(true, true)
            //                                                 ],
            //                                                 BooleanT,
            //                                                 true),
            //                                             Block(
            //                                                 [
            //                                                     VariableDeclaration(
            //                                                         "Local5",
            //                                                         FieldAccess(
            //                                                             LocalAccess("Local3",
            //                                                                 true,
            //                                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                             "Item2",
            //                                                             "A",
            //                                                             true,
            //                                                             BooleanT),
            //                                                         false),
            //                                                     BoolConstant(true, true)
            //                                                 ],
            //                                                 BooleanT,
            //                                                 true),
            //                                             true),
            //                                         true
            //                                     ),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "c", StringType),
            //                     new NewMethodLocal("_localX", "Local3", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "Local4", Int64_t),
            //                     new NewMethodLocal("_localX", "Local5", BooleanT),
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union class variant pattern",
            //     """
            //     union MyUnion{
            //         A {field FieldA: i64},
            //         B
            //     }
            //     var a = MyUnion::B;
            //     var b = a matches MyUnion::A{FieldA: _};
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant(
            //                         "A",
            //                         [
            //                             Field("_variantIdentifier", UInt16_t),
            //                             Field("FieldA", Int64_t)
            //                         ]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local2",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 BoolAnd(
            //                                     UInt16Equals(
            //                                         FieldAccess(
            //                                             LocalAccess(
            //                                                 "Local2",
            //                                                 true,
            //                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                             "_variantIdentifier",
            //                                             "A",
            //                                             true,
            //                                             UInt16_t),
            //                                         UInt16Constant(0, true),
            //                                         true),
            //                                     Block(
            //                                         [
            //                                             VariableDeclaration(
            //                                                 "Local3",
            //                                                 FieldAccess(
            //                                                     LocalAccess("Local2",
            //                                                         true,
            //                                                         ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                     "FieldA",
            //                                                     "A",
            //                                                     true,
            //                                                     Int64_t),
            //                                                 false),
            //                                             BoolConstant(true, true)
            //                                         ],
            //                                         BooleanT,
            //                                         true),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "Local2", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "Local3", Int64_t)
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union class variant pattern with discarded fields",
            //     """
            //     union MyUnion{
            //         A {field FieldA: i64},
            //         B
            //     }
            //     var a = MyUnion::B;
            //     var b = a matches MyUnion::A{_};
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant(
            //                         "A",
            //                         [
            //                             Field("_variantIdentifier", UInt16_t),
            //                             Field("FieldA", Int64_t)
            //                         ]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local2",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 UInt16Equals(
            //                                     FieldAccess(
            //                                         LocalAccess(
            //                                             "Local2",
            //                                             true,
            //                                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                         "_variantIdentifier",
            //                                         "A",
            //                                         true,
            //                                         UInt16_t),
            //                                     UInt16Constant(0, true),
            //                                     true),
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "Local2", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                 ])
            //         ])
            // },
            // {
            //     "matches - union class variant pattern multiple fields",
            //     """
            //     union MyUnion{
            //         A {field FieldA: i64, field FieldB: string, field FieldC: bool},
            //         B
            //     }
            //     var a = MyUnion::B;
            //     var b = a matches MyUnion::A{FieldA: var c, FieldB, FieldC: _};
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant(
            //                         "A",
            //                         [
            //                             Field("_variantIdentifier", UInt16_t),
            //                             Field("FieldA", Int64_t),
            //                             Field("FieldB", StringType),
            //                             Field("FieldC", BooleanT)
            //                         ]),
            //                     Variant("B", [Field("_variantIdentifier", UInt16_t)])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local4",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                     false),
            //                                 BoolAnd(
            //                                     UInt16Equals(
            //                                         FieldAccess(
            //                                             LocalAccess(
            //                                                 "Local4",
            //                                                 true,
            //                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                             "_variantIdentifier",
            //                                             "A",
            //                                             true,
            //                                             UInt16_t),
            //                                         UInt16Constant(0, true),
            //                                         true),
            //                                     BoolAnd(
            //                                         Block(
            //                                             [
            //                                                 VariableDeclaration(
            //                                                     "c",
            //                                                     FieldAccess(
            //                                                         LocalAccess("Local4",
            //                                                             true,
            //                                                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                         "FieldA",
            //                                                         "A",
            //                                                         true,
            //                                                         Int64_t),
            //                                                     false),
            //                                                 BoolConstant(true, true)
            //                                             ],
            //                                             BooleanT,
            //                                             true),
            //                                         BoolAnd(
            //                                             Block(
            //                                                 [
            //                                                     VariableDeclaration(
            //                                                         "FieldB",
            //                                                         FieldAccess(
            //                                                             LocalAccess("Local4",
            //                                                                 true,
            //                                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                             "FieldB",
            //                                                             "A",
            //                                                             true,
            //                                                             StringType),
            //                                                         false),
            //                                                     BoolConstant(true, true)
            //                                                 ],
            //                                                 BooleanT,
            //                                                 true),
            //                                             Block(
            //                                                 [
            //                                                     VariableDeclaration(
            //                                                         "Local5",
            //                                                         FieldAccess(
            //                                                             LocalAccess("Local4",
            //                                                                 true,
            //                                                                 ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                                                             "FieldC",
            //                                                             "A",
            //                                                             true,
            //                                                             BooleanT),
            //                                                         false),
            //                                                     BoolConstant(true, true)
            //                                                 ],
            //                                                 BooleanT,
            //                                                 true),
            //                                             true),
            //                                         true
            //                                     ),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "c", Int64_t),
            //                     new NewMethodLocal("_localX", "FieldB", StringType),
            //                     new NewMethodLocal("_localX", "Local4", ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                     new NewMethodLocal("_localX", "Local5", BooleanT)
            //                 ])
            //         ])
            // },
            // {
            //     "matches - class pattern",
            //     """
            //     class MyClass { pub field Field0: i64, pub field Field1: i64, pub field Field2: bool }
            //     var a = new MyClass{Field0 = 0, Field1 = 1, Field2 = true };
            //     var b = a matches MyClass {Field0: var c, Field1: _, Field2: _};
            //     """,
            //     NewLoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyClass",
            //                 variants: [
            //                     Variant("_classVariant",
            //                         [
            //                             Field("Field0", Int64_t),
            //                             Field("Field1", Int64_t),
            //                             Field("Field2", BooleanT)
            //                         ])
            //                 ])
            //         ],
            //         methods: [
            //             NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     VariableDeclaration(
            //                         "a",
            //                         CreateObject(
            //                             ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")),
            //                             "_classVariant",
            //                             true,
            //                             new()
            //                             {
            //                                 {"Field0", Int64Constant(0, true)},
            //                                 {"Field1", Int64Constant(1, true)},
            //                                 {"Field2", BoolConstant(true, true)},
            //                             }),
            //                         false),
            //                     VariableDeclaration(
            //                         "b",
            //                         Block(
            //                             [
            //                                 VariableDeclaration(
            //                                     "Local3",
            //                                     LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
            //                                     false),
            //                                 BoolAnd(
            //                                     Block(
            //                                         [
            //                                             VariableDeclaration("c",
            //                                                 FieldAccess(
            //                                                     LocalAccess(
            //                                                         "Local3",
            //                                                         true,
            //                                                         ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
            //                                                     "Field0",
            //                                                     "_classVariant",
            //                                                     true,
            //                                                     Int64_t),
            //                                                 false),
            //                                             BoolConstant(true, true)
            //                                         ],
            //                                         BooleanT,
            //                                         true),
            //                                     BoolAnd(
            //                                         Block(
            //                                             [
            //                                                 VariableDeclaration(
            //                                                     "Local4",
            //                                                     FieldAccess(
            //                                                         LocalAccess(
            //                                                             "Local3",
            //                                                             true,
            //                                                             ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
            //                                                         "Field1",
            //                                                         "_classVariant",
            //                                                         true,
            //                                                         Int64_t),
            //                                                     false),
            //                                                 BoolConstant(true, true)
            //                                             ],
            //                                             BooleanT,
            //                                             true),
            //                                         Block(
            //                                             [
            //                                                 VariableDeclaration(
            //                                                     "Local5",
            //                                                     FieldAccess(
            //                                                         LocalAccess(
            //                                                             "Local3",
            //                                                             true,
            //                                                             ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
            //                                                         "Field2",
            //                                                         "_classVariant",
            //                                                         true,
            //                                                         BooleanT),
            //                                                     false),
            //                                                 BoolConstant(true, true)
            //                                             ],
            //                                             BooleanT,
            //                                             true),
            //                                         true),
            //                                     true)
            //                             ],
            //                             BooleanT,
            //                             true),
            //                         false),
            //                     NewMethodReturnUnit()
            //                 ],
            //                 locals: [
            //                     new NewMethodLocal("_localX", "a", ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
            //                     new NewMethodLocal("_localX", "b", BooleanT),
            //                     new NewMethodLocal("_localX", "c", Int64_t),
            //                     new NewMethodLocal("_localX", "Local3", ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
            //                     new NewMethodLocal("_localX", "Local4", Int64_t),
            //                     new NewMethodLocal("_localX", "Local5", BooleanT),
            //                 ])
            //         ])
            // }
        };
    }
}
