namespace Reef.Core.Tests.IntegrationTests.Examples;

public class FibonacciTest : IntegrationTestBase
{
    [Fact]
    public async Task Fibonacci()
    {
        await SetupTest(
            """
            var mut a = 0;
            var mut b = 1;
            
            print_i32(a);
            print_string(", ");
            print_i32(b);
            
            while (b < 1000) { 
                print_string(", ");
                
                var c = b;
                b = a + c;
                a = c;
                print_i32(b);
            }
            """);

        var output = await Run();
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597");
    }
}