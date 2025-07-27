using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Tests.ILCompilerTests.TestCases;
using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests;

public class Tests
{
    public static TheoryData<string, string, ReefModule> ClosureTestCases() =>
        ClosureTests.TestCases();

    public static TheoryData<string, string, ReefModule> ModuleStructureTestCases() =>
        ModuleStructure.TestCases();
    
    public static TheoryData<string, string, ReefModule> SimpleExpressionTestCases() =>
        SimpleExpressions.TestCases();
    
    public static TheoryData<string, string, ReefModule> ClassTestCases() =>
        ClassTests.TestCases();
    
    public static TheoryData<string, string, ReefModule> UnionTestCases() =>
        UnionTests.TestCases();
    
    public static TheoryData<string, string, ReefModule> MethodTestCases() =>
        MethodTests.TestCases();

    [Theory]
    [MemberData(nameof(ModuleStructureTestCases))]
    [MemberData(nameof(SimpleExpressionTestCases))]
    [MemberData(nameof(ClosureTestCases))]
    [MemberData(nameof(ClassTestCases))]
    [MemberData(nameof(UnionTestCases))]
    [MemberData(nameof(MethodTestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var module = ILCompile.CompileToIL(program.ParsedProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }

    [Fact]
    public void SingleTest()
    {
        const string source = """
                static fn SomeFn() {
                    { 
                        fn InnerFn() {
                        }
                        
                        InnerFn();
                    }
                }
                """;
        var expected = Module(
            methods:
            [
                Method(
                    "InnerFn",
                    isStatic: false,
                    instructions: [LoadUnit(0), Return(1)]),
                Method("SomeFn",
                    isStatic: true,
                    instructions:
                    [
                        new LoadGlobalFunction(Addr(0), FunctionReference("InnerFn")),
                        new Call(Addr(1)),
                        Drop(2),
                        LoadUnit(3),
                        Return(4)
                    ])
            ]);

        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var module = ILCompile.CompileToIL(program.ParsedProgram);

        module.Should().NotBeNull();
        module.Should().BeEquivalentTo(expected, ConfigureEquivalencyCheck);
    }

    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid))
            .WithStrictTypingFor(x => x.CompileTimeType == typeof(IInstruction));
    }
}