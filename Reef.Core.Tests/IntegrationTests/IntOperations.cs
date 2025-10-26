using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

namespace Reef.Core.Tests.IntegrationTests;

public class IntOperations : IntegrationTestBase
{
    public static readonly TheoryData<string> IntTypes = new(){
        "i64",
        "i32",
        "i16",
        "i8",
        "u64",
        "u32",
        "u16",
        "u8",
    };
    
    [Theory]
    [MemberData(nameof(IntTypes))]
    public async Task IntEquals(string typeSpecifier)
    {
        await SetupTest($$"""
            var one: {{typeSpecifier}} = 1;
            var two: {{typeSpecifier}} = 2;
            if (one == one) {
                printf("1 == 1");
            }
            if (one == two) {
                printf("1 == 2");
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
                printf("1 != 0");
            }
            if (one != one) {
                printf("1 != 1");
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
                printf("a == 3");
            } else {
                printf("a != 3");
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
                printf("a == 3");
            } else {
                printf("a != 3");
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
                printf("a == 8");
            } else {
                printf("a != 8");
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
                printf("a == 2");
            } else {
                printf("a != 2");
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
                printf("a == 1");
            } else {
                printf("a != 1");
            }
            """);

        var output = await Run();

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Be("a == 1");
    }
}
