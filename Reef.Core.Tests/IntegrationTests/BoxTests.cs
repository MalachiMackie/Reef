namespace Reef.Core.Tests.IntegrationTests;

public class BoxTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateUnboxedClass()
    {
        await SetupTest(
            """
            class MyClass {pub field MyField: string}
            
            var a = new unboxed MyClass{MyField = "hi"};
            
            printf(a.MyField);
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("hi");
    }

    [Fact]
    public async Task ModifyUnboxedClassInFunction()
    {
        await SetupTest(
            """
            
            fn SomeFn(mut param: unboxed MyClass) {
                param.MyField = "bye";
                printf("param.MyField == ");
                printf(param.MyField);
                printf(". ");
            }
            
            class MyClass {pub field MyField: string}
            
            var mut a = new unboxed MyClass{MyField = "hi"};
            SomeFn(a);
            
            printf("a.MyField == ");
            printf(a.MyField);
            printf(". ");
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

            fn SomeFn(mut param: unboxed MyClass) {
                param.MyField = "bye";
                printf("param.MyField == ");
                printf(param.MyField);
                printf(". ");
            }

            class MyClass {pub field MyField: string}

            var mut a = new MyClass{MyField = "hi"};
            SomeFn(a);

            printf("a.MyField == ");
            printf(a.MyField);
            printf(". ");
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
                printf("a == 1. ");
            }
            if (unbox(a) == 2) {
                printf("a == 2. ");
            }
            if (a == box(1)) {
                printf("a == box(1). ");
            }
            if (a == box(2)) {
                printf("a == box(2). ");
            }
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 1. a == box(1). ");
    }

    [Fact]
    public async Task ModifyBoxedPrimitiveInMethod()
    {
        await SetupTest(
            """
            fn SomeFn(mut param: boxed i32) {
                param = box(2);
                if (param == box(1)) {
                    printf("param == box(1). ");
                }
                else if (param == box(2)) {
                    printf("param == box(2). ");
                }
            }
            
            var mut a = box(1);
            SomeFn(a);
            
            if (a == box(1)) {
                printf("a == box(1)");
            }
            else if (a == box(2)) {
                printf("a == box(2)");
            }
            """
        );

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("param == box(2). a == box(2)");
    }
    
    [Fact]
    public async Task ModifyUnboxedPrimitiveInMethod()
    {
        await SetupTest(
            """
            fn SomeFn(mut param: i32) {
                param = 2;
                if (param == 1) {
                    printf("param == 1. ");
                }
                else if (param == 2) {
                    printf("param == 2. ");
                }
            }

            var mut a = 1;
            SomeFn(a);

            if (a == 1) {
                printf("a == 1");
            }
            else if (a == 2) {
                printf("a == 2");
            }
            """
        );

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("param == 2. a == 1");
    }
}