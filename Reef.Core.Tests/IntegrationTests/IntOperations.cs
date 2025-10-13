using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

namespace Reef.Core.Tests.IntegrationTests;

public class IntOperations : IntegrationTestBase
{
    [Fact]
    public async Task IntEquals()
    {
        await SetupTest("""
            if (1 == 1) {
                printf("1 == 1");
            }
            if (1 == 2) {
                printf("1 == 2");
            }
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("1 == 1");
    }

    [Fact]
    public async Task Add()
    {
        await SetupTest(
            """
            var a = 1 + 2;
            if (a == 3) {
                printf("a == 3");
            } else {
                printf("a != 3");
            }
            """);

        var output = await Run();

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 3");
    }
}
