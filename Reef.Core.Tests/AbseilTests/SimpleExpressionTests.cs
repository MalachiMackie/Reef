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
        };
    }
}
