using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;

using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ControlFlow
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ControlFlowIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var module = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }
    
    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid))
            .WithStrictTyping();
    }
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            {
                "Fallout operator with result",
                """
                static fn SomeFn(): result::<int, string> {
                    var a = ok(1)?;
                    return ok(1);
                }
                """,
                Module(
                    methods: [
                        Method("SomeFn",
                            [
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference("result_Create_Ok", [IntType, StringType])),
                                new Call(1),
                                new StoreLocal("Local1"),
                                new LoadLocal("Local1"),
                                new LoadField(0, "_variantIdentifier"),
                                new SwitchInt(new(){{0, "switchInt_0_branch_0"}}, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadLocal("Local1"),
                                new LoadField(1, "Item0"),
                                new LoadFunction(FunctionDefinitionReference("result_Create_Error", [IntType, StringType])),
                                new Call(1),
                                new Return(),
                                // switchInt_0_branch_0
                                new LoadLocal("Local1"),
                                new LoadField(0, "Item0"),
                                new StoreLocal("a"),
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference("result_Create_Ok", [IntType, StringType])),
                                new Call(1),
                                new Return()
                            ],
                            locals: [
                                Local("a", IntType),
                                Local("Local1", ConcreteTypeReference("result", [IntType, StringType]))
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 7),
                                new InstructionLabel("switchInt_0_branch_0", 12),
                            ],
                            returnType: ConcreteTypeReference("result", [IntType, StringType]))
                    ])
            },
            {
                "empty if is last instruction",
                "if (true) {}",
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    },
                                    "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                new Return()
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 3),
                                new InstructionLabel("switchInt_0_branch_0", 4),
                                new InstructionLabel("switchInt_0_after", 4)
                            ])
                    ])
            },
            {
                "populated if is last instruction",
                "if (true) {var a = 1}",
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [Local("a", IntType)],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 3),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 6),
                            ])
                    ])
            },
            {
                "simple if",
                """
                var a;
                if (true) {
                    a = 1;
                }
                a = 2;
                """,
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                new LoadIntConstant(2),
                                new StoreLocal("a"),
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [Local("a", IntType)],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 3),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 6),
                            ])
                    ])
            },
            {
                "if with else",
                """
                var a;
                if (true) {
                    a = 1;
                } else {
                    a = 2;
                }
                """,
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                new LoadIntConstant(2),
                                new StoreLocal("a"),
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [Local("a", IntType)],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 3),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 8),
                            ])
                    ])
            },
            {
                "if else if chain",
                """
                var a;
                if (true) {
                    a = 1;
                } else if (true) {
                    a = 2;
                } else if (true) {
                    a = 3;
                }
                """,
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_1_branch_0"}
                                    }, "switchInt_1_otherwise"),
                                // switchInt_1_otherwise
                                new LoadIntConstant(2),
                                new StoreLocal("a"),
                                new Branch("switchInt_1_after"),
                                // switchInt_1_branch_0
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_2_branch_0"}
                                    }, "switchInt_2_otherwise"),
                                // switchInt_2_otherwise
                                new LoadIntConstant(3),
                                new StoreLocal("a"),
                                new Branch("switchInt_2_after"),
                                // switchInt_2_branch_0
                                // switchInt_2_after
                                // switchInt_1_after
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [Local("a", IntType)],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 3),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 18),
                                new InstructionLabel("switchInt_1_otherwise", 9),
                                new InstructionLabel("switchInt_1_branch_0", 12),
                                new InstructionLabel("switchInt_1_after", 18),
                                new InstructionLabel("switchInt_2_otherwise", 15),
                                new InstructionLabel("switchInt_2_branch_0", 18),
                                new InstructionLabel("switchInt_2_after", 18),
                            ])
                    ])
            },
            {
                "if else if chain with else",
                """
                var a;
                if (true) {
                    a = 1;
                } else if (true) {
                    a = 2;
                } else if (true) {
                    a = 3;
                } else {
                    a = 4;
                }
                """,
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_1_branch_0"}
                                    }, "switchInt_1_otherwise"),
                                // switchInt_1_otherwise
                                new LoadIntConstant(2),
                                new StoreLocal("a"),
                                new Branch("switchInt_1_after"),
                                // switchInt_1_branch_0
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_2_branch_0"}
                                    }, "switchInt_2_otherwise"),
                                // switchInt_2_otherwise
                                new LoadIntConstant(3),
                                new StoreLocal("a"),
                                new Branch("switchInt_2_after"),
                                // switchInt_2_branch_0
                                new LoadIntConstant(4),
                                new StoreLocal("a"),
                                // switchInt_2_after
                                // switchInt_1_after
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [Local("a", IntType)],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 3),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 20),
                                new InstructionLabel("switchInt_1_otherwise", 9),
                                new InstructionLabel("switchInt_1_branch_0", 12),
                                new InstructionLabel("switchInt_1_after", 20),
                                new InstructionLabel("switchInt_2_otherwise", 15),
                                new InstructionLabel("switchInt_2_branch_0", 18),
                                new InstructionLabel("switchInt_2_after", 20),
                            ])
                    ])
            },
            {
                "discard pattern matches",
                """
                if (1 matches _){}
                """,
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("Local0"),
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(new()
                                {
                                    {0, "switchInt_0_branch_0"}
                                }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [Local("Local0", IntType)],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 5),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 6),
                            ]) 
                    ])
            },
            {
                "variable declaration pattern matches",
                """
                if (1 matches var a) {}
                """,
                Module(
                    methods: [
                        Method("_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(new()
                                {
                                    {0, "switchInt_0_branch_0"}
                                }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                new LoadUnitConstant(),
                                Return()
                            ],
                            locals: [
                                Local("a", IntType),
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 5),
                                new InstructionLabel("switchInt_0_branch_0", 6),
                                new InstructionLabel("switchInt_0_after", 6),
                            ]) 
                    ])
            },
            {
                "class pattern matches",
                """
                class MyClass
                {
                    pub field MyField: string
                }
                var a = new MyClass { MyField = "" };
                if (a matches MyClass { MyField }) {}
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "MyField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new StoreLocal("Local2"),
                                new LoadLocal("Local2"),
                                new LoadField(0, "MyField"),
                                new StoreLocal("MyField"),
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    },
                                    "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 13),
                                new InstructionLabel("switchInt_0_branch_0", 14),
                                new InstructionLabel("switchInt_0_after", 14),
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("MyField", StringType),
                                Local("Local2", ConcreteTypeReference("MyClass"))
                            ])
                    ])
            },
            {
                "class pattern matches multiple fields",
                """
                class MyClass
                {
                    pub field MyField: string,
                    pub field SecondField: int,
                }
                var a = new MyClass { MyField = "", SecondField = 2 };
                if (a matches MyClass { MyField, SecondField }) {}
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("MyField", StringType),
                                        Field("SecondField", IntType)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "MyField"),
                                new CopyStack(),
                                new LoadIntConstant(2),
                                new StoreField(0, "SecondField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new StoreLocal("Local3"),
                                new LoadLocal("Local3"),
                                new LoadField(0, "MyField"),
                                new StoreLocal("MyField"),
                                new LoadBoolConstant(true),
                                new BranchIfFalse("boolAnd_0_false"),
                                new LoadLocal("Local3"),
                                new LoadField(0, "SecondField"),
                                new StoreLocal("SecondField"),
                                new LoadBoolConstant(true),
                                new Branch("boolAnd_0_after"),
                                // boolAnd_0_false
                                new LoadBoolConstant(false),
                                // boolAnd_0_after
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    },
                                    "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 23),
                                new InstructionLabel("switchInt_0_branch_0", 24),
                                new InstructionLabel("switchInt_0_after", 24),
                                new InstructionLabel("boolAnd_0_false", 20),
                                new InstructionLabel("boolAnd_0_after", 21),
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("MyField", StringType),
                                Local("SecondField", IntType),
                                Local("Local3", ConcreteTypeReference("MyClass"))
                            ])
                    ])
            },
            {
                "class pattern matches with field pattern",
                """
                class MyClass
                {
                    pub field MyField: string
                }
                var a = new MyClass { MyField = "" };
                if (a matches MyClass { MyField: var b }) {}
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("MyField", StringType),
                                    ])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "MyField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new StoreLocal("Local2"),
                                new LoadLocal("Local2"),
                                new LoadField(0, "MyField"),
                                new StoreLocal("b"),
                                new LoadBoolConstant(true),
                                new CastBoolToInt(),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"}
                                    },
                                    "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                // switchInt_0_after
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 13),
                                new InstructionLabel("switchInt_0_branch_0", 14),
                                new InstructionLabel("switchInt_0_after", 14),
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", StringType),
                                Local("Local2", ConcreteTypeReference("MyClass"))
                            ])
                    ])
            },
            {
                "match expression",
                """
                union MyUnion {
                    A(string),
                    B,
                    C
                }
                var a = MyUnion::B;
                var b = match (a) {
                    MyUnion::A(_) => 1,
                    MyUnion::B => 2,
                    _ => 3
                };
                """,
                Module(
                    types: [
                        DataType("MyUnion",
                            variants: [
                                Variant("A",
                                    [
                                        Field("_variantIdentifier", IntType),
                                        Field("Item0", StringType)
                                    ]),
                                Variant("B",
                                    [
                                        Field("_variantIdentifier", IntType),
                                    ]),
                                Variant("C",
                                [
                                    Field("_variantIdentifier", IntType),
                                ]),
                            ])
                    ],
                    methods: [
                        Method(
                            "MyUnion_Create_A",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(0),
                                new StoreField(0, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "Item0"),
                                Return()
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference("MyUnion")),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new StoreLocal("Local2"),
                                new LoadLocal("Local2"),
                                new LoadField(0, "_variantIdentifier"),
                                new SwitchInt(
                                    new()
                                    {
                                        {0, "switchInt_0_branch_0"},
                                        {1, "switchInt_0_branch_1"},
                                    }, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadIntConstant(3),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_0
                                new LoadLocal("Local2"),
                                new LoadField(0, "Item0"),
                                new StoreLocal("Local3"),
                                new LoadIntConstant(1),
                                new Branch("switchInt_0_after"),
                                // switchInt_0_branch_1
                                new LoadIntConstant(2),
                                // switchInt_0_after
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion")),
                                Local("b", IntType),
                                Local("Local2", ConcreteTypeReference("MyUnion")),
                                Local("Local3", StringType)
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 10),
                                new InstructionLabel("switchInt_0_branch_0", 12),
                                new InstructionLabel("switchInt_0_branch_1", 17),
                                new InstructionLabel("switchInt_0_after", 18),
                            ])
                    ])
            },
        };
    }
}
