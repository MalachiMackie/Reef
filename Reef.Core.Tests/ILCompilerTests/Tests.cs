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

    [Theory]
    [MemberData(nameof(ModuleStructureTestCases))]
    [MemberData(nameof(SimpleExpressionTestCases))]
    [MemberData(nameof(ClosureTestCases))]
    [MemberData(nameof(ClassTestCases))]
    [MemberData(nameof(TestCases))]
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
        const string source = "";
        var expected = Module();

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

    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            {
                "access parameter",
                """
                static fn SomeFn(a: int, b: int) {
                    var foo = a;
                    var bar = b;
                }
                """,
                Module(
                    methods:
                    [
                        Method("SomeFn",
                            isStatic: true,
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "foo", Type = ConcreteTypeReference("int") },
                                new ReefMethod.Local { DisplayName = "bar", Type = ConcreteTypeReference("int") },
                            ],
                            parameters:
                            [
                                Parameter("a", ConcreteTypeReference("int")),
                                Parameter("b", ConcreteTypeReference("int")),
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 1),
                                new StoreLocal(Addr(3), 1),
                                LoadUnit(4),
                                Return(5)
                            ]),
                    ])
            },
            {
                "call global method",
                """
                static fn FirstFn(){}
                FirstFn();
                """,
                Module(
                    methods:
                    [
                        Method("FirstFn", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                        Method("!Main", isStatic: true, instructions:
                        [
                            new LoadGlobalFunction(Addr(0), FunctionReference("FirstFn")),
                            new Call(Addr(1)),
                            Drop(2),
                            LoadUnit(3),
                            Return(4)
                        ])
                    ]
                )
            },
        };
    }
}