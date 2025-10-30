using FluentAssertions;

namespace Reef.Core.Tests.IntegrationTests;

public class FunctionTests : IntegrationTestBase
{
    [Fact]
    public async Task CallFunctionWithNoArgumentsOrReturnType()
    {
        await SetupTest("""
            fn SomeFn() {
                printf("SomeFn");
            }
            printf("Start. ");
            SomeFn();
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Start. SomeFn");
    }

    [Fact]
    public async Task CallFunctionWithArguments()
    {
        await SetupTest("""
            fn SomeFn(a: i64, b: string, c: bool) {
                if (a == 0) {
                    printf("a == 0. ");
                }

                printf(b);

                if (c) {
                    printf("c is true. ");
                }
            }

            var d = 1;
            var e = "Good Bye";
            var f = false;

            SomeFn(0, "Hello World! ", true);
            SomeFn(d, e, f);
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 0. Hello World! c is true. Good Bye");
    }

    [Fact]
    public async Task ReturnValueFromFunction()
    {
        await SetupTest("""
            fn SomeFn(): string {
                return "Hello World";
            }

            printf(SomeFn());
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("Hello World");
    }

    [Fact]
    public async Task ReturnValueFromMultipleFunction()
    {
        await SetupTest("""
            fn SomeFn(): string {
                return "Hello World. ";
            }
            fn SomeFn2(): string {
                return "Good Bye";
            }
            fn SomeFn3(a: string, b: string) {
                printf(a);
                printf(b);
            }

            SomeFn3(SomeFn(), SomeFn2());
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("Hello World. Good Bye");
    }

    [Fact]
    public async Task CallFunctionWithMoreThan4Arguments()
    {
        await SetupTest(
            """
            fn SomeFn(a: string, b: string, c: string, d: string, e: string, f: string, g: string, h: string) {
                printf(a);
                printf(b);
                printf(c);
                printf(d);
                printf(e);
                printf(f);
                printf(g);
                printf(h);
            }
            
            SomeFn("a", "b", "c", "d", "e", "f", "g", "h");
            """
        );

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("abcdefgh", result.StandardOutput);
    }
}
