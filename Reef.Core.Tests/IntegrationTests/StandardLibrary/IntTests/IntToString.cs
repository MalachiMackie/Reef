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
    public async Task U8ToString(byte value)
    {
        await SetupTest($"""
            var x: u8 = {value};
            print_string(x.to_string());
            """, value.ToString());

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
    public async Task I8ToString(sbyte value)
    {
        await SetupTest($"""
            var x: i8 = {value};
            print_string(x.to_string());
            """, value.ToString());

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
    [InlineData(30)]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(12412)]
    [InlineData(ushort.MaxValue)]
    public async Task U16ToString(ushort value)
    {
        await SetupTest($"""
        var x: u16 = {value};
        print_string(x.to_string());
        """, value.ToString());

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
    [InlineData(100)]
    [InlineData(12414)]
    [InlineData(short.MaxValue)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-10)]
    [InlineData(-100)]
    [InlineData(-15212)]
    [InlineData(short.MinValue)]
    public async Task I16ToString(short value)
    {
        await SetupTest($"""
        var x: i16 = {value};
        print_string(x.to_string());
        """, value.ToString());

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
    [InlineData(30)]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(12416)]
    [InlineData(1251231)]
    [InlineData(uint.MaxValue)]
    public async Task U32ToString(uint value)
    {
        await SetupTest($"""
        var x: u32 = {value};
        print_string(x.to_string());
        """, value.ToString());

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
    [InlineData(100)]
    [InlineData(2250121)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-10)]
    [InlineData(-22)]
    [InlineData(-100)]
    [InlineData(-2250121)]
    [InlineData(int.MinValue)]
    public async Task I32ToString(int value)
    {
        await SetupTest($"""
        var x: i32 = {value};
        print_string(x.to_string());
        """, value.ToString());

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
    [InlineData(30)]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(12412)]
    [InlineData(124126126421242)]
    [InlineData(ulong.MaxValue)]
    public async Task U64ToString(ulong value)
    {
        await SetupTest($"""
        var x: u64 = {value};
        print_string(x.to_string());
        """, value.ToString());

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
    [InlineData(1241)]
    [InlineData(20000)]
    [InlineData(124124612)]
    [InlineData(long.MaxValue)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-10)]
    [InlineData(-22)]
    [InlineData(-1241)]
    [InlineData(-20000)]
    [InlineData(-124124612)]
    [InlineData(long.MinValue)]
    public async Task I64ToString(long value)
    {
        await SetupTest($"""
            var x: i64 = {value};
            print_string(x.to_string());
            """, value.ToString());

        var result = await Run(value.ToString());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(value.ToString(), result.StandardOutput);
    }
}
