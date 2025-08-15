using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Tests.ILCompilerTests.TestCases;
using Reef.IL;
using Reef.Core.TypeChecking;

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
                union MyUnion {
                    A,
                    B(string),
                }
                
                var a = MyUnion::B;
                var b = a("");
                """;
                var expected = Module(
                    types: [
                        Union("MyUnion",
                            variants: [
                                Variant("A", fields: [Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true)]),
                                Variant("B", fields: [
                                    Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                                    Field("First", ConcreteTypeReference("string"), isPublic: true),
                                ]),
                            ],
                            methods: [
                                Method("MyUnion_B_Create",
                                    isStatic: true,
                                    parameters: [
                                        Parameter("First", ConcreteTypeReference("string")),
                                    ],
                                    returnType: ConcreteTypeReference("MyUnion"),
                                    instructions: [
                                        new CreateObject(Addr(0), ConcreteTypeReference("MyUnion")),
                                        new CopyStack(Addr(1)),
                                        new LoadIntConstant(Addr(2), 1),
                                        new StoreField(Addr(3), 1, 0),
                                        new CopyStack(Addr(4)),
                                        new LoadArgument(Addr(5), 0),
                                        new StoreField(Addr(6), 1, 1),
                                        Return(7)
                                    ]),
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("Function`2", [ConcreteTypeReference("string"), ConcreteTypeReference("MyUnion")])),
                                Local("b", ConcreteTypeReference("MyUnion"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Function`2", [ConcreteTypeReference("string"), ConcreteTypeReference("MyUnion")])),
                                new CopyStack(Addr(1)),
                                new LoadTypeFunction(Addr(2), ConcreteTypeReference("MyUnion"), 0, []),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadStringConstant(Addr(6), ""),
                                new LoadTypeFunction(
                                    Addr(7),
                                    ConcreteTypeReference("Function`2", [ConcreteTypeReference("string"), ConcreteTypeReference("MyUnion")]),
                                    0,
                                    []),
                                new Call(Addr(8)),
                                new StoreLocal(Addr(9), 1),
                                LoadUnit(10),
                                Return(11)
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
