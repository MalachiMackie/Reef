namespace Reef.Core.Tests.IntegrationTests;

public class StringTests : IntegrationTestBase
{
    [Fact]
    public async Task PrintStringConstant()
    {
        await SetupTest("""print_string("Hello World!")""");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Hello World!");
    }

    [Fact]
    public async Task PrintMultipleStringConstants()
    {
        await SetupTest("""print_string("Hello ");print_string("World!!")""");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Hello World!!");
    }

    [Fact]
    public async Task NewLineStringConstants()
    {
        await SetupTest("""print_string("hello\nworld!")""");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"hello{Environment.NewLine}world!");
    }

    [Fact]
    public async Task PrintEmptyString()
    {
        await SetupTest("print_string(\"hi\");print_string(\"\");print_string(\"bye\")");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }

    [Fact]
    public async Task PrintStringConsistingOnlyOfEscapedCharacter()
    {
        await SetupTest("print_string(\"\\n\")");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(Environment.NewLine);
    }

    [Fact]
    public async Task PrintStringStartingWithEscapedCharacter()
    {
        await SetupTest("print_string(\"\\nsomething\")");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"{Environment.NewLine}something");
    }

    [Fact]
    public async Task PrintStringEndingWithEscapedCharacter()
    {
        await SetupTest("print_string(\"something\\n\")");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"something{Environment.NewLine}");
    }

    [Fact]
    public async Task MultipleEscapedCharacters()
    {
        await SetupTest("print_string(\"foo\\nbar\\nbaz\")");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"foo{Environment.NewLine}bar{Environment.NewLine}baz");
    }

    [Fact]
    public async Task ConsecutiveEscapedCharacters()
    {
        await SetupTest("print_string(\"foo\\n\\nbar\")");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"foo{Environment.NewLine}{Environment.NewLine}bar");
    }
}
