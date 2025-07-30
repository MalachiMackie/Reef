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
    
    public static TheoryData<string, string, ReefModule> ControlFlowTestCases() =>
        ControlFlow.TestCases();

    [Theory]
    [MemberData(nameof(ModuleStructureTestCases))]
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
                              static fn SomeFn(): result::<int, string> {
                                  var a = ok(1)?;
                                  return ok(1);
                              }
                              """;
        var expected = Module(
            methods:
            [
                Method("SomeFn",
                    returnType: ConcreteTypeReference(
                        "result",
                        typeArguments:
                        [
                            ConcreteTypeReference("int"),
                            ConcreteTypeReference("string")
                        ]),
                    isStatic: true,
                    locals:
                    [
                        Local("a", ConcreteTypeReference("int"))
                    ],
                    instructions:
                    [
                        new LoadIntConstant(Addr(0), 1),
                        new LoadTypeFunction(Addr(1), ConcreteTypeReference(
                            "result",
                            typeArguments:
                            [
                                ConcreteTypeReference("int"),
                                ConcreteTypeReference("string")
                            ]), 0),
                        new Call(Addr(2)),
                        new CopyStack(Addr(3)),
                        new LoadField(Addr(4), 0, 0),
                        new LoadIntConstant(Addr(5), 1),
                        new CompareIntEqual(Addr(6)),
                        new BranchIfFalse(Addr(7), Addr(9)),
                        new Return(Addr(8)),
                        new LoadField(Addr(9), 0, 1),
                        new StoreLocal(Addr(10), 0),
                        new LoadIntConstant(Addr(11), 1),
                        new LoadTypeFunction(Addr(12), ConcreteTypeReference(
                            "result",
                            typeArguments:
                            [
                                ConcreteTypeReference("int"),
                                ConcreteTypeReference("string")
                            ]), 0),
                        new Call(Addr(13)),
                        Return(14)
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