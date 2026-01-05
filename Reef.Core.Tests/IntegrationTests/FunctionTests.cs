namespace Reef.Core.Tests.IntegrationTests;

public class FunctionTests : IntegrationTestBase
{
    [Fact]
    public async Task CallFunctionWithNoArgumentsOrReturnType()
    {
        await SetupTest("""
            fn SomeFn() {
                print_string("SomeFn");
            }
            print_string("Start. ");
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
                    print_string("a == 0. ");
                }

                print_string(b);

                if (c) {
                    print_string("c is true. ");
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

            print_string(SomeFn());
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
                print_string(a);
                print_string(b);
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
                print_string(a);
                print_string(b);
                print_string(c);
                print_string(d);
                print_string(e);
                print_string(f);
                print_string(g);
                print_string(h);
            }
            
            SomeFn("a", "b", "c", "d", "e", "f", "g", "h");
            """
        );

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("abcdefgh", result.StandardOutput);
    }
    
    [Fact]
    public async Task GenericFunction()
    {
        await SetupTest(
            """
            fn SomeFn<T>(param: T): T {
                return param;
            }
            
            var a = SomeFn(1);
            var b = SomeFn("hi");
            
            if (a == 1) {
                print_string("a == 1. ");
            }
            print_string(b);
            """);

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a == 1. hi", result.StandardOutput);
    }

    [Fact]
    public async Task GenericFunctionCallingAnotherGenericFunction()
    {
        await SetupTest(
            """
            fn Fn1<T>(param: T): T {
                return param;
            }
            
            fn Fn2<T>(param: T): T {
                return Fn1(param);
            }
            
            var a = Fn2(1);
            var b = Fn2("hi");
            
            if (a == 1) {
                print_string("a == 1. ");
            }
            print_string(b);
            """
        );
        
        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a == 1. hi", result.StandardOutput);
    }
}
