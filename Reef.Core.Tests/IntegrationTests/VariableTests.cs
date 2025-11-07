using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Reef.Core.Tests.IntegrationTests;

public class VariableTests : IntegrationTestBase
{
    [Fact]
    public async Task PrintStringVariable()
    {
        await SetupTest("var a = \"My Test\";printf(a)");
        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("My Test");
    }

    [Fact]
    public async Task PrintMultipleVariableStrings()
    {
        await SetupTest(
            """
            var a = "Hello";
            var b = "World";

            printf(a);
            printf(" ");
            printf(b);
            printf("!");
            """);

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Hello World!");
    }

    [Fact]
    public async Task VariableReassignment()
    {
        await SetupTest(
            """
            var mut a = "hello";
            a = "world";

            printf(a);
            """);

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("world");
    }
}
