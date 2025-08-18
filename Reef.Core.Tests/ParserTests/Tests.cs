﻿using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Reef.Core.Expressions;
using Xunit.Abstractions;

#pragma warning disable IDE0060 // Remove unused parameter

namespace Reef.Core.Tests.ParserTests;

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
        const string source = "::<";
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
        
        try
        {
            result.ParsedProgram.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion()
                .Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        }
        catch
        {
            testOutputHelper.WriteLine("Expected [{0}], found [{1}]", expectedProgram, result.ParsedProgram);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(ParseErrorTestCases))]
    public void ParseErrorTests(string source, LangProgram expectedProgram, IEnumerable<ParserError> expectedErrors)
    {
        var tokens = Tokenizer.Tokenize(source);

        var output = Parser.Parse(tokens);

        output.Errors.Should().BeEquivalentTo(
            expectedErrors,
            opts => opts.Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        output.ParsedProgram.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion().Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
    }
}
