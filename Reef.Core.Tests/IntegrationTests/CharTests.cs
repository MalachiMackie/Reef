using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class CharTests : IntegrationTestBase
{
    [Theory]
    [InlineData("a", "a", "a")]
    [InlineData("A", "A", "A")]
    [InlineData("z", "z", "z")]
    [InlineData("semicolon", ";", ";")]
    [InlineData("space", " ", " ")]
    [InlineData("new line", "\\n", "\n")]
    [InlineData("single single quote", "'", "'")]
    [InlineData("unicode character", "は", "は")]
    public async Task PrintCharConstant(string name, string constant, string expectedOutput)
    {
        await SetupTest($"print_char('{constant}');", name);

        var result = await Run(name);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(expectedOutput.Replace("\n", Environment.NewLine));
    }

    [Theory]
    [InlineData("aa", "a", "a", "true")]
    [InlineData("ab", "a", "b", "false")]
    [InlineData("semicolon", ";", ";", "true")]
    [InlineData("semicolon==b", ";", "b", "false")]
    [InlineData("new line", "\\n", "\\n", "true")]
    [InlineData("new line==b", "\\n", "b", "false")]
    [InlineData("unicode character", "は", "は", "true")]
    [InlineData("unicode character", "は", "に", "false")]
    public async Task CharEquals(string name, string left, string right, string expectedOutput)
    {
        await SetupTest(
            $$"""
            var result = if ('{{left}}' == '{{right}}') "true" else "false";
            print_string(result);
            """, name);

        var result = await Run(name);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(expectedOutput);
    }
}
