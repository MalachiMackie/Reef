using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class UnionTests : IntegrationTestBase
{
    [Theory]
    [InlineData("unboxed")]
    [InlineData("boxed")]
    public async Task GetUnionElements(string boxingSpecifier)
    {
        await SetupTest(
            $$"""
            {{boxingSpecifier}} union MyUnion
            {
                A,
                B(u64, string),
                C{field a: u64, field b: string},
            }
            var a = MyUnion::A;
            var b = MyUnion::B(1, "hi");
            var c = new MyUnion::C { a = 2, b = "bye", };

            var values = [unboxed; a, b, c];

            var i = 0;
            while (i < values.length)
            {
                match (values[i]) {
                    MyUnion::A => print_string("MyUnion::A"),
                    MyUnion::B(var tuple_num, var tuple_string) => {
                        print_string("MyUnion::B(");
                        print_u64(tuple_num);
                        print_string(", ");
                        print_string(tuple_string);
                        print_string(")");
                    },
                    MyUnion::C{a: var class_num, b: var class_string} => {
                        print_string("MyUnion::C { a = ");
                        print_u64(class_num);
                        print_string(", b = ");
                        print_string(class_string);
                        print_string(" }");
                    },
                    _ => {},
                }
                print_string("\n");
                i = i + 1;
            }
            """,
            boxingSpecifier
        );
        var result = await Run(boxingSpecifier);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            MyUnion::A
            MyUnion::B(1, hi)
            MyUnion::C { a = 2, b = bye }

            """
        );
    }

    [Fact, TestMe]
    public async Task VariantOfUnion()
    {
        await SetupTest(
            """
            union MyUnion{A(string), B, C{field a: u32}};

            var a = variantOf MyUnion::B;

            var b = match (a) {
                variantOf MyUnion::A => 6,
                variantOf MyUnion::B => 7,
                variantOf MyUnion::C => 8,
            };

            print_u32(b);
            """
        );

        var result = await Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("7", result.StandardOutput);
    }
}
