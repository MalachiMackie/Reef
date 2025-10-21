using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ControlFlowTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ControlFlowAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string _moduleId = "ControlFlowTests";

    [Fact]
    public void SingleTest()
    {
        var source = """
                var mut a = 0;
                if (true)
                {
                    a = 1;
                }
                else if (true)
                {
                    a = 2;
                }
                """;
                var expectedProgram = LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a", Int64Constant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new()
                                    {
                                        {
                                            0,
                                            SwitchInt(
                                                CastBoolToInt(BoolConstant(true, true), true),
                                                new()
                                                {
                                                    {
                                                        0,
                                                        Noop()
                                                    }
                                                },
                                                Block([LocalValueAssignment("a", Int64Constant(2, true), false, Int64_t)], Unit, false),
                                                false,
                                                Unit)
                                        }
                                    },
                                    Block([LocalValueAssignment("a", Int64Constant(1, true), false, Int64_t)], Unit, false),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int64_t)])
                    ]);

        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);
        
        PrintPrograms(expectedProgram, loweredProgram, false, false);
        
        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "FallOut operator",
                """
                fn SomeFn(): result::<i64, i64>
                {
                    return error(1);
                }

                fn OtherFn(): result::<i64, i64>
                {
                    var a = SomeFn()?;
                    return ok(a);
                }
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                MethodReturn(
                                    MethodCall(
                                        FunctionReference(DefId.Result_Create_Error, "result__Create__Error", [Int64_t, Int64_t]),
                                        [Int64Constant(1, true)],
                                        true,
                                        ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])))
                            ],
                            returnType: ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])),
                        Method(new DefId(_moduleId, $"{_moduleId}.OtherFn"), "OtherFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    Block(
                                        [
                                            LocalValueAssignment(
                                                "Local1",
                                                MethodCall(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn", []),
                                                    [],
                                                    true,
                                                    ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])),
                                                false,
                                                ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])),
                                            SwitchInt(
                                                FieldAccess(
                                                    LocalAccess("Local1", true, ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])),
                                                    "_variantIdentifier",
                                                    "Ok",
                                                    true,
                                                    Int64_t),
                                                new()
                                                {
                                                    {
                                                        0,
                                                        FieldAccess(
                                                            LocalAccess("Local1", true, ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])),
                                                            "Item0",
                                                            "Ok",
                                                            true,
                                                            Int64_t)
                                                        }
                                                },
                                                MethodReturn(
                                                    MethodCall(
                                                        FunctionReference(DefId.Result_Create_Error, "result__Create__Error", [Int64_t, Int64_t]),
                                                        [
                                                            FieldAccess(
                                                                LocalAccess("Local1", true, ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])),
                                                                "Item0",
                                                                "Error",
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        true,
                                                        ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t]))),
                                                valueUseful: true,
                                                resolvedType: Int64_t)
                                        ],
                                        Int64_t,
                                        true),
                                    false),
                                MethodReturn(
                                    MethodCall(
                                        FunctionReference(DefId.Result_Create_Ok, "result__Create__Ok", [Int64_t, Int64_t]),
                                        [LocalAccess("a", true, Int64_t)],
                                        true,
                                        ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t])))
                            ],
                            locals: [
                                Local("a", Int64_t),
                                Local("Local1", ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t]))
                            ],
                            returnType: ConcreteTypeReference("result", DefId.Result, [Int64_t, Int64_t]))
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration("a", Int64Constant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new() {
                                        {0, Noop()}
                                    },
                                    LocalValueAssignment("a", Int64Constant(1, true), false, Int64_t),
                                    false,
                                    Unit),
                                MethodReturnUnit(),
                            ],
                            locals: [Local("a", Int64_t)])
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration("a", Int64Constant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new(){
                                        {
                                            0,
                                            Block(
                                                [LocalValueAssignment("a", Int64Constant(2, true), false, Int64_t)],
                                                Unit,
                                                false)},
                                    },
                                    Block([LocalValueAssignment("a", Int64Constant(1, true), false, Int64_t)], Unit, false),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int64_t)])
                    ])
            },
            {
                "assign if else to variable",
                """
                var mut a = 1;
                var b = if (true) { a = 2; } else { a = 3; };
                // var b = if (true) { a = 2; 4 } else { a = 3; 5 };
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a", Int64Constant(1, true), false),
                                VariableDeclaration(
                                    "b",
                                    SwitchInt(
                                        CastBoolToInt(BoolConstant(true, true), true),
                                        new()
                                        {
                                            {
                                                0,
                                                Block(
                                                    [
                                                        LocalValueAssignment(
                                                            "a",
                                                            Int64Constant(3, true),
                                                            true,
                                                            Int64_t)
                                                    ],
                                                    Unit,
                                                    true)
                                            }
                                        },
                                        Block(
                                            [LocalValueAssignment("a", Int64Constant(2, true), true, Int64_t)],
                                            Unit,
                                            true),
                                        true,
                                        Unit),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", Int64_t),
                                Local("b", Unit)
                            ])
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a", Int64Constant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new()
                                    {
                                        {
                                            0,
                                            SwitchInt(
                                                CastBoolToInt(BoolConstant(true, true), true),
                                                new()
                                                {
                                                    {
                                                        0,
                                                        Noop()
                                                    }
                                                },
                                                Block([LocalValueAssignment("a", Int64Constant(2, true), false, Int64_t)], Unit, false),
                                                false,
                                                Unit)
                                        }
                                    },
                                    Block([LocalValueAssignment("a", Int64Constant(1, true), false, Int64_t)], Unit, false),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int64_t)])
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration("a", Int64Constant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new()
                                    {
                                        {
                                            0,
                                            SwitchInt(
                                                CastBoolToInt(BoolConstant(true, true), true),
                                                new()
                                                {
                                                    {
                                                        0,
                                                        Block([LocalValueAssignment("a", Int64Constant(3, true), false, Int64_t)], Unit, false)
                                                    }
                                                },
                                                Block([LocalValueAssignment("a", Int64Constant(2, true), false, Int64_t)], Unit, false),
                                                false,
                                                Unit)
                                        }
                                    },
                                    Block([LocalValueAssignment("a", Int64Constant(1, true), false, Int64_t)], Unit, false),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int64_t)])
                    ])
            },
        };
    }
}
