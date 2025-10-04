
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
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

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
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a", IntConstant(0, true), false),
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
                                                Block([LocalValueAssignment("a", IntConstant(2, true), true, Int)], Unit, true),
                                                false,
                                                Unit)
                                        }
                                    },
                                    Block([LocalValueAssignment("a", IntConstant(1, true), true, Int)], Unit, true),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int)])
                    ]);

        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        
        PrintPrograms(expectedProgram, loweredProgram, false, false);
        
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "FallOut operator",
                """
                fn SomeFn(): result::<int, int>
                {
                    return error(1);
                }

                fn OtherFn(): result::<int, int>
                {
                    var a = SomeFn()?;
                    return ok(a);
                }
                """,
                LoweredProgram(
                    methods: [
                        Method(
                            "OtherFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    Block(
                                        [
                                            LocalValueAssignment(
                                                "Local1",
                                                MethodCall(
                                                    FunctionReference("SomeFn", []),
                                                    [],
                                                    true,
                                                    ConcreteTypeReference("result", [Int, Int])),
                                                false,
                                                ConcreteTypeReference("result", [Int, Int])),
                                            SwitchInt(
                                                FieldAccess(
                                                    LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                    "_variantIdentifier",
                                                    "Ok",
                                                    true,
                                                    Int),
                                                new()
                                                {
                                                    {
                                                        0,
                                                        FieldAccess(
                                                            LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                            "Item0",
                                                            "Ok",
                                                            true,
                                                            Int)
                                                        }
                                                },
                                                MethodReturn(
                                                    MethodCall(
                                                        FunctionReference("result_Create_Error", [Int, Int]),
                                                        [
                                                            FieldAccess(
                                                                LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                                "Item0",
                                                                "Error",
                                                                true,
                                                                Int)
                                                        ],
                                                        true,
                                                        ConcreteTypeReference("result", [Int, Int]))),
                                                valueUseful: true,
                                                resolvedType: Int)
                                        ],
                                        Int,
                                        true),
                                    false),
                                MethodReturn(
                                    MethodCall(
                                        FunctionReference("result_Create_Ok", [Int, Int]),
                                        [LocalAccess("a", true, Int)],
                                        true,
                                        ConcreteTypeReference("result", [Int, Int])))
                            ],
                            locals: [
                                Local("a", Int),
                                Local("Local1", ConcreteTypeReference("result", [Int, Int]))
                            ],
                            returnType: ConcreteTypeReference("result", [Int, Int])),
                        Method(
                            "SomeFn",
                            [
                                MethodReturn(
                                    MethodCall(
                                        FunctionReference("result_Create_Error", [Int, Int]),
                                        [IntConstant(1, true)],
                                        true,
                                        ConcreteTypeReference("result", [Int, Int])))
                            ],
                            returnType: ConcreteTypeReference("result", [Int, Int]))
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
                        Method("_Main",
                            [
                                VariableDeclaration("a", IntConstant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new() {
                                        {0, Noop()}
                                    },
                                    LocalValueAssignment("a", IntConstant(1, true), true, Int),
                                    false,
                                    Unit),
                                MethodReturnUnit(),
                            ],
                            locals: [Local("a", Int)])
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
                        Method(
                            "_Main",
                            [
                                VariableDeclaration("a", IntConstant(0, true), false),
                                SwitchInt(
                                    CastBoolToInt(BoolConstant(true, true), true),
                                    new(){
                                        {
                                            0,
                                            Block(
                                                [LocalValueAssignment("a", IntConstant(2, true), true, Int)],
                                                Unit,
                                                true)},
                                    },
                                    Block([LocalValueAssignment("a", IntConstant(1, true), true, Int)], Unit, true),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int)])
                    ])
            },
            {
                "assign if else to variable",
                """
                var mut a = 1;
                var b = if (true) { a = 2; } else { a = 3; };
                """,
                LoweredProgram(
                    methods: [
                        Method(
                            "_Main",
                            [
                                VariableDeclaration(
                                    "a", IntConstant(1, true), false),
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
                                                            IntConstant(3, true),
                                                            true,
                                                            Int)
                                                    ],
                                                    Unit,
                                                    true)
                                            }
                                        },
                                        Block(
                                            [LocalValueAssignment("a", IntConstant(2, true), true, Int)],
                                            Unit,
                                            true),
                                        true,
                                        Unit),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", Int),
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
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a", IntConstant(0, true), false),
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
                                                Block([LocalValueAssignment("a", IntConstant(2, true), true, Int)], Unit, true),
                                                false,
                                                Unit)
                                        }
                                    },
                                    Block([LocalValueAssignment("a", IntConstant(1, true), true, Int)], Unit, true),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int)])
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
                        Method("_Main",
                            [
                                VariableDeclaration("a", IntConstant(0, true), false),
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
                                                        Block([LocalValueAssignment("a", IntConstant(3, true), true, Int)], Unit, true)
                                                    }
                                                },
                                                Block([LocalValueAssignment("a", IntConstant(2, true), true, Int)], Unit, true),
                                                false,
                                                Unit)
                                        }
                                    },
                                    Block([LocalValueAssignment("a", IntConstant(1, true), true, Int)], Unit, true),
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", Int)])
                    ])
            },
        };
    }
}
