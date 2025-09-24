using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.Tests.ILCompilerTests.TestCases;
using Reef.IL;
using Reef.Core.TypeChecking;

using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests;

public class Tests
{
    public static TheoryData<string, string, ReefModule> ClosureTestCases() =>
        ClosureTests.TestCases();

    public static TheoryData<string, string, ReefModule> SimpleExpressionTestCases() =>
        SimpleExpressions.TestCases();

    public static TheoryData<string, string, ReefModule> ClassTestCases() =>
        ClassTests.TestCases();

    public static TheoryData<string, string, ReefModule> UnionTestCases() =>
        UnionTests.TestCases();

    public static TheoryData<string, string, ReefModule> MethodTestCases() =>
        MethodTests.TestCases();

    public static TheoryData<string, string, ReefModule> ControlFlowTestCases() =>
        ControlFlow.TestCases();

    [Theory]
    [MemberData(nameof(SimpleExpressionTestCases))]
    [MemberData(nameof(ClosureTestCases))]
    [MemberData(nameof(ClassTestCases))]
    [MemberData(nameof(UnionTestCases))]
    [MemberData(nameof(MethodTestCases))]
    [MemberData(nameof(ControlFlowTestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var module = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }

    [Fact]
    public void SingleTest()
    {
        const string source = """
                union MyUnion {
                    A,
                    B(string),
                }
                
                var a = MyUnion::B;
                var b = a("");
                """;
        var expected = Module();

        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram);
        
        var module = ILCompile.CompileToIL(loweredProgram);

        module.Should().NotBeNull();
        module.Should().BeEquivalentTo(expected, ConfigureEquivalencyCheck);
    }

    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid));
    }
}
