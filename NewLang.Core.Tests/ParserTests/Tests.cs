using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Xunit.Abstractions;
using static NewLang.Core.Tests.ParserTests.RemoveSourceSpanHelpers;
using static NewLang.Core.Tests.ParserTests.ParserHelpers;

#pragma warning disable IDE0060 // Remove unused parameter

namespace NewLang.Core.Tests.ParserTests;

public class Tests(ITestOutputHelper testOutputHelper)
{
    public static IEnumerable<object[]> FailTestCases => TestCases.FailTestCases.TestCases();

    public static IEnumerable<object[]> PopExpressionTestCases => TestCases.PopExpressionTestCases.TestCases();

    public static IEnumerable<object[]> ParseTestCases => TestCases.ParseTestCases.TestCases();

    public static TheoryData<string, LangProgram, IEnumerable<ParserError>> ParseErrorTestCases =>
        TestCases.ParseErrorTestCases.TestCases();

    [Theory]
    [MemberData(nameof(FailTestCases))]
    public void FailTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens)
    {
        var result = Parser.Parse(tokens);

        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(PopExpressionTestCases))]
    public void PopExpressionTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        IExpression expectedExpression)
    {
        var result = Parser.PopExpression(tokens);
        result.Should().NotBeNull();

        // clear out the source spans, we don't care about them
        var expression = RemoveSourceSpan(result);

        try
        {
            expression.Should().BeEquivalentTo(expectedExpression, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected {0}, found {1}", expectedExpression, expression);
            throw;
        }
    }

    [Fact]
    public void SingleTest()
    {
        const string source = "class MyClass<T,,,,T2>{}";
        var result = Parser.Parse(Tokenizer.Tokenize(source));
        result.Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(ParseTestCases))]
    public void ParseTest(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        LangProgram expectedProgram)
    {
        var result = Parser.Parse(tokens);

        result.Errors.Should().BeEmpty();
        
        var program = RemoveSourceSpan(result.ParsedProgram);

        try
        {
            program.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected [{0}], found [{1}]", expectedProgram, program);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(ParseErrorTestCases))]
    public void ParseErrorTests(string source, LangProgram expectedProgram, IEnumerable<ParserError> expectedErrors)
    {
        var tokens = Tokenizer.Tokenize(source);

        var output = Parser.Parse(tokens);

        expectedProgram = RemoveSourceSpan(expectedProgram);
        expectedErrors = RemoveSourceSpan(expectedErrors.ToArray());
        var program = RemoveSourceSpan(output.ParsedProgram);
        var errors = RemoveSourceSpan(output.Errors);

        program.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion());
        errors.Should().BeEquivalentTo(expectedErrors);
    }
}