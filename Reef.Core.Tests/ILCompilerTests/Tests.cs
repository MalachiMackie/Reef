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
                var mut a = 1;
                fn InnerFn() {
                    var b = a;
                    a = 2;
                }
                """;
        var expected = Module(
            types:
            [
                Class("!Main_Locals",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                    ])
            ],
            methods:
            [
                Method("InnerFn",
                    locals:
                    [
                        Local("b", ConcreteTypeReference("int"))
                    ],
                    parameters:
                    [
                        Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals"))
                    ],
                    instructions:
                    [
                        new LoadArgument(Addr(0), 0),
                        new LoadField(Addr(1), 0, 0),
                        new StoreLocal(Addr(2), 0),
                        new LoadIntConstant(Addr(3), 2),
                        new LoadArgument(Addr(4), 0),
                        new StoreField(Addr(5), 0, 0),
                        LoadUnit(6),
                        Return(7)
                    ]),
                Method("!Main",
                    isStatic: true,
                    locals:
                    [
                        Local("locals", ConcreteTypeReference("!Main_Locals")),
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                        new StoreLocal(Addr(1), 0),
                        new LoadIntConstant(Addr(2), 1),
                        new LoadLocal(Addr(3), 0),
                        new StoreField(Addr(4), 0, 0),
                        LoadUnit(5),
                        Return(6)
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