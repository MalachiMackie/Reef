using FluentAssertions.Execution;
using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class ExceptionTests : IntegrationTestBase
{
    [Fact]
    public async Task GlobalHandler()
    {
        await SetupTest(
            """
            panic();
            """);


        var result = await Run();

        using var _ = new AssertionScope();

        result.ExitCode.Should().Be(1);
        result.StandardError.Should().Be("");
        result.StandardOutput.Should().Be("Unhandled panic!");
    }

    [Fact]
    public async Task GlobalHandlerInsideMethod()
    {
        await SetupTest(
            """
            fn throws() {
                panic();
            }

            throws();
            """);


        var result = await Run();

        using var _ = new AssertionScope();

        result.ExitCode.Should().Be(1);
        result.StandardError.Should().Be("");
        result.StandardOutput.Should().Be("Unhandled panic!");
    }

    [Fact]
    public async Task CatchFnDoesntThrow()
    {
        await SetupTest(
            """
            fn doesnt_throw(): string {
                return "hi";
            }

            match (catch_unwind(doesnt_throw)) {
                result::Ok(var x) => print_string(x),
                result::Error => print_string("error thrown 1")
            }
            """
        );

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hi");
    }

    [Fact]
    public async Task CatchPanic_UsesStackValues()
    {
        await SetupTest(
            """
            fn throws(): string {
                panic();
                return "hi";
            }

            var a = 2;

            match (catch_unwind(throws)) {
                result::Ok(var x) => print_string(x),
                result::Error => {
                    print_string("error thrown 1\n");
                    print_u32(a);
                }
            }
            """
        );

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            error thrown 1
            2
            """
        );
    }

    [Fact]
    public async Task CatchPanic()
    {
        await SetupTest(
            """
            fn throws(): string {
                panic();
                return "hi";
            }

            match (catch_unwind(throws)) {
                result::Ok(var x) => print_string(x),
                result::Error => print_string("error thrown 1")
            }
            """
        );

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            error thrown 1
            """
        );
    }
}
