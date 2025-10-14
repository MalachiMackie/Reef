namespace Reef.Core.Tests.AbseilTests;

using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.LoweredProgramHelpers;

public class SimpleExpressionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{

    [Theory]
    [MemberData(nameof(TestCases))]
    public void SimpleExpressionAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

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
                    Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
                            [
                                IntDivide(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int not equals",
                "1 != 2;",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                IntNotEquals(IntConstant(1, true), IntConstant(2, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "int equals",
                "1 == 2;",
                LoweredProgram(
                    methods: [
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
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
                        Method("_Main",
                            [
                                BoolNot(BoolConstant(true, true), false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "empty block",
                "{}",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                Block([], Unit, false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "block with one expression",
                "{true;}",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                Block([BoolConstant(true, false)], Unit, false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "block with multiple expressions",
                "{true; 1;}",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                Block([
                                    BoolConstant(true, false),
                                    IntConstant(1, false),
                                ], Unit, false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "local access",
                "var a = 1; var b = a;",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                VariableDeclaration("a", IntConstant(1, true), valueUseful: false),
                                VariableDeclaration("b", LocalAccess("a", true, Int), valueUseful: false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", Int),
                                Local("b", Int),
                            ])
                    ])
            },
            {
                "method call",
                "fn MyFn(){} MyFn();",
                LoweredProgram(
                    methods: [
                        Method("MyFn", [MethodReturnUnit()]),
                        Method("_Main", [
                            MethodCall(FunctionReference("MyFn"), [], false, Unit),
                            MethodReturnUnit()
                        ])
                    ])
            },
            {
                "generic method call",
                """
                fn MyFn<T>(){}
                MyFn::<string>();
                MyFn::<int>();
                """,
                LoweredProgram(
                    methods: [
                        Method("MyFn", [MethodReturnUnit()], typeParameters: ["T"]),
                        Method("_Main",
                            [
                                MethodCall(
                                    FunctionReference("MyFn", [StringType]),
                                    [],
                                    false,
                                    Unit),
                                MethodCall(
                                    FunctionReference("MyFn", [Int]),
                                    [],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "function parameter access",
                "fn MyFn(a: string): string { return a; }",
                LoweredProgram(
                    methods: [
                        Method("MyFn",
                            [
                                MethodReturn(
                                    LoadArgument(0, true, StringType))
                            ],
                            parameters: [StringType],
                            returnType: StringType)
                    ])
            },
            {
                "single element tuple",
                "(1)",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                IntConstant(1, false),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "two element tuple",
                "(1, \"\")",
                LoweredProgram(
                    methods: [
                        Method("_Main",
                            [
                                CreateObject(
                                    ConcreteTypeReference("Tuple`2", [Int, StringType]),
                                    "_classVariant",
                                    false,
                                    new()
                                    {
                                        {"Item0", IntConstant(1, true)},
                                        {"Item1", StringConstant("", true)},
                                    }),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "local function in block",
                """
                {
                    fn SomeFn(){}
                }
                """,
                LoweredProgram(
                    methods: [
                        Method("_Main__SomeFn",
                            [MethodReturnUnit()]),
                        Method("_Main",
                            [
                                Block([], Unit, false),
                                MethodReturnUnit()
                            ])
                    ])
            },
        };
    }
}
