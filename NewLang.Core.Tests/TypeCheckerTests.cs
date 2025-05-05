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

    public static TheoryData<LangProgram> SuccessfulExpressionTestCases() =>
        ConvertToPrograms(
        [
            "var a = 2",
            "var a: int = 2",
            "var b: string = \"somestring\"",
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