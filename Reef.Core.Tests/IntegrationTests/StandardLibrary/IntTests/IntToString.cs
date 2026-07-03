using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests.StandardLibrary.IntTests;

public class IntToString : IntegrationTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(22)]
    [InlineData(30)]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(byte.MaxValue)]
    [TestMe]
    public async Task U8ToString(byte value)
    {
        await SetupTest($"print_string(u8_to_string({value}));", value.ToString());

        var result = await Run(value.ToString());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(value.ToString(), result.StandardOutput);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(22)]
    [InlineData(sbyte.MaxValue)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-10)]
    [InlineData(-22)]
    [InlineData(sbyte.MinValue)]
    [TestMe]
    public async Task I8ToString(sbyte value)
    {
        await SetupTest($"print_string(i8_to_string({value}));", value.ToString());

        var result = await Run(value.ToString());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(value.ToString(), result.StandardOutput);
    }
}
