using FluentAssertions.Execution;
using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class ExceptionTests : IntegrationTestBase
{
    [Fact, TestMe]
    public async Task CreateEmptyArray()
    {
        await SetupTest(
            """
            panic();
            """);


        var result = await Run();

        using var _ = new AssertionScope();

        result.ExitCode.Should().Be(1);
        result.StandardOutput.Should().Be("Unhandled panic!");
    }
}
