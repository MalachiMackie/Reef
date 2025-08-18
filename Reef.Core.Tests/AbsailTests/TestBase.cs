using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.TypeChecking;

namespace Reef.Core.Tests.AbsailTests;

public class TestBase
{
    protected TestBase() { }

    protected LangProgram CreateProgram(string source)
    {
        var tokens = Tokenizer.Tokenize(source);
        var parseResult = Parser.Parse(tokens);
        parseResult.Errors.Should().BeEmpty();
        var program = parseResult.ParsedProgram;
        var typeCheckErrors = TypeChecker.TypeCheck(program);
        typeCheckErrors.Should().BeEmpty();

        return program;
    }

    protected EquivalencyOptions<T> IgnoringGuids<T>(EquivalencyOptions<T> opts)
    {
        return opts.Excluding(m => m.Type == typeof(Guid));
    }
}
