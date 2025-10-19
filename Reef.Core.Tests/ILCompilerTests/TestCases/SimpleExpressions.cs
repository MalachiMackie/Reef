using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.IL;
using Reef.Core.TypeChecking;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class SimpleExpressions
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

    private const string _moduleId = "SimpleExpressions";

    public static TheoryData<string, string, ReefILModule> TestCases()
    {
        return new()
        {
            {
                "push int",
                "1",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "push constant string",
                "\"someString\"",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadStringConstant("someString"),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "push constant bool true",
                "true",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(true),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "push constant bool false",
                "false",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(false),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "variable declaration without initializer",
                "var a: int",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "two variable declarations without initializers",
                "var a: int;var b: string",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType), Local("b", StringType)])
                    ])
            },
            {
                "variable declaration with value initializer",
                "var a = 1",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", IntType)
                            ])
                    ])
            },
            {
                "two variable declarations with value initializers",
                "var a = 1;var b = \"hello\"",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new LoadStringConstant("hello"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", IntType),
                                Local("b", StringType)
                            ])
                    ])
            },
            {
                "less than",
                "var a = 1 < 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new CompareIntLessThan(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "greater than",
                "var a = 1 > 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new CompareIntGreaterThan(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "access local variable",
                """
                var a = 1;
                var b = a;
                var c = b;
                """,
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new StoreLocal("b"),
                                new LoadLocal("b"),
                                new StoreLocal("c"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", IntType),
                                Local("b", IntType),
                                Local("c", IntType)
                            ])
                    ])
            },
            {
                "plus",
                "var a = 1 + 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new IntPlus(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "minus",
                "var a = 1 - 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new IntMinus(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "multiply",
                "var a = 1 * 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new IntMultiply(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "divide",
                "var a = 1 / 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new IntDivide(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "not equals",
                "var a = 1 != 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new CompareIntNotEqual(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "equals",
                "var a = 1 == 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadIntConstant(2),
                                new CompareIntEqual(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "local assignment",
                """
                var a;
                a = 1;
                """,
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "field assignment",
                """
                class MyClass{pub mut field MyField: int}
                var mut a = new MyClass{MyField = 1};
                a.MyField = 2;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [Field("MyField", IntType)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(0, "MyField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadIntConstant(2),
                                new StoreField(0, "MyField"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))])
                    ])
            },
            {
                "static field assignment",
                """
                class MyClass{pub static mut field MyField: int = 1}
                MyClass::MyField = 2;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("MyField", IntType, [new LoadIntConstant(1)])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(2),
                                new StoreStaticField(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"), "MyField"),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "single element tuple",
                "var a = (1);",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadIntConstant(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "tuple with multiple elements",
                "var a = (1, true)",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.Tuple(2), "Tuple`2", [IntType, BoolType])),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(0, "Item0"),
                                new CopyStack(),
                                new LoadBoolConstant(true),
                                new StoreField(0, "Item1"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(DefId.Tuple(2), "Tuple`2", [IntType, BoolType]))])
                    ])
            },
            {
                "bool not",
                "var a = !true;",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(true),
                                new BoolNot(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "and",
                "var a = true && false",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(true),
                                new BranchIfFalse("boolAnd_0_false"),
                                new LoadBoolConstant(false),
                                new Branch("boolAnd_0_after"),
                                // boolAnd_0_false
                                new LoadBoolConstant(false),
                                // boolAnd_0_after
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("boolAnd_0_false", 4),
                                new InstructionLabel("boolAnd_0_after", 5),
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "or",
                "var a = true || false",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(true),
                                new BranchIfTrue("boolOr_0_true"),
                                new LoadBoolConstant(false),
                                new Branch("boolOr_0_after"),
                                // boolOr_0_true
                                new LoadBoolConstant(true),
                                // boolOr_0_after
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("boolOr_0_true", 4),
                                new InstructionLabel("boolOr_0_after", 5)
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "double and",
                "var a = true && false && true",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(true),
                                new BranchIfFalse("boolAnd_1_false"),
                                new LoadBoolConstant(false),
                                new Branch("boolAnd_1_after"),
                                // boolAnd_1_false
                                new LoadBoolConstant(false),
                                // boolAnd_1_after
                                new BranchIfFalse("boolAnd_0_false"),
                                new LoadBoolConstant(true),
                                new Branch("boolAnd_0_after"),
                                // boolAnd_0_false
                                new LoadBoolConstant(false),
                                // boolAnd_0_after
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("boolAnd_1_false", 4),
                                new InstructionLabel("boolAnd_1_after", 5),
                                new InstructionLabel("boolAnd_0_false", 8),
                                new InstructionLabel("boolAnd_0_after", 9),
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            },
            {
                "double or",
                "var a = true || false || true",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadBoolConstant(true),
                                new BranchIfTrue("boolOr_1_true"),
                                new LoadBoolConstant(false),
                                new Branch("boolOr_1_after"),
                                // boolOr_1_true
                                new LoadBoolConstant(true),
                                // boolOr_1_after
                                new BranchIfTrue("boolOr_0_true"),
                                new LoadBoolConstant(true),
                                new Branch("boolOr_0_after"),
                                // boolOr_0_true
                                new LoadBoolConstant(true),
                                // boolOr_0_after
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            labels: [
                                new InstructionLabel("boolOr_1_true", 4),
                                new InstructionLabel("boolOr_1_after", 5),
                                new InstructionLabel("boolOr_0_true", 8),
                                new InstructionLabel("boolOr_0_after", 9),
                            ],
                            locals: [Local("a", BoolType)])
                    ])
            }
        };
    }
}