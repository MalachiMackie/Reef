using System.Diagnostics.CodeAnalysis;
using Reef.Core.Common;
using Reef.Core.Expressions;

using static Reef.Core.Tests.ExpressionHelpers;

#pragma warning disable IDE0060 // Remove unused parameter

namespace Reef.Core.Tests.ParserTests;

public class ParserTests(ITestOutputHelper testOutputHelper)
{
    public static IEnumerable<object[]> FailTestCases => TestCases.FailTestCases.TestCases();

    public static IEnumerable<object[]> PopExpressionTestCases => TestCases.PopExpressionTestCases.TestCases();

    public static IEnumerable<object[]> ParseTestCases => TestCases.ParseTestCases.TestCases();

    public static TheoryData<string, LangModule, IEnumerable<ParserError>> ParseErrorTestCases =>
        TestCases.ParseErrorTestCases.TestCases();

    [Theory]
    [MemberData(nameof(FailTestCases))]
    public void FailTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens)
    {
        var result = Parser.Parse(new ModuleId("Tests"), tokens);

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
        var result = Parser.PopExpression(new ModuleId("Tests"), tokens);
        result.Should().NotBeNull();

        try
        {
            result.Should().BeEquivalentTo(expectedExpression,
                opts => opts.AllowingInfiniteRecursion()
                    .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        }
        catch
        {
            testOutputHelper.WriteLine("Expected {0}, found {1}", expectedExpression, result);
            throw;
        }
    }

    [Fact]
    public void SingleTest()
    {

        var source =
            "fn some_fn() where T: boxed{}";

        var expectedProgram = Program("ParseTestCases",
            functions: [Function("some_fn", parameters: [], block: Block().Block)]);

        IEnumerable<ParserError> expectedErrors = [ParserError.ExpectedType(Token.LeftBrace(SourceSpan.Default))];

        var result = Parser.Parse(new ModuleId("ParseTestCases"), Tokenizer.Tokenize(source)).NotNull();

        testOutputHelper.WriteLine("Expected {0}, found {1}", expectedProgram, result.ParsedModule);

        result.Errors.Should().BeEquivalentTo(expectedErrors, opts => opts
            .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        result.ParsedModule.Should().BeEquivalentTo(expectedProgram, opts => opts
            .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
    }

    [Theory]
    [MemberData(nameof(ParseTestCases))]
    public void ParseTest(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        LangModule expectedProgram)
    {
        var result = Parser.Parse(new ModuleId("ParseTestCases"), tokens);

        testOutputHelper.WriteLine(source);

        result.Errors.Should().BeEmpty();

        try
        {
            result.ParsedModule.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion()
                .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        }
        catch
        {
            testOutputHelper.WriteLine("Expected [{0}], found [{1}]", expectedProgram, result.ParsedModule);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(ParseErrorTestCases))]
    public void ParseErrorTests(string source, LangModule expectedProgram, IEnumerable<ParserError> expectedErrors)
    {
        var tokens = Tokenizer.Tokenize(source);

        var output = Parser.Parse(new ModuleId("ParseErrorTestCases"), tokens);

        testOutputHelper.WriteLine(source);

        output.Errors.Should().BeEquivalentTo(
            expectedErrors,
            opts => opts.Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        output.ParsedModule.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion().Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
    }
}
