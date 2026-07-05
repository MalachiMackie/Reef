using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class IntOperations : IntegrationTestBase
{
    public static readonly TheoryData<string> IntTypes =
    [
        "i64",
        "i32",
        "i16",
        "i8",
        "u64",
        "u32",
        "u16",
        "u8"
    ];

    public static readonly TheoryData<string, int, int> NumberData = [.. IntTypes.SelectMany(x => new[] { (x.Data, 1, 2), (x.Data, 20, 120) })];

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task PrintPositiveInts(string typeSpecifier)
    {
        await SetupTest($"print_{typeSpecifier}(3);", typeSpecifier);

        var result = await Run(typeSpecifier);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("3");
    }


    [Theory]
    [InlineData("i64")]
    [InlineData("i32")]
    [InlineData("i16")]
    [InlineData("i8")]
    public async Task PrintNegativeInts(string typeSpecifier)
    {
        await SetupTest($"print_{typeSpecifier}(0 - 4);", typeSpecifier);

        var result = await Run(typeSpecifier);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("-4");
    }

    [Fact]
    public async Task NegateIntLiteral()
    {
        await SetupTest("print_i32(-1);");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("-1");
    }

    [Fact]
    public async Task NegateVariable()
    {
        await SetupTest("""
                        var a = 1;
                        print_i32(-a);
                        """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("-1");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task ModuloIntTypes(string type)
    {
        await SetupTest(
            $"""
            var a: {type} = 6 % 4;
            print_{type}(a);
            """
        );

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("2", result.StandardOutput);
    }

    [Theory]
    [InlineData(3, 5, 3)]
    [InlineData(15, 5, 0)]
    [InlineData(5, 5, 0)]
    [InlineData(6, 5, 1)]
    public async Task Modulo(int left, int right, int expected)
    {
        await SetupTest(
            $"""
            var a = {left} % {right};
            print_u32(a);
            """
        );

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected.ToString(), result.StandardOutput);
    }

    [Fact]
    public async Task Increment()
    {
        await SetupTest(
            """
            var a = 1;
            var b = a++;
            print_u32(a);
            print_u32(b);
            """
        );

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("21", result.StandardOutput);
    }

    [Fact]
    public async Task Decrement()
    {
        await SetupTest(
            """
            var a = 3;
            var b = a--;
            print_u32(a);
            print_u32(b);
            """
        );

        var result = await Run();
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("23", result.StandardOutput);
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task IntEquals(string typeSpecifier)
    {
        await SetupTest($$"""
            var one: {{typeSpecifier}} = 1;
            var two: {{typeSpecifier}} = 2;
            if (one == one) {
                print_string("1 == 1");
            }
            if (one == two) {
                print_string("1 == 2");
            }
            """,
            typeSpecifier);

        var output = await Run(typeSpecifier);
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("1 == 1");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task IntNotEquals(string typeSpecifier)
    {
        await SetupTest($$"""
            var one: {{typeSpecifier}} = 1;
            var zero: {{typeSpecifier}} = 0;
            if (one != zero) {
                print_string("1 != 0");
            }
            if (one != one) {
                print_string("1 != 1");
            }
            """, typeSpecifier);

        var output = await Run(typeSpecifier);
        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("1 != 0");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task Add(string typeSpecifier)
    {
        await SetupTest(
            $$"""
            var one: {{typeSpecifier}} = 1;
            var two: {{typeSpecifier}} = 2;
            var a = one + two;
            if (a == 3) {
                print_string("a == 3");
            } else {
                print_string("a != 3");
            }
            """, typeSpecifier);

        var output = await Run(typeSpecifier);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 3");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task Subtract(string typeSpecifier)
    {
        await SetupTest(
            $$"""
            var four: {{typeSpecifier}} = 4;
            var one: {{typeSpecifier}} = 1;
            var a = four - one;
            if (a == 3) {
                print_string("a == 3");
            } else {
                print_string("a != 3");
            }
            """, typeSpecifier);

        var output = await Run(typeSpecifier);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 3");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task Multiply(string typeSpecifier)
    {
        await SetupTest(
            $$"""
            var four: {{typeSpecifier}} = 4;
            var two: {{typeSpecifier}} = 2;
            var a = four * two;
            if (a == 8) {
                print_string("a == 8");
            } else {
                print_string("a != 8");
            }
            """, typeSpecifier);

        var output = await Run(typeSpecifier);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 8");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task Divide(string typeSpecifier)
    {
        await SetupTest(
            $$"""
            var four: {{typeSpecifier}} = 4;
            var two: {{typeSpecifier}} = 2;
            var a = four / two;
            if (a == 2) {
                print_string("a == 2");
            } else {
                print_string("a != 2");
            }
            """, typeSpecifier);

        var output = await Run(typeSpecifier);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 2");
    }

    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task DivideNonEvenly(string typeSpecifier)
    {
        await SetupTest(
            $$"""
            var four: {{typeSpecifier}} = 4;
            var three: {{typeSpecifier}} = 3;
            var a = four / three;
            if (a == 1) {
                print_string("a == 1");
            } else {
                print_string("a != 1");
            }
            """, typeSpecifier);

        var output = await Run(typeSpecifier);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 1");
    }

    [Theory]
    [MemberData(nameof(NumberData))]
    public async Task GreaterThan(string typeSpecifier, int small, int large)
    {
        var testCaseName = $"{typeSpecifier}_{small}_{large}";
        await SetupTest(
            $$"""
            var small: {{typeSpecifier}} = {{small}};
            var large: {{typeSpecifier}} = {{large}};
            if (small > large) {
                print_string("{{small}} > {{large}}");
            }
            if (large > small) {
                print_string("{{large}} > {{small}}");
            }
            if (large > large) {
                print_string("{{large}} > {{large}}");
            }
            """, testCaseName
        );

        var result = await Run(testCaseName);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{large} > {small}", result.StandardOutput);
    }

    [Theory]
    [MemberData(nameof(NumberData))]
    public async Task GreaterThanOrEqual(string typeSpecifier, int small, int large)
    {
        var testCaseName = $"{typeSpecifier}_{small}_{large}";
        await SetupTest(
            $$"""
            var small: {{typeSpecifier}} = {{small}};
            var large: {{typeSpecifier}} = {{large}};
            if (small >= large) {
                print_string("{{small}} >= {{large}}, ");
            }
            if (large >= small) {
                print_string("{{large}} >= {{small}}, ");
            }
            if (large >= large) {
                print_string("{{large}} >= {{large}}, ");
            }
            """, testCaseName
        );

        var result = await Run(testCaseName);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{large} >= {small}, {large} >= {large}, ", result.StandardOutput);
    }

    [Theory]
    [MemberData(nameof(NumberData))]
    public async Task LessThan(string typeSpecifier, int small, int large)
    {
        var testCaseName = $"{typeSpecifier}_{small}_{large}";
        await SetupTest(
            $$"""
              var small: {{typeSpecifier}} = {{small}};
              var large: {{typeSpecifier}} = {{large}};
              if (small < large) {
                  print_string("{{small}} < {{large}}");
              }
              if (large < small) {
                  print_string("{{large}} < {{small}}");
              }
              if (large < large) {
                  print_string("{{large}} > {{large}}");
              }
              """, testCaseName
        );

        var result = await Run(testCaseName);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{small} < {large}", result.StandardOutput);
    }


    [Theory]
    [MemberData(nameof(NumberData))]
    public async Task LessThanOrEqual(string typeSpecifier, int small, int large)
    {
        var testCaseName = $"{typeSpecifier}_{small}_{large}";
        await SetupTest(
            $$"""
                var small: {{typeSpecifier}} = {{small}};
                var large: {{typeSpecifier}} = {{large}};
                if (small <= large) {
                    print_string("{{small}} <= {{large}}, ");
                }
                if (large <= small) {
                    print_string("{{large}} <= {{small}}, ");
                }
                if (large <= large) {
                    print_string("{{large}} <= {{large}}, ");
                }
                """, testCaseName
        );

        var result = await Run(testCaseName);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{small} <= {large}, {large} <= {large}, ", result.StandardOutput);
    }
}
