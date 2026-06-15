using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class TupleTests : IntegrationTestBase
{
    [Fact]
    public async Task GetTupleElements()
    {
        await SetupTest(
            """
            var a = (1, "hi");
            print_u64(a.Item0);
            print_string(", ");
            print_string(a.Item1);
            """
        );
        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"1, hi");
    }

    [Fact]
    public async Task GetTupleElementsWithReferenceObject()
    {
        await SetupTest(
            """
            union MyUnion{
                A,
                B,

                pub fn to_string(): string {
                    return match (this)
                    {
                        Self::A => "A",
                        Self::B => "B",
                    };
                }
            }

            var a = (MyUnion::A, MyUnion::B);
            var b = a.Item0;
            var c = a.Item1;
            print_string(b.to_string());
            print_string(c.to_string());
            """
        );
        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"AB");
    }
}
