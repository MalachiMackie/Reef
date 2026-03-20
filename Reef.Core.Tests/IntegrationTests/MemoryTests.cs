using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class MemoryTests : IntegrationTestBase
{
    [Fact]
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

    [Fact]
    public async Task BoxedValueStoredInBoxedField()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            class SubClass{pub field B: u64}
            class MyClass{pub field A: SubClass}

            var a = new MyClass {A = new SubClass{B = 7}};

            trigger_gc();
            var memoryUsed = get_memory_used_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("24"); // 8 + two 8 byte object headers
    }

    [Fact]
    public async Task BoxedValueStoredInUnboxedObject()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            class SubClass{pub field B: u64}
            class MyClass{pub field A: SubClass}

            var a = new unboxed MyClass{A = new SubClass{B = 6}}

            trigger_gc();
            var memoryUsed = get_memory_used_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("16"); // 8 + one 8 byte object header
    }

    [Fact]
    public async Task BoxedValueStoredInUnboxedArray()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            class MyClass{pub field A: u64}

            var a = [unboxed; new MyClass{A = 3}];

            trigger_gc();
            var memoryUsed = get_memory_used_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("16"); // 8 + one 8 byte object header
    }

    [Fact]
    public async Task BoxedValueStoredInBoxedArray()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            class MyClass{pub field A: u64}

            var a = [new MyClass{A = 3}];

            trigger_gc();
            var memoryUsed = get_memory_used_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("24"); // 8 + class object header + array object header
    }
}
