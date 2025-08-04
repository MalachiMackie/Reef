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
                    pub fn MyFn<T>(){}
                }
                var a = new MyClass{};
                a.MyFn::<string>();
                """;
        var expected = Module(
            types:
            [
                Class("MyClass",
                    methods:
                    [
                        Method("MyFn",
                            typeParameters: ["T"],
                            parameters:
                            [
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
                        Local("a", ConcreteTypeReference("MyClass"))
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                        new StoreLocal(Addr(1), 0),
                        new LoadLocal(Addr(2), 0),
                        new LoadTypeFunction(Addr(3), ConcreteTypeReference("MyClass"), 0,
                            [ConcreteTypeReference("string")]),
                        new Call(Addr(4)),
                        Drop(5),
                        LoadUnit(6),
                        Return(7)
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