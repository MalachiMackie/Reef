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
            fn SomeFn(a: int, b: string, c: bool) {
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
}
