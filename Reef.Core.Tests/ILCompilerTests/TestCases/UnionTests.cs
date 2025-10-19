using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.IL;
using Reef.Core.TypeChecking;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class UnionTests
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefILModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(_moduleId, tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            description);
    }

    private const string _moduleId = "UnionTests";
    
    public static TheoryData<string, string, ReefILModule> TestCases()
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(0),
                                new StoreField(0, "_variantIdentifier"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion"))])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField]),
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion"))])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField, Field("Item0", StringType)]),
                                Variant("C", [VariantIdentifierField, Field("Item0", IntType), Field("Item1", StringType)]),
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                            [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__B"), "MyUnion__Create__B",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(1, "Item0"),
                                Return()
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__C"), "MyUnion__Create__C",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
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
                            returnType: ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadStringConstant(""),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__C"), "MyUnion__Create__C")),
                                new Call(2, 0, true),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion"))])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField, Field("Item0", StringType)]),
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__B"), "MyUnion__Create__B",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(1, "Item0"),
                                Return()
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [StringType, ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__B"), "MyUnion__Create__B")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadStringConstant(""),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [StringType, ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")])),
                                new Call(2, 0, true),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [StringType, ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")])),
                                Local("b", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion"))
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant("A", [VariantIdentifierField]),
                                Variant("B", [VariantIdentifierField, Field("Field1", IntType), Field("Field2", StringType)]),
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
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
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion"))])
                    ])
            }
        };
    }
}