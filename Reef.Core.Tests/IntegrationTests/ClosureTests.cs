using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

[TestMe]
public class ClosureTests : IntegrationTestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaticClosureWithNoArgumentsOrReturnValue(bool check)
    {
        await SetupTest(
            $$"""
            fn hello() {
                print_string("hello!");
            }
            fn good_bye() {
                print_string("good bye!");
            }

            var print: Fn();

            if ({{(check ? "true" : "false")}}) {
                print = hello;
            }
            else {
                print = good_bye;
            }

            print();
            """, testCaseName: check.ToString());

        var result = await Run(testCaseName: check.ToString());

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(check ? "hello!" : "good bye!");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaticClosureWithNoArgumentsButHasReturnValue(bool check)
    {
        await SetupTest(
            $$"""
            fn hello(): string {
                return "hello!";
            }
            fn good_bye(): string {
                return "good bye!";
            }

            var get_value: Fn(): string;

            if ({{(check ? "true" : "false")}}) {
                get_value = hello;
            }
            else {
                get_value = good_bye;
            }

            print_string(get_value());
            """, testCaseName: check.ToString());

        var result = await Run(testCaseName: check.ToString());

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(check ? "hello!" : "good bye!");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaticClosureWithArgumentsAndReturnValue(bool check)
    {
        await SetupTest(
            $$"""
            fn plus_1(param: u64): u64 {
                return param + 1;
            }
            fn plus_2(param: u64): u64 {
                return param + 2;
            }

            var get_value: Fn(u64): u64;

            if ({{(check ? "true" : "false")}}) {
                get_value = plus_1
            }
            else {
                get_value = plus_2;
            }

            var value = get_value(2);
            print_u64(value);

            """, testCaseName: check.ToString());

        var result = await Run(testCaseName: check.ToString());

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(check ? "3" : "4");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonStaticClosureWithArgumentsAndReturnValue(bool check)
    {
        await SetupTest(
            $$"""
            var base_value = 3;

            fn plus_1(param: u64): u64 {
                return param + base_value + 1;
            }
            fn plus_2(param: u64): u64 {
                return param + base_value + 2;
            }

            var get_value: Fn(u64): u64;

            if ({{(check ? "true" : "false")}}) {
                get_value = plus_1
            }
            else {
                get_value = plus_2;
            }

            var value = get_value(2);
            print_u64(value);

            """, testCaseName: check.ToString());

        var result = await Run(testCaseName: check.ToString());

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(check ? "6" : "7");
    }
}
