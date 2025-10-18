using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests.PatternMatching;

public class MatchesTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchesAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string _moduleId = "MatchesTests";

    [Fact]
    public void SingleTest()
    {
        var source = """
                union MyUnion{A(int), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_);
                """;
                var expectedProgram = LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", Int), Field("Item0", Int)]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new() {
                                            {"Item0", LoadArgument(0, true, Int)},
                                            {"_variantIdentifier", IntConstant(0, true)},
                                        }))
                            ],
                            parameters: [Int],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            BoolAnd(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local2",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                Block(
                                                    [
                                                        VariableDeclaration(
                                                            "Local3",
                                                            FieldAccess(
                                                                LocalAccess("Local2",
                                                                    true,
                                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                "Item0",
                                                                "A",
                                                                true,
                                                                Int),
                                                            false),
                                                        BoolConstant(true, true)
                                                    ],
                                                    BooleanType,
                                                    true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local3", Int)
                            ])
                    ]);

        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(
                expectedProgram,
                loweredProgram,
                false,
                false);
        
        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "matches - discard pattern",
                """
                var b = 1 matches _;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration("Local1", IntConstant(1, true) ,false),
                                            BoolConstant(true, true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("b", BooleanType),
                                Local("Local1", Int)
                            ])
                    ])
            },
            {
                "matches - variable declaration pattern",
                """
                var b = 1 matches var a;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration("a",
                                                IntConstant(1, true),
                                                false),
                                            BoolConstant(true, true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("b", BooleanType),
                                Local("a", Int)
                            ])
                    ])
            },
            {
                "matches - type pattern",
                """
                var b = 1 matches int;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration("Local1", IntConstant(1, true) ,false),
                                            // TODO: for now, type patterns always evaluate to true.
                                            // In the future,this will only be true when the operands concrete
                                            // type is known.When the operand is some dynamic dispatch
                                            // interface, we willneed some way of checking the concrete
                                            // type at runtime
                                            BoolConstant(true, true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("b", BooleanType),
                                Local("Local1", Int)
                            ])
                    ])
            },
            {
                "matches - type pattern with variable declaration",
                """
                var b = 1 matches int var a;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration("a", IntConstant(1, true) ,false),
                                            BoolConstant(true, true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("b", BooleanType),
                                Local("a", Int)
                            ])
                    ])
            },
            {
                "matches - union variant pattern",
                """
                union MyUnion{A, B};
                var a = MyUnion::A;
                var b = a matches MyUnion::B;
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", Int)]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(0, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            IntEquals(
                                                FieldAccess(
                                                    LocalAccess(
                                                        "Local2",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    "_variantIdentifier",
                                                    "B",
                                                    true,
                                                    Int),
                                                IntConstant(1, true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit(),
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                            ])
                    ])
            },
            {
                "matches - union variant pattern with variable declaration",
                """
                union MyUnion{A, B};
                var a = MyUnion::A;
                var b = a matches MyUnion::B var c;
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", Int)]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(0, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "c",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            IntEquals(
                                                FieldAccess(
                                                    LocalAccess(
                                                        "c",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    "_variantIdentifier",
                                                    "B",
                                                    true,
                                                    Int),
                                                IntConstant(1, true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit(),
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("c", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                            ])
                    ])
            },
            {
                "matches - union tuple pattern",
                """
                union MyUnion{A(int), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_);
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", Int), Field("Item0", Int)]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(0, true)}, {"Item0", LoadArgument(0, true, Int)}}))
                            ],
                            parameters: [Int],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            BoolAnd(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local2",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                Block(
                                                    [
                                                        VariableDeclaration(
                                                            "Local3",
                                                            FieldAccess(
                                                                LocalAccess("Local2",
                                                                    true,
                                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                "Item0",
                                                                "A",
                                                                true,
                                                                Int),
                                                            false),
                                                        BoolConstant(true, true)
                                                    ],
                                                    BooleanType,
                                                    true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local3", Int)
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with variable declaration",
                """
                union MyUnion{A(int), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_) var c;
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", Int), Field("Item0", Int)]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(0, true)}, {"Item0", LoadArgument(0, true, Int)}}))
                            ],
                            parameters: [Int],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "c",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            BoolAnd(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "c",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                Block(
                                                    [
                                                        VariableDeclaration(
                                                            "Local3",
                                                            FieldAccess(
                                                                LocalAccess("c",
                                                                    true,
                                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                "Item0",
                                                                "A",
                                                                true,
                                                                Int),
                                                            false),
                                                        BoolConstant(true, true)
                                                    ],
                                                    BooleanType,
                                                    true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("c", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local3", Int)
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with multiple members ",
                """
                union MyUnion{A(int, string, bool), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_, var c, _);
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", Int),
                                        Field("Item0", Int),
                                        Field("Item1", StringType),
                                        Field("Item2", BooleanType),
                                    ]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new() {
                                            {"Item0", LoadArgument(0, true, Int)},
                                            {"Item1", LoadArgument(1, true, StringType)},
                                            {"Item2", LoadArgument(2, true, BooleanType)},
                                            {"_variantIdentifier", IntConstant(0, true)},
                                        }))
                            ],
                            parameters: [Int, StringType, BooleanType],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local3",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            BoolAnd(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local3",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                BoolAnd(
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local4",
                                                                FieldAccess(
                                                                    LocalAccess("Local3",
                                                                        true,
                                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "Item0",
                                                                    "A",
                                                                    true,
                                                                    Int),
                                                                false),
                                                            BoolConstant(true, true)
                                                        ],
                                                        BooleanType,
                                                        true),
                                                    BoolAnd(
                                                        Block(
                                                            [
                                                                VariableDeclaration(
                                                                    "c",
                                                                    FieldAccess(
                                                                        LocalAccess("Local3",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        "Item1",
                                                                        "A",
                                                                        true,
                                                                        StringType),
                                                                    false),
                                                                BoolConstant(true, true)
                                                            ],
                                                            BooleanType,
                                                            true),
                                                        Block(
                                                            [
                                                                VariableDeclaration(
                                                                    "Local5",
                                                                    FieldAccess(
                                                                        LocalAccess("Local3",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        "Item2",
                                                                        "A",
                                                                        true,
                                                                        BooleanType),
                                                                    false),
                                                                BoolConstant(true, true)
                                                            ],
                                                            BooleanType,
                                                            true),
                                                        true),
                                                    true
                                                ),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("c", StringType),
                                Local("Local3", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local4", Int),
                                Local("Local5", BooleanType),
                            ])
                    ])
            },
            {
                "matches - union class variant pattern",
                """
                union MyUnion{
                    A {field FieldA: int},
                    B
                }
                var a = MyUnion::B;
                var b = a matches MyUnion::A{FieldA: _};
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", Int),
                                        Field("FieldA", Int)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            BoolAnd(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local2",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                Block(
                                                    [
                                                        VariableDeclaration(
                                                            "Local3",
                                                            FieldAccess(
                                                                LocalAccess("Local2",
                                                                    true,
                                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                "FieldA",
                                                                "A",
                                                                true,
                                                                Int),
                                                            false),
                                                        BoolConstant(true, true)
                                                    ],
                                                    BooleanType,
                                                    true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local3", Int)
                            ])
                    ])
            },
            {
                "matches - union class variant pattern with discarded fields",
                """
                union MyUnion{
                    A {field FieldA: int},
                    B
                }
                var a = MyUnion::B;
                var b = a matches MyUnion::A{_};
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", Int),
                                        Field("FieldA", Int)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            IntEquals(
                                                FieldAccess(
                                                    LocalAccess(
                                                        "Local2",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    "_variantIdentifier",
                                                    "A",
                                                    true,
                                                    Int),
                                                IntConstant(0, true),
                                                true),
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                            ])
                    ])
            },
            {
                "matches - union class variant pattern multiple fields",
                """
                union MyUnion{
                    A {field FieldA: int, field FieldB: string, field FieldC: bool},
                    B
                }
                var a = MyUnion::B;
                var b = a matches MyUnion::A{FieldA: var c, FieldB, FieldC: _};
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", Int),
                                        Field("FieldA", Int),
                                        Field("FieldB", StringType),
                                        Field("FieldC", BooleanType)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", Int)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", IntConstant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local4",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            BoolAnd(
                                                IntEquals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local4",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        Int),
                                                    IntConstant(0, true),
                                                    true),
                                                BoolAnd(
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "c",
                                                                FieldAccess(
                                                                    LocalAccess("Local4",
                                                                        true,
                                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "FieldA",
                                                                    "A",
                                                                    true,
                                                                    Int),
                                                                false),
                                                            BoolConstant(true, true)
                                                        ],
                                                        BooleanType,
                                                        true),
                                                    BoolAnd(
                                                        Block(
                                                            [
                                                                VariableDeclaration(
                                                                    "FieldB",
                                                                    FieldAccess(
                                                                        LocalAccess("Local4",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        "FieldB",
                                                                        "A",
                                                                        true,
                                                                        StringType),
                                                                    false),
                                                                BoolConstant(true, true)
                                                            ],
                                                            BooleanType,
                                                            true),
                                                        Block(
                                                            [
                                                                VariableDeclaration(
                                                                    "Local5",
                                                                    FieldAccess(
                                                                        LocalAccess("Local4",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        "FieldC",
                                                                        "A",
                                                                        true,
                                                                        BooleanType),
                                                                    false),
                                                                BoolConstant(true, true)
                                                            ],
                                                            BooleanType,
                                                            true),
                                                        true),
                                                    true
                                                ),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("b", BooleanType),
                                Local("c", Int),
                                Local("FieldB", StringType),
                                Local("Local4", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local5", BooleanType)
                            ])
                    ])
            },
            {
                "matches - class pattern",
                """
                class MyClass { pub field Field0: int, pub field Field1: int, pub field Field2: bool }
                var a = new MyClass{Field0 = 0, Field1 = 1, Field2 = true };
                var b = a matches MyClass {Field0: var c, Field1: _, Field2: _};
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("Field0", Int),
                                        Field("Field1", Int),
                                        Field("Field2", BooleanType)
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {"Field0", IntConstant(0, true)},
                                            {"Field1", IntConstant(1, true)},
                                            {"Field2", BoolConstant(true, true)},
                                        }),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local3",
                                                LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                false),
                                            BoolAnd(
                                                Block(
                                                    [
                                                        VariableDeclaration("c",
                                                            FieldAccess(
                                                                LocalAccess(
                                                                    "Local3",
                                                                    true,
                                                                    ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                "Field0",
                                                                "_classVariant",
                                                                true,
                                                                Int),
                                                            false),
                                                        BoolConstant(true, true)
                                                    ],
                                                    BooleanType,
                                                    true),
                                                BoolAnd(
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local4",
                                                                FieldAccess(
                                                                    LocalAccess(
                                                                        "Local3",
                                                                        true,
                                                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                    "Field1",
                                                                    "_classVariant",
                                                                    true,
                                                                    Int),
                                                                false),
                                                            BoolConstant(true, true)
                                                        ],
                                                        BooleanType,
                                                        true),
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local5",
                                                                FieldAccess(
                                                                    LocalAccess(
                                                                        "Local3",
                                                                        true,
                                                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                    "Field2",
                                                                    "_classVariant",
                                                                    true,
                                                                    BooleanType),
                                                                false),
                                                            BoolConstant(true, true)
                                                        ],
                                                        BooleanType,
                                                        true),
                                                    true),
                                                true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("b", BooleanType),
                                Local("c", Int),
                                Local("Local3", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local4", Int),
                                Local("Local5", BooleanType),
                            ])
                    ])
            }
        };
    }
}
