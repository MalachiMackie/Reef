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

    [Fact]
    public async Task BoolAnd()
    {
        await SetupTest(
            """
            fn getTrue(): bool {
                printf("getTrue(). ");
                return true;
            }
            fn getFalse(): bool {
                printf("getFalse(). ");
                return false;
            }
            
            if (getTrue() && getTrue()) {
                printf("true && true. ");
            }
            if (getFalse() && getTrue()) {
                printf("false && true. ");
            }
            if (getTrue() && getFalse()) {
                printf("true && false. ");
            }
            if (getFalse() && getFalse()) {
                printf("false && false. ");
            }
            """);

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("getTrue(). getTrue(). true && true. getFalse(). getTrue(). getFalse(). getFalse(). ", result.StandardOutput);
    }

    [Fact]
    public async Task BoolOr()
    {
        await SetupTest(
            """
            fn getTrue(): bool {
                printf("getTrue(). ");
                return true;
            }
            fn getFalse(): bool {
                printf("getFalse(). ");
                return false;
            }

            if (getTrue() || getTrue()) {
                printf("true || true. ");
            }
            if (getFalse() || getTrue()) {
                printf("false || true. ");
            }
            if (getTrue() || getFalse()) {
                printf("true || false. ");
            }
            if (getFalse() || getFalse()) {
                printf("false || false");
            }
            """);

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("getTrue(). true || true. getFalse(). getTrue(). false || true. getTrue(). true || false. getFalse(). getFalse(). ", result.StandardOutput);
    }
}
