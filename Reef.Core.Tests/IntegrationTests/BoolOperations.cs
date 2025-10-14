using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

namespace Reef.Core.Tests.IntegrationTests;

public class BoolOperations : IntegrationTestBase
{
    [Fact]
    public async Task TrueConstant()
    {
        await SetupTest(
            """
            var a = true;
            if (a) {
                printf("true");
            }
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("true");
    }

    [Fact]
    public async Task FalseConstant()
    {
        await SetupTest(
            """
            var a = false;
            if (a) {
                printf("false");
            }
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("");
    }

    [Fact]
    public async Task BoolNot()
    {
        await SetupTest(
            """
            var a = true;
            if (a) {
                printf("true. ");
            }
            if (!a) {
                printf("!true. ");
            }
            if (!!a) {
                printf("!!true. ");
            }
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("true. !!true. ");
    }
}
