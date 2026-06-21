using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class ControlFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task BasicIf_0()
    {
        await SetupTest("""
            var a = 1;
            var b = a < 2;

            if (b) {
                print_string("Less than 2. ");
            }

            if (a > 2) {
                print_string("Greater than 2. ");
            }
            else {
                print_string("Less than or equal to 2. ");
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("Less than 2. Less than or equal to 2. ", output.StandardOutput);
    }

    [Fact]
    public async Task BasicIf_1()
    {
        await SetupTest("""
            var a = 2;
            var b = a < 2;

            if (b) {
                print_string("Less than 2. ");
            }

            if (a > 2) {
                print_string("Greater than 2. ");
            }
            else {
                print_string("Less than or equal to 2. ");
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("Less than or equal to 2. ", output.StandardOutput);
    }

    [Fact]
    public async Task BasicIf_2()
    {
        await SetupTest("""
            var a = 3;
            var b = a < 2;

            if (b) {
                print_string("Less than 2. ");
            }

            if (a > 2) {
                print_string("Greater than 2. ");
            }
            else {
                print_string("Less than or equal to 2. ");
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("Greater than 2. ", output.StandardOutput);
    }

    [Fact]
    public async Task WhileLoop()
    {
        await SetupTest(
            """
            var mut a = 10;
            while (a > 0) {
                print_string("hi. ");
                a = a - 1;
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("hi. hi. hi. hi. hi. hi. hi. hi. hi. hi. ", output.StandardOutput);
    }

    [Fact]
    public async Task WhileLoopWithGrab()
    {
        await SetupTest(
            """
            var b = 0;
            var a = while (b < 5) {
                b = b + 1;
                print_u32(b);
                grab b;
            };

            print_u32(a + 1);
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("123456", result.StandardOutput);
    }

    [Fact]
    public async Task ForLoop()
    {
        await SetupTest(
            """
            for (var i = 0; i < 10; i = i + 1)
            {
                print_u32(i);
            }
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("0123456789", result.StandardOutput);
    }

    [Fact]
    public async Task ForLoopWithBreak()
    {
        await SetupTest(
            """
            var i = 0;
            for (;;)
            {
                if (i > 4)
                {
                    break;
                }

                print_u32(i);

                i = i + 1;
            }
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("01234", result.StandardOutput);
    }

    [Fact]
    public async Task ForLoopWithContinue()
    {
        await SetupTest(
            """
            for (var i = 0; i < 10; i = i + 1)
            {
                if (i < 5)
                {
                    continue;
                }
                print_u32(i);
            }
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("56789", result.StandardOutput);
    }

    [Fact]
    public async Task ForLoopWithGrab()
    {
        await SetupTest(
            """
            var b = for (var i = 0; i < 10; i = i + 1)
            {
                grab i;
            };
            print_u32(b);
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("10", result.StandardOutput);
    }

    [Fact]
    public async Task WhileWithBreakAndContinue()
    {
        await SetupTest(
            """
            var mut a = 0;
            while (true) {
                a = a + 1;
                if (a == 1) {
                    continue;
                }

                print_string("hi. ");

                if (a == 5) {
                    break;
                }
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("hi. hi. hi. hi. ", output.StandardOutput);
    }

    [Theory]
    [InlineData("A", "1")]
    [InlineData("B", "2")]
    public async Task MatchOnUnion(string variant, string expectedNumber)
    {
        await SetupTest(
            $$"""
            union MyUnion {A, B}
            var a = MyUnion::{{variant}};
            var b = match(a) {
                MyUnion::A => 1,
                MyUnion::B => 2,
            };
            print_u8(b);
            """,
            variant
        );

        var output = await Run(variant);

        Assert.Equal(0, output.ExitCode);
        Assert.Equal(expectedNumber, output.StandardOutput);
    }
}
