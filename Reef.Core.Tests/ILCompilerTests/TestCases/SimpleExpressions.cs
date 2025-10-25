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
            opts => opts.Excluding(x => x.Type == typeof(Stack<IReefTypeReference>)),
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
                                new LoadInt32Constant(1),
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
                "var a: i64",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int64Type)])
                    ])
            },
            {
                "two variable declarations without initializers",
                "var a: i64;var b: string",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int64Type), Local("b", StringType)])
                    ])
            },
            {
                "variable declaration with value initializer",
                "var a = 1",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt32Constant(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", Int32Type)
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
                                new LoadInt32Constant(1),
                                new StoreLocal("a"),
                                new LoadStringConstant("hello"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", Int32Type),
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
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new CompareInt32LessThan(),
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
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new CompareInt32GreaterThan(),
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
                                new LoadInt32Constant(1),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new StoreLocal("b"),
                                new LoadLocal("b"),
                                new StoreLocal("c"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", Int32Type),
                                Local("b", Int32Type),
                                Local("c", Int32Type)
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
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new Int32Plus(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int32Type)])
                    ])
            },
            {
                "minus",
                "var a = 1 - 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new Int32Minus(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int32Type)])
                    ])
            },
            {
                "multiply",
                "var a = 1 * 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new Int32Multiply(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int32Type)])
                    ])
            },
            {
                "divide",
                "var a = 1 / 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new Int32Divide(),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int32Type)])
                    ])
            },
            {
                "not equals",
                "var a = 1 != 2",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new CompareInt32NotEqual(),
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
                                new LoadInt32Constant(1),
                                new LoadInt32Constant(2),
                                new CompareInt32Equal(),
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
                                new LoadInt32Constant(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int32Type)])
                    ])
            },
            {
                "field assignment",
                """
                class MyClass{pub mut field MyField: i64}
                var mut a = new MyClass{MyField = 1};
                a.MyField = 2;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [Field("MyField", Int64Type)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new CopyStack(),
                                new LoadInt64Constant(1),
                                new StoreField(0, "MyField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadInt64Constant(2),
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
                class MyClass{pub static mut field MyField: i64 = 1}
                MyClass::MyField = 2;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("MyField", Int64Type, [new LoadInt64Constant(1)])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt64Constant(2),
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
                                new LoadInt32Constant(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int32Type)])
                    ])
            },
            {
                "tuple with multiple elements",
                "var a = (1, true)",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.Tuple(2), "Tuple`2", [Int32Type, BoolType])),
                                new CopyStack(),
                                new LoadInt32Constant(1),
                                new StoreField(0, "Item0"),
                                new CopyStack(),
                                new LoadBoolConstant(true),
                                new StoreField(0, "Item1"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(DefId.Tuple(2), "Tuple`2", [Int32Type, BoolType]))])
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