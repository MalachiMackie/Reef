namespace Reef.Core.Tests.IntegrationTests;

public class BoxTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateUnboxedClass()
    {
        await SetupTest(
            """
            class MyClass {pub field MyField: string, pub field SecondField: string}
            
            var a = new unboxed MyClass{MyField = "hi", SecondField = "bye"};
            
            print_string(a.MyField);
            print_string(a.SecondField);
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("hibye");
    }
    
    [Fact]
    public async Task ModifyUnboxedClassInFunction()
    {
        await SetupTest(
            """
            
            fn SomeFn(mut param: unboxed MyClass) {
                param.MyField = "bye";
                print_string("param.MyField == ");
                print_string(param.MyField);
                print_string(". ");
            }
            
            class MyClass {pub mut field MyField: string}
            
            var mut a = new unboxed MyClass{MyField = "hi"};
            SomeFn(a);
            
            print_string("a.MyField == ");
            print_string(a.MyField);
            print_string(". ");
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("param.MyField == bye. a.MyField == hi. ");
    }
    
    [Fact]
    public async Task ModifyBoxedClassInFunction()
    {
        await SetupTest(
            """

            fn SomeFn(mut param: MyClass) {
                param.MyField = "bye";
                print_string("param.MyField == ");
                print_string(param.MyField);
                print_string(". ");
            }

            class MyClass {pub mut field MyField: string}

            var mut a = new MyClass{MyField = "hi"};
            SomeFn(a);

            print_string("a.MyField == ");
            print_string(a.MyField);
            print_string(". ");
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("param.MyField == bye. a.MyField == bye. ");
    }

    [Fact]
    public async Task CreateBoxedPrimitive()
    {
        await SetupTest(
            """
            var a: boxed i32 = box(1);
            
            if (unbox(a) == 1) {
                print_string("unbox(a) == 1");
            }
            if (unbox(a) == 2) {
                print_string("unbox(a) == 2");
            }
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("unbox(a) == 1");
    }

    [Fact]
    public async Task ModifyBoxedPrimitiveInMethod()
    {
        await SetupTest(
            """
            fn SomeFn(mut param: boxed i32) {
                param = box(2);
                if (unbox(param) == 1) {
                    print_string("unbox(param) == 1. ");
                }
                else if (unbox(param) == 2) {
                    print_string("unbox(param) == 2. ");
                }
            }
            
            var mut a = box(1);
            SomeFn(a);
            
            if (unbox(a) == 1) {
                print_string("unbox(a) == 1");
            }
            else if (unbox(a) == 2) {
                print_string("unbox(a) == 2");
            }
            """
        );

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("unbox(param) == 2. unbox(a) == 1");
    }
    
    [Fact]
    public async Task ModifyUnboxedPrimitiveInMethod()
    {
        await SetupTest(
            """
            fn SomeFn(mut param: i32) {
                param = 2;
                if (param == 1) {
                    print_string("param == 1. ");
                }
                else if (param == 2) {
                    print_string("param == 2. ");
                }
            }

            var mut a = 1;
            SomeFn(a);

            if (a == 1) {
                print_string("a == 1");
            }
            else if (a == 2) {
                print_string("a == 2");
            }
            """
        );

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("param == 2. a == 1");
    }
}