using FluentAssertions.Equivalency;
using Reef.Core.LoweredExpressions;
using Reef.Core.LoweredExpressions.New;
using Reef.Core.TypeChecking;
using Xunit.Abstractions;

namespace Reef.Core.Tests.NewAbseilTests;

public class NewTestBase
{
    protected ITestOutputHelper TestOutput { get; }

    protected NewTestBase(ITestOutputHelper testOutputHelper)
    {
        TestOutput = testOutputHelper;
    }

    protected static LangProgram CreateProgram(string moduleId, string source)
    {
        var tokens = Tokenizer.Tokenize(source);
        var parseResult = Parser.Parse(moduleId, tokens);
        parseResult.Errors.Should().BeEmpty();
        var program = parseResult.ParsedProgram;
        var typeCheckErrors = TypeChecker.TypeCheck(program);
        typeCheckErrors.Should().BeEmpty();

        return program;
    }

    protected void PrintPrograms(NewLoweredProgram expected, NewLoweredProgram actual, bool parensAroundExpressions = true, bool printValueUseful = true)
    {
        TestOutput.WriteLine("Expected Program:");
        TestOutput.WriteLine(NewPrettyPrinter.PrettyPrintLoweredProgram(expected, parensAroundExpressions, printValueUseful));
        TestOutput.WriteLine("----------------------------------------");
        TestOutput.WriteLine("Actual Program:");
        TestOutput.WriteLine(NewPrettyPrinter.PrettyPrintLoweredProgram(actual, parensAroundExpressions, printValueUseful));
    }
}
