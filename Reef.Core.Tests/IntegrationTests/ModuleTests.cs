namespace Reef.Core.Tests.IntegrationTests;

public class ModuleTests : IntegrationTestBase
{
    [Fact]
    public async Task UseSiblingModule()
    {
        await SetupTest(
            new Dictionary<string, string>
            {
                {
                    "main.rf",
                    """
                    use :::otherModule:::MyClass;

                    var a = new MyClass{MyString = "hi"};
                    print_string(a.MyString);
                    """
                },
                {
                    "otherModule.rf",
                    """
                    class MyClass{pub field MyString: string}
                    """
                }
            }
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hi");
    }

    [Fact]
    public async Task FunctionsInDifferentModulesWithTheSameName()
    {
        await SetupTest(
            new Dictionary<string, string>
            {
                {
                    "main.rf",
                    """
                    fn SomeFn() {
                        print_string("from main.rf! ");
                    }

                    SomeFn();
                    :::otherModule:::SomeFn();
                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub fn SomeFn() {
                        print_string("from otherModule.rf! ");
                    }
                    """
                }
            }
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("from main.rf! from otherModule.rf! ");
    }
}
