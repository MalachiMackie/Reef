using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.IL;
using Reef.Core.TypeChecking;

using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ControlFlow
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ControlFlowIL_Should_GenerateCorrectIL(string description, string source, ReefILModule expectedModule)
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

    private const string _moduleId = "ControlFlow";

    [Fact]
    public void SingleTest()
    {
        var source = """
                fn SomeFn(param: int) {
                }
                var a = SomeFn;
                a(1);
                """;
                var expectedModule = Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [LoadUnit(), Return()],
                            parameters: [IntType]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [IntType, UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [IntType, UnitType])),
                                new Call(2, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [IntType, UnitType]))
                            ])
                    ]);
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(_moduleId, tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule);
    }
    
    public static TheoryData<string, string, ReefILModule> TestCases()
    {
        return new TheoryData<string, string, ReefILModule>
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
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference(DefId.Result_Create_Ok, "result__Create__Ok", [IntType, StringType])),
                                new Call(1, 0, true),
                                new StoreLocal("Local1"),
                                new LoadLocal("Local1"),
                                new LoadField(0, "_variantIdentifier"),
                                new SwitchInt(new(){{0, "switchInt_0_branch_0"}}, "switchInt_0_otherwise"),
                                // switchInt_0_otherwise
                                new LoadLocal("Local1"),
                                new LoadField(1, "Item0"),
                                new LoadFunction(FunctionDefinitionReference(DefId.Result_Create_Error, "result__Create__Error", [IntType, StringType])),
                                new Call(1, 0, true),
                                new Return(),
                                // switchInt_0_branch_0
                                new LoadLocal("Local1"),
                                new LoadField(0, "Item0"),
                                new StoreLocal("a"),
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference(DefId.Result_Create_Ok, "result__Create__Ok", [IntType, StringType])),
                                new Call(1, 0, true),
                                new Return()
                            ],
                            locals: [
                                Local("a", IntType),
                                Local("Local1", ConcreteTypeReference(DefId.Result, "result", [IntType, StringType]))
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_otherwise", 7),
                                new InstructionLabel("switchInt_0_branch_0", 12),
                            ],
                            returnType: ConcreteTypeReference(DefId.Result, "result", [IntType, StringType]))
                    ])
            },
            {
                "empty if is last instruction",
                "if (true) {}",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
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
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                Local("MyField", StringType),
                                Local("Local2", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("MyField", StringType),
                                        Field("SecondField", IntType)
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
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
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                Local("MyField", StringType),
                                Local("SecondField", IntType),
                                Local("Local3", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("MyField", StringType),
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
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
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                Local("b", StringType),
                                Local("Local2", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))
                            ])
                    ])
            },
            {
                "match expression",
                """
                union MyUnion {
                    A(string),
                    B,
                }
                var a = MyUnion::B;
                var b = match (a) {
                    MyUnion::A(_) => 1,
                    MyUnion::B => 2,
                };
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
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
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(0),
                                new StoreField(0, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "Item0"),
                                Return()
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
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
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                Local("b", IntType),
                                Local("Local2", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                Local("Local3", StringType)
                            ],
                            labels: [
                                new InstructionLabel("switchInt_0_branch_0", 10),
                                new InstructionLabel("switchInt_0_branch_1", 15),
                                new InstructionLabel("switchInt_0_after", 16),
                            ])
                    ])
            },
        };
    }
}
