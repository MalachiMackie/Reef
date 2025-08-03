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
                              class MyClass {
                                  pub fn MyFn() {
                                  }
                              }
                              var a = new MyClass{};
                              var b = a.MyFn;
                              b();
                              """;
        var expected = Module(
            types:
            [
                Class("MyClass",
                    methods:
                    [
                        Method("MyFn",
                            parameters: [
                                Parameter("this", ConcreteTypeReference("MyClass"))
                            ],
                            instructions:
                            [
                                LoadUnit(0),
                                Return(1)
                            ])
                    ])
            ],
            methods:
            [
                Method("!Main",
                    isStatic: true,
                    locals:
                    [
                        Local("a", ConcreteTypeReference("MyClass")),
                        Local("b", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                        new StoreLocal(Addr(1), 0),
                        new CreateObject(Addr(2), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                        new CopyStack(Addr(3)),
                        new LoadTypeFunction(Addr(4), ConcreteTypeReference("MyClass"), 0),
                        new StoreField(Addr(5), 0, 0),
                        new CopyStack(Addr(6)),
                        new LoadLocal(Addr(7), 0),
                        new StoreField(Addr(8), 0, 1),
                        new StoreLocal(Addr(9), 1),
                        new LoadLocal(Addr(10), 1),
                        new LoadTypeFunction(Addr(11),
                            ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0),
                        new Call(Addr(12)),
                        Drop(13),
                        LoadUnit(14),
                        Return(15)
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