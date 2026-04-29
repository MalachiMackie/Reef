using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Reef.Core.Tests.IntegrationTests.Helpers;

namespace Reef.Core.Tests.AbseilTests;

public class TestBase
{
    protected ITestOutputHelper TestOutput { get; }

    protected TestBase(ITestOutputHelper testOutputHelper)
    {
        TestOutput = testOutputHelper;
    }

    protected static LoweredProgram Lower(LangModule module)
    {
        return ProgramAbseil.Lower(new() { { module.ModuleId, module } }, module.ModuleId);
    }

    protected async Task<LangModule> CreateProgram(ModuleId moduleId, string source)
    {
        var fs = new MockFileSystem();
        fs.AddFilesFromEmbeddedNamespace("", typeof(TestBase).Assembly, "reef-std");

        fs.AddFile("main.rf", new MockFileData(Encoding.UTF8.GetBytes(source)));

        var (results, _, _, _) = await new ReefCompiler(fs, moduleId, new TestLogger(TestOutput)).TypeCheck();

        var moduleResult = results[moduleId];

        moduleResult.ParserErrors.Should().BeEmpty();
        moduleResult.TypeCheckerErrors.Should().BeEmpty();

        return moduleResult.Module;
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
