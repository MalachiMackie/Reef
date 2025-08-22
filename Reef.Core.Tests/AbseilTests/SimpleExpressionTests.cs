namespace Reef.Core.Tests.AbseilTests;

using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

public class SimpleExpressionTests : TestBase
{

    [Theory]
    [MemberData(nameof(TestCases))]
    public void SimpleExpressionAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "variable declaration",
                "var a = \"\";",
                LoweredProgram(methods: [
                    GlobalMethod("_Main",
                        [
                            VariableDeclaration(
                                "a",
                                StringConstant("", true),
                                valueUseful: false),
                            MethodReturnUnit()
                        ],
                        locals: [
                            Local("a", StringType)
                        ])
                ])
            },
            {
                "local assignment",
                "var a;a = 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                VariableDeclaration("a", false),
                                LocalValueAssignment("a", IntConstant(2, true), false, Int),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", Int)
                            ])
                    ])
            },
            {
                "int plus",
                "1 + 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntPlus(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int minus",
                "1 - 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntMinus(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int multiply",
                "1 * 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntMultiply(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int divide",
                "1 / 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntDivide(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int equals",
                "1 == 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntEquals(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int greater than",
                "1 > 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntGreaterThan(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int less than",
                "1 < 2;",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                IntLessThan(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "bool or",
                "true || true",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                BoolOr(BoolConstant(true, true), BoolConstant(true, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "bool and",
                "true && true",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                BoolAnd(BoolConstant(true, true), BoolConstant(true, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "bool not",
                "!true",
                LoweredProgram(
                    methods: [
                        GlobalMethod("_Main",
                            [
                                BoolNot(BoolConstant(true, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
        };
    }
}
