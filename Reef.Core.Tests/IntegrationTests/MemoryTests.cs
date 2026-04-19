using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class MemoryTests : IntegrationTestBase
{
    [Fact]
    public async Task MemoryTest()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::{get_memory_usage_bytes, trigger_gc};

            class MyClass{pub field A: u64, pub field B: u64, pub field C: u64, pub field D: u64, pub field E: u64, pub field F: u64, pub field G: u64}
            fn create_my_class(num: u64): mut MyClass {
                return new MyClass{A = num, B = num + 1, C = num + 2, D = num + 3, E = num + 4, F = num + 5, G = num + 6};
            }

            fn print_memory_used() {
                var memoryUsed = get_memory_usage_bytes();
                print_string("MemoryUsed: ");
                print_u64(memoryUsed);
                print_string("\n");
            }

            var mut latest = create_my_class(0);
            print_memory_used();
            latest = create_my_class(1);
            print_memory_used();

            var mut i = 0;
            while (i < 100000)
            {
                latest = create_my_class(i);
                trigger_gc();
                i = i + 1;
            }


            trigger_gc();
            print_memory_used();
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            MemoryUsed: 64
            MemoryUsed: 128
            MemoryUsed: 64

            """);
    }

    [Fact]
    public async Task PrintStackTrace_MainOnly()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::print_stack_trace;

            print_stack_trace();
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            main:::_Main
            """
        );
    }

    [Fact]
    public async Task PrintStackTrace()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::print_stack_trace;

            fn SomeFn()
            {
                RecursiveFnEntry();
            }

            fn RecursiveFnEntry()
            {
                RecursiveFn(3);
            }

            fn RecursiveFn(level: u8)
            {
                if (level == 0)
                {
                    print_stack_trace();
                    return;
                }
                RecursiveFn(level - 1);
            }

            SomeFn();
            """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            main:::RecursiveFn
            main:::RecursiveFn
            main:::RecursiveFn
            main:::RecursiveFnEntry
            main:::SomeFn
            main:::_Main
            """
        );
    }

    [Fact(Skip = "Only for testing")]
    // [Fact, TestMe]
    public async Task PrintTypes()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            class MyClass{pub field A: string}
            union MyUnion{A, B}

            var a = new MyClass{A = ""};
            var b = MyUnion::B;
            var c = [unboxed; ""];

            print_all_types();
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact(Skip = "Only for testing")]
    // [Fact, TestMe]
    public async Task PrintMethods()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            pub fn Something()
            {}

            pub fn Something1(val: string)
            {
                var a = 2;
            }

            pub fn Something2(val: i32, val2: string)
            {}

            pub fn Something3<T>(val: T)
            {}

            Something();
            Something1("hi");
            Something2(1, "bye");
            Something3(1);
            Something3("bye");
            print_all_methods();
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public async Task BoxedUnion()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;
            class MyClass {pub field value: u64}
            union MyUnion {
                A(MyClass),
                B,
            }

            var mut a = MyUnion::A(new MyClass{value = 2});
            a = MyUnion::B;

            trigger_gc();

            var memoryUsed = get_memory_usage_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("24");
    }

    [Fact]
    public async Task CircularBoxedValue()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;
            union Option<T> {
                None,
                Some(T)
            }

            class MyClass {
                pub mut field value: unboxed Option<MyClass>,

                pub static fn create(): mut MyClass {
                    var mut a = new MyClass{value = unboxed Option::None};
                    var mut b = new MyClass{value = unboxed Option::Some(a)};
                    a.value = unboxed Option::Some(b);
                    return a;
                }
            }

            var mut value = MyClass::create();
            value = MyClass::create();

            trigger_gc();

            var memoryUsed = get_memory_usage_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("32");
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
            var memoryUsed = get_memory_usage_bytes();
            print_u64(memoryUsed);
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("32");
    }

    [Fact]
    public async Task BoxedValueStoredInUnboxedObject()
    {
        await SetupTest(
            """
            use :::Reef:::Core:::Diagnostics:::*;

            class SubClass{pub field B: u64}
            class MyClass{pub field A: SubClass}

            var a = new unboxed MyClass{A = new SubClass{B = 6}};

            trigger_gc();
            var memoryUsed = get_memory_usage_bytes();
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
            var memoryUsed = get_memory_usage_bytes();
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
            var memoryUsed = get_memory_usage_bytes();
            print_u64(memoryUsed);
            """
        );

        // ObjectHeader: 4 bytes
        // Padding: 4 bytes
        // ArrayLength: 8 bytes
        // MyClassPointer: 8 bytes
        //
        // ObjectHeader: 8 bytes
        // MyClassObject: 8 bytes

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("40");
    }
}
