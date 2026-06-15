using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class TupleTests : IntegrationTestBase
{
    [Fact]
    public async Task GetTupleElements()
    {
        await SetupTest(
            """
            var a = (1, "hi");
            print_u64(a.Item0);
            print_string(", ");
            print_string(a.Item1);
            """
        );
        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"1, hi");
    }
}
