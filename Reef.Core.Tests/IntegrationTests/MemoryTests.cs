namespace Reef.Core.Tests.IntegrationTests;

public class MemoryTests : IntegrationTestBase
{
    [Fact]
    [TestMe]
    public async Task MemoryTest()
    {
        await SetupTest(
            """
            class MyClass{pub field A: u64, pub field B: u64, pub field C: u64, pub field D: u64, pub field E: u64, pub field F: u64, pub field G: u64}
            fn create_my_class(num: u64): mut MyClass {
                return new MyClass{A = num, B = num + 1, C = num + 2, D = num + 3, E = num + 4, F = num + 5, G = num + 6};
            }

            var mut latest = create_my_class(0);
            latest = create_my_class(1);

            :::Reef:::Core:::Diagnostics:::trigger_gc();

            var memoryUsed = :::Reef:::Core:::Diagnostics:::get_memory_usage_bytes();
            print_u64(memoryUsed);
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("56");
    }
}
