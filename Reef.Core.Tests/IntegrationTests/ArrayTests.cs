namespace Reef.Core.Tests.IntegrationTests;

public class ArrayTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateEmptyArray()
    {
        await SetupTest(
            """
            var a: [string; 0] = [];
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("");
    }

    [Fact]
    public async Task CreateArrayWithItem()
    {
        await SetupTest(
            """
            var a: [string; 2] = ["hi", "bye"];
            print_string(a[0]);
            print_string(a[1]);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }

    [Fact]
    public async Task FillArray()
    {
        await SetupTest(
            """
            var a = [6; 3];
            print_i32(a[0]);
            print_i32(a[1]);
            print_i32(a[2]);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("012");
    }

    [Fact]
    public async Task UnboxedArray()
    {
        await SetupTest("""
                        var a = [unboxed; 1, 5];
                        print_i32(a[0]);
                        print_i32(a[1]);
                        """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("15");
    }

    [Fact]
    public async Task BoxedTypeInsideBoxedArray()
    {
        await SetupTest(
            """
            class MyClass{pub field MyString: string, }
            var a = [new MyClass{MyString = "hi"}, new MyClass{MyString = "bye"}];
            print_string(a[0].MyString);
            print_string(a[1].MyString);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }

    [Fact]
    public async Task WriteIntoArray()
    {
        await SetupTest(
            """
            var mut a = ["hi"];
            print_string(a[0]);
            a[0] = "bye";
            print_string(a[0]);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }
}