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
                static fn First(a: string) {
                    fn Second() {
                        fn Third() {
                            var c = 1;
                            fn Fourth() {
                                var b = a;
                                var d = c;
                            }
                            
                            Fourth();
                        }
                        Third();
                    }
                    Second();
                }
                """;
        var expected = Module(
            types:
            [
                Class("Fourth_Closure",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("First_Locals"), isPublic: true),
                        Field("Field_1", ConcreteTypeReference("Third_Locals"), isPublic: true)
                    ]),
                Class("Third_Locals",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                    ]),
                Class("Third_Closure",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("First_Locals"), isPublic: true)
                    ]),
                Class("Second_Closure",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("First_Locals"), isPublic: true)
                    ]),
                Class("First_Locals",
                    fields:
                    [
                        Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                    ]),
            ],
            methods:
            [
                Method("Fourth",
                    parameters:
                    [
                        Parameter("closureParameter", ConcreteTypeReference("Fourth_Closure")),
                    ],
                    locals:
                    [
                        Local("b", ConcreteTypeReference("string")),
                        Local("d", ConcreteTypeReference("int"))
                    ],
                    instructions:
                    [
                        new LoadArgument(Addr(0), 0),
                        new LoadField(Addr(1), 0, 0),
                        new LoadField(Addr(2), 0, 0),
                        new StoreLocal(Addr(3), 0),
                        new LoadArgument(Addr(4), 0),
                        new LoadField(Addr(5), 0, 1),
                        new LoadField(Addr(6), 0, 0),
                        new StoreLocal(Addr(7), 1),
                        LoadUnit(8),
                        Return(9)
                    ]),
                Method("Third",
                    parameters:
                    [
                        Parameter("closureParameter", ConcreteTypeReference("Third_Closure"))
                    ],
                    locals:
                    [
                        Local("locals", ConcreteTypeReference("Third_Locals"))
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("Third_Locals")),
                        new StoreLocal(Addr(1), 0),
                        new LoadIntConstant(Addr(2), 1),
                        new LoadLocal(Addr(3), 0),
                        new StoreField(Addr(4), 0, 0),
                        new CreateObject(Addr(5), ConcreteTypeReference("Fourth_Closure")),
                        new CopyStack(Addr(6)),
                        new LoadArgument(Addr(7), 0),
                        new LoadField(Addr(8), 0, 0),
                        new StoreField(Addr(9), 0, 0),
                        new CopyStack(Addr(10)),
                        new LoadLocal(Addr(11), 0),
                        new StoreField(Addr(12), 0, 1),
                        new LoadGlobalFunction(Addr(13), FunctionDefinitionReference("Fourth")),
                        new Call(Addr(14)),
                        Drop(15),
                        LoadUnit(16),
                        Return(17)
                    ]),
                Method("Second",
                    parameters:
                    [
                        Parameter("closureParameter", ConcreteTypeReference("Second_Closure"))
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("Third_Closure")),
                        new CopyStack(Addr(1)),
                        new LoadArgument(Addr(2), 0),
                        new LoadField(Addr(3), 0, 0),
                        new StoreField(Addr(4), 0, 0),
                        new LoadGlobalFunction(Addr(5), FunctionDefinitionReference("Third")),
                        new Call(Addr(6)),
                        Drop(7),
                        LoadUnit(8),
                        Return(9)
                    ]),
                Method("First",
                    isStatic: true,
                    parameters:
                    [
                        Parameter("a", ConcreteTypeReference("string"))
                    ],
                    locals:
                    [
                        Local("locals", ConcreteTypeReference("First_Locals"))
                    ],
                    instructions:
                    [
                        new CreateObject(Addr(0), ConcreteTypeReference("First_Locals")),
                        new StoreLocal(Addr(1), 0),
                        new LoadArgument(Addr(2), 0),
                        new LoadLocal(Addr(3), 0),
                        new StoreField(Addr(4), 0, 0),
                        new CreateObject(Addr(5), ConcreteTypeReference("Second_Closure")),
                        new CopyStack(Addr(6)),
                        new LoadLocal(Addr(7), 0),
                        new StoreField(Addr(8), 0, 0),
                        new LoadGlobalFunction(Addr(9), FunctionDefinitionReference("Second")),
                        new Call(Addr(10)),
                        Drop(11),
                        LoadUnit(12),
                        Return(13)
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