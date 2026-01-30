namespace Reef.Core.Tests.IntegrationTests;

public class ModuleTests : IntegrationTestBase
{
    [Fact]
    public async Task UseSiblingModule()
    {
        await SetupTest(
            """
             class MyClass{pub field MyString: string}
             """,
            fileName: "otherModule.rf");

        await SetupTest(
            """
            use ::otherModule::MyClass;
            
            var a = new MyClass{MyString = "hi"};
            print_string(a.MyString);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hi");
    }
}