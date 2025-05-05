using FluentAssertions;

namespace NewLang.Core.Tests;

public class TypeCheckerTests
{
    [Theory]
    [MemberData(nameof(SuccessfulExpressionTestCases))]
    public void Should_SuccessfullyTypeCheckExpressions(LangProgram program)
    {
        var act = () => TypeChecker.TypeCheck(program);
        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(FailedExpressionTestCases))]
    public void Should_FailTypeChecking_When_ExpressionsAreNotValid(LangProgram program)
    {
        var act = () => TypeChecker.TypeCheck(program);

        act.Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<LangProgram> SuccessfulExpressionTestCases() =>
        ConvertToPrograms(
        [
            "var a = 2",
            "var a: int = 2",
            "var b: string = \"somestring\"",
        ]);

    public static TheoryData<LangProgram> FailedExpressionTestCases() =>
        ConvertToPrograms([
            "var a: string = 2",
            "var a: int = \"somestring\"",
            "var b;"
        ]);

    private static TheoryData<LangProgram> ConvertToPrograms(IEnumerable<string> input)
    {
        var theoryData = new TheoryData<LangProgram>();
        foreach (var program in input.Select(Tokenizer.Tokenize).Select(Parser.Parse))
        {
            theoryData.Add(program);
        }

        return theoryData;
    }
}