using FluentAssertions.Execution;
using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.IntegrationTests;

public class StringTests : IntegrationTestBase
{
    [Fact]
    public async Task PrintStringConstant()
    {
        await SetupTest("""print_string("Hello World!");""");

        var result = await Run();

        using var _ = new AssertionScope();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Hello World!");
    }

    [Fact]
    public async Task PrintStringConstantByChars()
    {
        await SetupTest("""
            var str = "Hello World!";
            var chars = str.chars();
            var i = 0;
            while (i < chars.length)
            {
                print_char(chars[i]);
                print_string("\n");
                i = i + 1;
            }
            """);

        var result = await Run();

        using var _ = new AssertionScope();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            $"""
            H
            e
            l
            l
            o
            {' '}
            W
            o
            r
            l
            d
            !

            """
        );
    }

    [Fact]
    public async Task PrintUnicodeString()
    {
        await SetupTest("""print_string("こんにちは");""");

        var result = await Run();

        using var _ = new AssertionScope();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("こんにちは");
    }

    [Fact]
    public async Task PrintUnicodeStringByChars()
    {
        await SetupTest("""
            var str = "こんにちは";
            var chars = str.chars();
            var i = 0;
            while (i < chars.length)
            {
                print_char(chars[i]);
                print_string("\n");
                i = i + 1;
            }
            """);

        var result = await Run();

        using var _ = new AssertionScope();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("""
            こ
            ん
            に
            ち
            は

            """);
    }

    [Fact]
    public async Task PrintUnicodeStringLength()
    {
        await SetupTest("""print_u64("こんにちは".length);""");

        var result = await Run();

        using var _ = new AssertionScope();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("5");
    }

    [Fact]
    public async Task PrintMultipleStringConstants()
    {
        await SetupTest("""print_string("Hello ");print_string("World!!");""");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Hello World!!");
    }

    [Fact]
    public async Task NewLineStringConstants()
    {
        await SetupTest("""print_string("hello\nworld!");""");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"hello{Environment.NewLine}world!");
    }

    [Fact]
    public async Task PrintEmptyString()
    {
        await SetupTest("print_string(\"hi\");print_string(\"\");print_string(\"bye\");");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("hibye");
    }

    [Fact]
    public async Task PrintStringEscapedQuote()
    {
        await SetupTest("""
            print_string("\"hi\"");
            """);

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("\"hi\"");
    }

    [Fact]
    public async Task PrintStringConsistingOnlyOfEscapedCharacter()
    {
        await SetupTest("print_string(\"\\n\");");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(Environment.NewLine);
    }

    [Fact]
    public async Task PrintStringStartingWithEscapedCharacter()
    {
        await SetupTest("print_string(\"\\nsomething\");");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"{Environment.NewLine}something");
    }

    [Fact]
    public async Task PrintStringEndingWithEscapedCharacter()
    {
        await SetupTest("print_string(\"something\\n\");");

        var result = await Run();

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"something{Environment.NewLine}");
    }

    [Fact]
    public async Task MultipleEscapedCharacters()
    {
        await SetupTest("print_string(\"foo\\nbar\\nbaz\");");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"foo{Environment.NewLine}bar{Environment.NewLine}baz");
    }

    [Fact]
    public async Task ConsecutiveEscapedCharacters()
    {
        await SetupTest("print_string(\"foo\\n\\nbar\");");

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"foo{Environment.NewLine}{Environment.NewLine}bar");
    }

    [Fact]
    public async Task StringConcat()
    {
        await SetupTest(""" print_string("fんoo".concat("bんar")); """);

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be($"fんoobんar");
    }

    [Fact]
    public async Task StringSliceToString()
    {
        await SetupTest(
            """
            var original: string = "ん hello ん world";
            var trimmedSlice: string_slice = match (original.slice(3, 11)) {
                result::Ok(var slice) => slice,
                result::Error(var err) => {
                    print_string(err);
                    return;
                }
            };

            print_string_slice(trimmedSlice);
            print_string("\n");
            print_string(trimmedSlice.to_string());
            """
        );

        using var _ = new AssertionScope();

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            ello ん worl
            ello ん worl
            """
        );
    }

    [Fact]
    public async Task UnicodeSliceToString()
    {
        await SetupTest(
            """
            var original: string = "hello world";
            var trimmedSlice: string_slice = match (original.slice(1, 9)) {
                result::Ok(var slice) => slice,
                result::Error(var err) => {
                    print_string(err);
                    return;
                }
            };

            print_string_slice(trimmedSlice);
            print_string("\n");
            print_string(trimmedSlice.to_string());
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            ello worl
            ello worl
            """
        );
    }

    [Fact]
    public async Task StringSlices()
    {
        await SetupTest(
            """
            fn run(): result::<(), string> {
                var original: string = "hello world";
                print_string(original);
                print_string("\n");
                var trimmedSlice: string_slice = original.slice(1, 9)?;

                print_string_slice(trimmedSlice);
                print_string("\n");
                var trimmedAgain = trimmedSlice.slice(1, 7)?;

                print_string_slice(trimmedAgain);
                return ok(());
            }

            if (run() matches result::Error(var err)) {
                print_string("ERROR: ");
                print_string(err);
            }
            """
        );

        var result = await Run();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(
            """
            hello world
            ello worl
            llo wor
            """
        );
    }
}
