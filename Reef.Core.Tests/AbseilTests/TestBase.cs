using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;

namespace Reef.Core.Tests.AbseilTests;

public class TestBase
{
    protected ITestOutputHelper TestOutput { get; }

    protected TestBase(ITestOutputHelper testOutputHelper)
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

    protected void PrintPrograms(LoweredProgram expected, LoweredProgram actual, bool parensAroundExpressions = true, bool printValueUseful = true)
    {
        TestOutput.WriteLine("Expected Program:");
        TestOutput.WriteLine(PrettyPrinter.PrettyPrintLoweredProgram(expected, parensAroundExpressions, printValueUseful));
        TestOutput.WriteLine("----------------------------------------");
        TestOutput.WriteLine("Actual Program:");
        TestOutput.WriteLine(PrettyPrinter.PrettyPrintLoweredProgram(actual, parensAroundExpressions, printValueUseful));
    }
}
