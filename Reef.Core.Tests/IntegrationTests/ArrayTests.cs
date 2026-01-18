namespace Reef.Core.Tests.IntegrationTests;

public class ArrayTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateEmptyArray()
    {
        await SetupTest(
            """
            var a: [string; 0] = [];
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("");
    }

    [Fact]
    public async Task CreateArrayWithItem()
    {
        await SetupTest(
            """
            var a: [string; 2] = ["hi", "bye"];
            print_string(a[0]);
            print_string(a[1]);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }

    [Fact]
    public async Task WriteIntoArray()
    {
        await SetupTest(
            """
            var mut a = ["hi"];
            print_string(a[0]);
            a[0] = "bye";
            print_string(a[0]);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }
}