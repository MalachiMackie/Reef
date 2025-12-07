using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ControlFlowTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ControlFlowAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ControlFlowTests";

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
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
                 NewLoweredProgram(
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(
                                             DefId.Result_Create_Error,
                                             [Int32T, Int64T]),
                                         [new IntConstant(1, 8)],
                                         new Local("_returnValue"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             returnType: new NewLoweredConcreteTypeReference("result", DefId.Result, [Int32T, Int64T])),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.OtherFn"), "OtherFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), []),
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
                                         new NewLoweredFunctionReference(
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
                                         new NewLoweredFunctionReference(
                                             DefId.Result_Create_Ok,
                                             [Int64T, Int64T]),
                                         [new IntConstant(1, 8)],
                                         new Local("_returnValue"),
                                         new BasicBlockId("bb4"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb4"), [], new Return())
                             ],
                             locals: [
                                 new NewMethodLocal("_local0", "a", Int32T),
                                 new NewMethodLocal("_local1", null, new NewLoweredConcreteTypeReference("result", DefId.Result, [Int32T, Int64T])),
                             ],
                             returnType: new NewLoweredConcreteTypeReference("result", DefId.Result, [Int64T, Int64T]))
                     ])
             },
             {
                 "simple if",
                 """
                 var mut a = 0;
                 if (true) a = 1;
                 """,
                 NewLoweredProgram(
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
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
                             locals: [new NewMethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "if else",
                 """
                 var mut a = 0;
                 if (true) {a = 1}
                 else {a = 2}
                 """,
                 NewLoweredProgram(
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
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
                             locals: [new NewMethodLocal("_local0", "a", Int32T)])
                     ])
             },
             {
                 "if else",
                 """
                 var a = if (true) 1 else 2;
                 """,
                 NewLoweredProgram(
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
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
                             locals: [new NewMethodLocal("_local0", "a", Int32T)])
                     ])
             },
//             {
//                 "assign if else to variable",
//                 """
//                 var mut a = 1;
//                 var b = if (true) { a = 2; } else { a = 3; };
//                 // var b = if (true) { a = 2; 4 } else { a = 3; 5 };
//                 """,
//                 NewLoweredProgram(
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a", Int32Constant(1, true), false),
//                                 VariableDeclaration(
//                                     "b",
//                                     SwitchInt(
//                                         CastBoolToInt(BoolConstant(true, true), true),
//                                         new()
//                                         {
//                                             {
//                                                 0,
//                                                 Block(
//                                                     [
//                                                         new NewMethodLocalValueAssignment(
//                                                             "a",
//                                                             Int32Constant(3, true),
//                                                             true,
//                                                             Int32T)
//                                                     ],
//                                                     Unit,
//                                                     true)
//                                             }
//                                         },
//                                         Block(
//                                             [LocalValueAssignment("a", Int32Constant(2, true), true, Int32T)],
//                                             Unit,
//                                             true),
//                                         true,
//                                         Unit),
//                                     false),
//                                 NewMethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", Int32T),
//                                 new NewMethodLocal("_localX", "b", Unit)
//                             ])
//                     ])
//             },
//             {
//                 "if else if",
//                 """
//                 var mut a = 0;
//                 if (true)
//                 {
//                     a = 1;
//                 }
//                 else if (true)
//                 {
//                     a = 2;
//                 }
//                 """,
//                 NewLoweredProgram(
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a", Int32Constant(0, true), false),
//                                 SwitchInt(
//                                     CastBoolToInt(BoolConstant(true, true), true),
//                                     new()
//                                     {
//                                         {
//                                             0,
//                                             SwitchInt(
//                                                 CastBoolToInt(BoolConstant(true, true), true),
//                                                 new()
//                                                 {
//                                                     {
//                                                         0,
//                                                         Noop()
//                                                     }
//                                                 },
//                                                 Block([LocalValueAssignment("a", Int32Constant(2, true), false, Int32T)], Unit, false),
//                                                 false,
//                                                 Unit)
//                                         }
//                                     },
//                                     Block([LocalValueAssignment("a", Int32Constant(1, true), false, Int32T)], Unit, false),
//                                     false,
//                                     Unit),
//                                 NewMethodReturnUnit()
//                             ],
//                             locals: [new NewMethodLocal("_localX", "a", Int32T)])
//                     ])
//             },
//             {
//                 "if else if else",
//                 """
//                 var mut a = 0;
//                 if (true)
//                 {a = 1;}
//                 else if (true)
//                 {a = 2;}
//                 else
//                 {a = 3;}
//                 """,
//                 NewLoweredProgram(
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration("a", Int32Constant(0, true), false),
//                                 SwitchInt(
//                                     CastBoolToInt(BoolConstant(true, true), true),
//                                     new()
//                                     {
//                                         {
//                                             0,
//                                             SwitchInt(
//                                                 CastBoolToInt(BoolConstant(true, true), true),
//                                                 new()
//                                                 {
//                                                     {
//                                                         0,
//                                                         Block([LocalValueAssignment("a", Int32Constant(3, true), false, Int32T)], Unit, false)
//                                                     }
//                                                 },
//                                                 Block([LocalValueAssignment("a", Int32Constant(2, true), false, Int32T)], Unit, false),
//                                                 false,
//                                                 Unit)
//                                         }
//                                     },
//                                     Block([LocalValueAssignment("a", Int32Constant(1, true), false, Int32T)], Unit, false),
//                                     false,
//                                     Unit),
//                                 NewMethodReturnUnit()
//                             ],
//                             locals: [new NewMethodLocal("_localX", "a", Int32T)])
//                     ])
//             },
        };
    }
}
