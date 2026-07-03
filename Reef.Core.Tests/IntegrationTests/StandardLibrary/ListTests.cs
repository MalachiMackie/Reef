using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests.StandardLibrary;

public class ListTests : IntegrationTestBase
{
    [Fact]
    public async Task Create()
    {
        await SetupTest(
            """
            var a = list::<u64>::create();
            print_u64(a.length());
            print_u64(a.capacity());
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("04", result.StandardOutput);
    }

    [Fact]
    public async Task Add()
    {
        await SetupTest(
            """
            var mut a = list::create();
            a.add(4);

            print_u64(a.length());
            print_u64(a.capacity());
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("14", result.StandardOutput);
    }

    [Fact]
    public async Task Get_Found()
    {
        await SetupTest(
            """
            var mut a = list::create();
            a.add(7);

            var b = a.get(0);
            match (b)
            {
                option::Some(var val) => print_u64(val),
                option::None => print_string("None"),
            }
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("7", result.StandardOutput);
    }

    [Fact]
    public async Task Get_NonEmpty_NotFound()
    {
        await SetupTest(
            """
            var mut a = list::create();
            a.add(7);

            print_string(match (a.get(1)) {
                option::None => "pass",
                option::Some => "fail",
            });
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", result.StandardOutput);
    }

    [Fact]
    public async Task Get_Empty_NotFound()
    {
        await SetupTest(
            """
            var mut a = list::<u64>::create();

            print_string(match (a.get(0)) {
                option::None => "pass",
                option::Some => "fail",
            });
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", result.StandardOutput);
    }

    [Fact]
    public async Task AddPastCapacity()
    {
        await SetupTest(
            """
            var mut a = list::create();
            a.add(8);
            a.add(9);
            a.add(10);
            a.add(11);
            a.add(12);

            print_string("length: ");
            print_u64(a.length());
            print_string(", capacity: ");
            print_u64(a.capacity());
            print_string(". ");
            for (var i = 0; i < a.length(); i++)
            {
                print_u64(option::unwrap(a.get(i)));
                print_string(", ");
            }
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("length: 5, capacity: 8. 8, 9, 10, 11, 12, ", result.StandardOutput);
    }

    [Fact]
    public async Task AddToCapacity()
    {
        await SetupTest(
            """
            var mut a = list::create();
            a.add(8);
            a.add(9);
            a.add(10);
            a.add(11);

            print_string("length: ");
            print_u64(a.length());
            print_string(", capacity: ");
            print_u64(a.capacity());
            print_string(". ");
            for (var i = 0; i < a.length(); i++)
            {
                print_u64(option::unwrap(a.get(i)));
                print_string(", ");
            }
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("length: 4, capacity: 4. 8, 9, 10, 11, ", result.StandardOutput);
    }
}
