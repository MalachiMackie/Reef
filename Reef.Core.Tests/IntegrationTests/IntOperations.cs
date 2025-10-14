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
    public async Task IntNotEquals()
    {
        await SetupTest("""
            if (1 != 0) {
                printf("1 != 0");
            }
            if (1 != 1) {
                printf("1 != 1");
            }
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("1 != 0");
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

    [Fact]
    public async Task Subtract()
    {
        await SetupTest(
            """
            var a = 4 - 1;
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

    [Fact]
    public async Task Multiply()
    {
        await SetupTest(
            """
            var a = 4 * 2;
            if (a == 8) {
                printf("a == 8");
            } else {
                printf("a != 8");
            }
            """);

        var output = await Run();

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 8");
    }

    [Fact]
    public async Task Divide()
    {
        await SetupTest(
            """
            var a = 4 / 2;
            if (a == 2) {
                printf("a == 2");
            } else {
                printf("a != 2");
            }
            """);

        var output = await Run();

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 2");
    }

    [Fact]
    public async Task DivideNonEvenly()
    {
        await SetupTest(
            """
            var a = 4 / 3;
            if (a == 1) {
                printf("a == 1");
            } else {
                printf("a != 1");
            }
            """);

        var output = await Run();

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 1");
    }
}
