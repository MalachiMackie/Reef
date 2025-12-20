using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ControlFlowTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ControlFlowAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
    [Fact]
    public void Single()
    {
        var source = """
                 var mut a = 0;
                 while (true) {
                    a = a + 1;
                    if (a > 25) {
                        break;
                    }
                 }
                 """;
        var expectedProgram = LoweredProgram(
        [
            Method(
                new DefId(ModuleId, $"{ModuleId}._Main"),
                "_Main",
                [
                    new BasicBlock(
                        new BasicBlockId("bb0"),
                        [
                            new Assign(new Local("_local0"), new Use(new IntConstant(0, 4))),
                        ],
                        new GoTo(new BasicBlockId("bb1"))),
                    new BasicBlock(
                        new BasicBlockId("bb1"),
                        [],
                        new SwitchInt(
                            new BoolConstant(true),
                            new Dictionary<int, BasicBlockId>
                            {
                                { 0, new BasicBlockId("bb5") }
                            }, new BasicBlockId("bb2"))),
                    new BasicBlock(
                        new BasicBlockId("bb2"),
                        [
                            new Assign(
                                new Local("_local0"),
                                new BinaryOperation(
                                    new Copy(new Local("_local0")),
                                    new IntConstant(1, 4),
                                    BinaryOperationKind.Add)),
                            new Assign(
                                new Local("_local1"),
                                new BinaryOperation(
                                    new Copy(new Local("_local0")),
                                    new IntConstant(25, 4),
                                    BinaryOperationKind.GreaterThan))
                        ],
                        new SwitchInt(
                            new Copy(new Local("_local1")),
                            new Dictionary<int, BasicBlockId>
                            {
                                { 0, new BasicBlockId("bb4") }
                            },
                            new BasicBlockId("bb3"))),
                    new BasicBlock(
                        new BasicBlockId("bb3"),
                        [],
                        new GoTo(new BasicBlockId("bb5"))),
                    new BasicBlock(
                        new BasicBlockId("bb4"),
                        [],
                        new GoTo(new BasicBlockId("bb1"))),
                    new BasicBlock(
                        new BasicBlockId("bb5"),
                        [],
                        new Return())
                ],
                Unit,
                locals:
                [
                    new MethodLocal("_local0", "a", Int32T),
                    new MethodLocal("_local1", null, BooleanT)
                ])
        ]);
        
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ControlFlowTests";

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
             {
                 "FallOut operator",
                 """
                 fn SomeFn(): result::<i32, i64>
                 {
                     return error(1);
                 }

                 fn OtherFn(): result::<i64, i64>
                 {
                     var a = SomeFn()?;
                     return ok(1);
                 }
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             DefId.Result_Create_Error,
                                             [Int32T, Int64T]),
                                         [new IntConstant(1, 8)],
                                         new Local("_returnValue"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             returnType: new LoweredConcreteTypeReference("result", DefId.Result, [Int32T, Int64T])),
                         Method(new DefId(ModuleId, $"{ModuleId}.OtherFn"), "OtherFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), []),
                                         [],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new Copy(
                                             new Field(
                                                 new Local("_local1"),
                                                 "_variantIdentifier",
                                                 "Ok")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb3") }
                                         },
                                         new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             DefId.Result_Create_Error,
                                             [Int64T, Int64T]),
                                         [new Copy(new Field(new Local("_local1"), "Item0", "Error"))],
                                         new Local("_returnValue"),
                                         new BasicBlockId("bb4"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new Copy(new Field(new Local("_local1"), "Item0", "Ok"))))
                                     ],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             DefId.Result_Create_Ok,
                                             [Int64T, Int64T]),
                                         [new IntConstant(1, 8)],
                                         new Local("_returnValue"),
                                         new BasicBlockId("bb4"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"), [], new Return())
                             ],
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                                 new MethodLocal("_local1", null, new LoweredConcreteTypeReference("result", DefId.Result, [Int32T, Int64T])),
                             ],
                             returnType: new LoweredConcreteTypeReference("result", DefId.Result, [Int64T, Int64T]))
                     ])
             },
             {
                 "simple if",
                 """
                 var mut a = 0;
                 if (true) a = 1;
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(0, 4)))
                                     ],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             {0, new BasicBlockId("bb2")}
                                         },
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb2"))),
                                 new BasicBlock(new BasicBlockId("bb2"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "if else",
                 """
                 var mut a = 0;
                 if (true) {a = 1}
                 else {a = 2}
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(0, 4)))],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2")}
                                         },
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "if else",
                 """
                 var a = if (true) 1 else 2;
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2")}
                                         },
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "if else if",
                 """
                 var mut a = 0;
                 if (true)
                 {
                     a = 1;
                 }
                 else if (true)
                 {
                     a = 2;
                 }
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(0, 4)))],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb2") }
                                         },
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb4"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb4")}
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb4"))),
                                 new BasicBlock(new BasicBlockId("bb4"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "if else if else",
                 """
                 var mut a = 0;
                 if (true)
                 {a = 1;}
                 else if (true)
                 {a = 2;}
                 else
                 {a = 3;}
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(0, 4)))],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             {0, new BasicBlockId("bb2")}
                                         },
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(1, 4)))],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb4") }
                                         }, new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(2, 4)))],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [new Assign(new Local("_local0"), new Use(new IntConstant(3, 4)))],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(new BasicBlockId("bb5"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "basic while",
                 """
                 var mut a = 0;
                 while (a < 25) {
                    a = a + 1;
                 }
                 """,
                 LoweredProgram(
                     [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}._Main"),
                             "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(0, 4))),
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(25, 4),
                                                 BinaryOperationKind.LessThan))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Local("_local1")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb3") }
                                         }, new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(1, 4),
                                                 BinaryOperationKind.Add))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                                 new MethodLocal("_local1", null, BooleanT),
                             ])
                     ])
             },
             {
                 "while break",
                 """
                 var mut a = 0;
                 while (true) {
                    a = a + 1;
                    if (a > 25) {
                        break;
                    }
                 }
                 """,
                 LoweredProgram(
                     [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}._Main"),
                             "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(0, 4))),
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [],
                                     new SwitchInt(
                                         new BoolConstant(true),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb5") }
                                         }, new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(1, 4),
                                                 BinaryOperationKind.Add)),
                                         new Assign(
                                             new Local("_local1"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(25, 4),
                                                 BinaryOperationKind.GreaterThan))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Local("_local1")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             {0, new BasicBlockId("bb4")}
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [],
                                     new GoTo(new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [],
                                     new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                                 new MethodLocal("_local1", null, BooleanT)
                             ])
                     ])
             },
             {
                 "while continue",
                 """
                 var mut a = 0;
                 while(a < 10) {
                    a = a + 1;
                    if (a == 5) {
                        continue;
                    }
                    printf("hi")
                 }
                 """,
                 LoweredProgram(
                     [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}._Main"),
                             "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(new Local("_local0"), new Use(new IntConstant(0, 4))),
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(10, 4),
                                                 BinaryOperationKind.LessThan))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Local("_local1")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             { 0, new BasicBlockId("bb6") }
                                         },
                                         new BasicBlockId("bb2"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb2"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(1, 4),
                                                 BinaryOperationKind.Add)),
                                         new Assign(
                                             new Local("_local2"),
                                             new BinaryOperation(
                                                 new Copy(new Local("_local0")),
                                                 new IntConstant(5, 4),
                                                 BinaryOperationKind.Equal))
                                     ],
                                     new SwitchInt(
                                         new Copy(new Local("_local2")),
                                         new Dictionary<int, BasicBlockId>
                                         {
                                             {0, new BasicBlockId("bb4")}
                                         },
                                         new BasicBlockId("bb3"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb3"),
                                     [],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(DefId.Printf, []),
                                         [new StringConstant("hi")],
                                         new Local("_local3"),
                                         new BasicBlockId("bb5"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb5"),
                                     [],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb6"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", Int32T),
                                 new MethodLocal("_local1", null, BooleanT),
                                 new MethodLocal("_local2", null, BooleanT),
                                 new MethodLocal("_local3", null, Unit),
                             ])
                     ])
             }
        };
    }
}
