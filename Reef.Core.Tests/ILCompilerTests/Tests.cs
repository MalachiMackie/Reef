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
                              fn MyFn() {}
                              var a = MyFn;
                              fn OtherFn() {
                                  a();
                              }
                              OtherFn();
                              """;
        var expected = Module(
            types:
            [
                Class("!Main_Locals", fields:
                [
                    Field("Field_0", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]),
                        isPublic: true),
                ]),
                Class("OtherFn_Closure",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                    ])
            ],
            methods:
            [
                Method("MyFn", instructions:
                [
                    LoadUnit(0),
                    Return(1)
                ]),
                Method("OtherFn",
                    parameters:
                    [
                        Parameter("closureParameter", ConcreteTypeReference("OtherFn_Closure")),
                    ],
                    instructions:
                    [
                        new LoadArgument(Addr(0), 0),
                        new LoadField(Addr(1), 0, 0),
                        new LoadField(Addr(2), 0, 0),
                        new LoadTypeFunction(Addr(3),
                            ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0),
                        new Call(Addr(4)),
                        Drop(5),
                        LoadUnit(6),
                        Return(7)
                    ]),
                Method("!Main",
                    isStatic: true,
                    locals:
                    [
                        Local("locals", ConcreteTypeReference("!Main_Locals"))
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                        new StoreLocal(Addr(1), 0),
                        new LoadLocal(Addr(2), 0),
                        new CreateObject(Addr(3), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                        new CopyStack(Addr(4)),
                        new LoadGlobalFunction(Addr(5), FunctionDefinitionReference("MyFn")),
                        new StoreField(Addr(6), 0, 0),
                        new StoreField(Addr(7), 0, 0),
                        new CreateObject(Addr(8), ConcreteTypeReference("OtherFn_Closure")),
                        new CopyStack(Addr(9)),
                        new LoadLocal(Addr(10), 0),
                        new StoreField(Addr(11), 0, 0),
                        new LoadGlobalFunction(Addr(12), FunctionDefinitionReference("OtherFn")),
                        new Call(Addr(13)),
                        Drop(14),
                        LoadUnit(15),
                        Return(16)
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