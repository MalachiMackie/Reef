using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
