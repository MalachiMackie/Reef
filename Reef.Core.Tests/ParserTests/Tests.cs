using System.Diagnostics.CodeAnalysis;
using Reef.Core.Common;
using Reef.Core.Expressions;

using static Reef.Core.Tests.ExpressionHelpers;

#pragma warning disable IDE0060 // Remove unused parameter

namespace Reef.Core.Tests.ParserTests;

public class ParserTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases.FailTestCases.TestCases), MemberType = typeof(TestCases.FailTestCases))]
    public void FailTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens)
    {
        var result = Parser.Parse(new ModuleId("Tests"), tokens);

        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(TestCases.PopExpressionTestCases.TestCases), MemberType = typeof(TestCases.PopExpressionTestCases))]
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

        var source = "new A;";
        var expectedProgram = Program("ParseTestCases", [ObjectInitializer(NamedTypeIdentifier("A"))]);

        IEnumerable<ParserError> expectedErrors = [
            ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.DoubleColon)
        ];

        var tokens = Tokenizer.Tokenize(source);

        tokens.Errors.Should().BeEmpty();

        var result = Parser.Parse(expectedProgram.ModuleId, tokens.Tokens).NotNull();

        testOutputHelper.WriteLine("Expected {0}, found {1}", expectedProgram, result.ParsedModule);

        result.Errors.Should().BeEquivalentTo(expectedErrors, opts => opts
            .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        result.ParsedModule.Should().BeEquivalentTo(expectedProgram, opts => opts
            .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
    }

    [Theory]
    [MemberData(nameof(TestCases.ParseTestCases.TestCases), MemberType = typeof(TestCases.ParseTestCases))]
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

    [Theory(Timeout = 3000)]
    [MemberData(nameof(TestCases.ParseErrorTestCases.TestCases), MemberType = typeof(TestCases.ParseErrorTestCases))]
    public void ParseErrorTests(string source, LangModule expectedProgram, IEnumerable<ParserError> expectedErrors)
    {
        var tokens = Tokenizer.Tokenize(source);

        tokens.Errors.Should().BeEmpty();

        testOutputHelper.WriteLine(source);

        var output = Parser.Parse(new ModuleId("ParseErrorTestCases"), tokens.Tokens);

        output.Errors.Should().BeEquivalentTo(
            expectedErrors,
            opts => opts.Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        output.ParsedModule.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion().Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
    }
}
