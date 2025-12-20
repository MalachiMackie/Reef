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
                printf("Less than 2. ");
            }

            if (a > 2) {
                printf("Greater than 2. ");
            }
            else {
                printf("Less than or equal to 2. ");
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
                printf("Less than 2. ");
            }

            if (a > 2) {
                printf("Greater than 2. ");
            }
            else {
                printf("Less than or equal to 2. ");
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
                printf("Less than 2. ");
            }

            if (a > 2) {
                printf("Greater than 2. ");
            }
            else {
                printf("Less than or equal to 2. ");
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
                printf("hi. ");
                a = a - 1;
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("hi. hi. hi. hi. hi. hi. hi. hi. hi. hi. ", output.StandardOutput);
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
                
                printf("hi. ");
                
                if (a == 5) {
                    break;
                }
            }
            """);

        var output = await Run();

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("hi. hi. hi. hi. ", output.StandardOutput);
    }
}
