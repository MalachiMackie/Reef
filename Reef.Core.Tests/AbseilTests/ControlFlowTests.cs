
using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ControlFlowTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ControlFlowAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    [Fact]
    public void SingleTest()
    {
        var source = """
                fn SomeFn(): result::<int, int>
                {
                    return error(1);
                }

                fn OtherFn(): result::<int, int>
                {
                    var a = SomeFn()?;
                    return ok(a);
                }
                """;
        var expectedProgram = LoweredProgram(
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
                                            IfExpression(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                        "_variantIdentifier",
                                                        "Ok",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                FieldAccess(
                                                    LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                    "Item0",
                                                    "Ok",
                                                    true,
                                                    Int),
                                                valueUseful: true,
                                                resolvedType: Int,
                                                elseBody: MethodReturn(
                                                    LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int]))))
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
                    ]);

        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
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
                                            IfExpression(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                        "_variantIdentifier",
                                                        "Ok",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                FieldAccess(
                                                    LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int])),
                                                    "Item0",
                                                    "Ok",
                                                    true,
                                                    Int),
                                                valueUseful: true,
                                                resolvedType: Int,
                                                elseBody: MethodReturn(
                                                    LocalAccess("Local1", true, ConcreteTypeReference("result", [Int, Int]))))
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
            }
        };
    }
}
