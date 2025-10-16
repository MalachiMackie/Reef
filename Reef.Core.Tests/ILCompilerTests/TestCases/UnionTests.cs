using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class UnionTests
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }
    
    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid));
    }
    
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "Create union unit variant",
                """
                union MyUnion {
                    A
                }
                var a = MyUnion::A
                """,
                Module(
                    types: [
                        DataType(
                            "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(0),
                                new StoreField(0, "_variantIdentifier"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyUnion"))])
                    ])
            },
            {
                "Create union second unit variant",
                """
                union MyUnion {
                    A,
                    B
                }
                var a = MyUnion::B
                """,
                Module(
                    types: [
                        DataType(
                            "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField]),
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyUnion"))])
                    ])
            },
            {
                "create union tuple variant",
                """
                union MyUnion {
                    A,
                    B(string),
                    C(int, string),
                    
                    pub static fn SomeFn(){}
                }
                
                var a = MyUnion::C(1, "");
                """,
                Module(
                    types: [
                        DataType(
                            "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField, Field("Item0", StringType)]),
                                Variant("C", [VariantIdentifierField, Field("Item0", IntType), Field("Item1", StringType)]),
                            ])
                    ],
                    methods: [
                        Method("MyUnion__SomeFn",
                            [LoadUnit(), Return()]),
                        Method(
                            "MyUnion_Create_B",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(1, "Item0"),
                                Return()
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference("MyUnion")),
                        Method(
                            "MyUnion_Create_C",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(2),
                                new StoreField(2, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(2, "Item0"),
                                new CopyStack(),
                                new LoadArgument(1),
                                new StoreField(2, "Item1"),
                                Return()
                            ],
                            parameters: [IntType, StringType],
                            returnType: ConcreteTypeReference("MyUnion")),
                        Method("_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadStringConstant(""),
                                new LoadFunction(FunctionDefinitionReference("MyUnion_Create_C")),
                                new Call(2),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyUnion"))])
                    ])
            },
            {
                "create union tuple variant with function object",
                """
                union MyUnion {
                    A,
                    B(string),
                }
                
                var a = MyUnion::B;
                var b = a("");
                """,
                Module(
                    types: [
                        DataType("MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField, Field("Item0", StringType)]),
                            ])
                    ],
                    methods: [
                        Method("MyUnion_Create_B",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(1, "Item0"),
                                Return()
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference("MyUnion")),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("Function`2", [StringType, ConcreteTypeReference("MyUnion")])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyUnion_Create_B")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadStringConstant(""),
                                new LoadFunction(FunctionDefinitionReference("Function`2__Call", [StringType, ConcreteTypeReference("MyUnion")])),
                                new Call(2),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`2", [StringType, ConcreteTypeReference("MyUnion")])),
                                Local("b", ConcreteTypeReference("MyUnion"))
                            ])
                    ])
            },
            {
                "union class initializer",
                """
                union MyUnion {
                    A,
                    B { field Field1: int, field Field2: string }
                }
                var a = new MyUnion::B {
                    Field1 = 1,
                    Field2 = ""
                };
                """,
                Module(
                    types: [
                        DataType("MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField, Field("Field1", IntType), Field("Field2", StringType)]),
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "Field1"),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(1, "Field2"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyUnion"))])
                    ])
            }
        };
    }
}