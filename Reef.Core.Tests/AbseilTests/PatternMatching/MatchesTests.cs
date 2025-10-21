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
                union MyUnion{A(i64), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_);
                """;
                var expectedProgram = LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t), Field("Item0", Int64_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                            {"Item0", LoadArgument(0, true, Int64_t)},
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                        }))
                            ],
                            parameters: [Int64_t],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
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
                                                UInt16Equals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local2",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    UInt16Constant(0, true),
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
                                                                Int64_t),
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
                                Local("Local3", Int64_t)
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
                                            VariableDeclaration("Local1", Int64Constant(1, true) ,false),
                                            BoolConstant(true, true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("b", BooleanType),
                                Local("Local1", Int64_t)
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
                                                Int64Constant(1, true),
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
                                Local("a", Int64_t)
                            ])
                    ])
            },
            {
                "matches - type pattern",
                """
                var b = 1 matches i64;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration("Local1", Int64Constant(1, true) ,false),
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
                                Local("Local1", Int64_t)
                            ])
                    ])
            },
            {
                "matches - type pattern with variable declaration",
                """
                var b = 1 matches i64 var a;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration("a", Int64Constant(1, true) ,false),
                                            BoolConstant(true, true)
                                        ],
                                        BooleanType,
                                        true),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("b", BooleanType),
                                Local("a", Int64_t)
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
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(0, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            UInt16Equals(
                                                FieldAccess(
                                                    LocalAccess(
                                                        "Local2",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    "_variantIdentifier",
                                                    "B",
                                                    true,
                                                    UInt16_t),
                                                UInt16Constant(1, true),
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
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(0, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "c",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            UInt16Equals(
                                                FieldAccess(
                                                    LocalAccess(
                                                        "c",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    "_variantIdentifier",
                                                    "B",
                                                    true,
                                                    UInt16_t),
                                                UInt16Constant(1, true),
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
                union MyUnion{A(i64), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_);
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t), Field("Item0", Int64_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(0, true)}, {"Item0", LoadArgument(0, true, Int64_t)}}))
                            ],
                            parameters: [Int64_t],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
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
                                                UInt16Equals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local2",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    UInt16Constant(0, true),
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
                                                                Int64_t),
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
                                Local("Local3", Int64_t)
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with variable declaration",
                """
                union MyUnion{A(i64), B}
                var a = MyUnion::B;
                var b = a matches MyUnion::A(_) var c;
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t), Field("Item0", Int64_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(0, true)}, {"Item0", LoadArgument(0, true, Int64_t)}}))
                            ],
                            parameters: [Int64_t],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
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
                                                UInt16Equals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "c",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    UInt16Constant(0, true),
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
                                                                Int64_t),
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
                                Local("Local3", Int64_t)
                            ])
                    ])
            },
            {
                "matches - union tuple pattern with multiple members ",
                """
                union MyUnion{A(i64, string, bool), B}
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
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("Item0", Int64_t),
                                        Field("Item1", StringType),
                                        Field("Item2", BooleanType),
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                            {"Item0", LoadArgument(0, true, Int64_t)},
                                            {"Item1", LoadArgument(1, true, StringType)},
                                            {"Item2", LoadArgument(2, true, BooleanType)},
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                        }))
                            ],
                            parameters: [Int64_t, StringType, BooleanType],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
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
                                                UInt16Equals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local3",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    UInt16Constant(0, true),
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
                                                                    Int64_t),
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
                                Local("Local4", Int64_t),
                                Local("Local5", BooleanType),
                            ])
                    ])
            },
            {
                "matches - union class variant pattern",
                """
                union MyUnion{
                    A {field FieldA: i64},
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
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("FieldA", Int64_t)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
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
                                                UInt16Equals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local2",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    UInt16Constant(0, true),
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
                                                                Int64_t),
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
                                Local("Local3", Int64_t)
                            ])
                    ])
            },
            {
                "matches - union class variant pattern with discarded fields",
                """
                union MyUnion{
                    A {field FieldA: i64},
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
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("FieldA", Int64_t)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    Block(
                                        [
                                            VariableDeclaration(
                                                "Local2",
                                                LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                false),
                                            UInt16Equals(
                                                FieldAccess(
                                                    LocalAccess(
                                                        "Local2",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    "_variantIdentifier",
                                                    "A",
                                                    true,
                                                    UInt16_t),
                                                UInt16Constant(0, true),
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
                    A {field FieldA: i64, field FieldB: string, field FieldC: bool},
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
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("FieldA", Int64_t),
                                        Field("FieldB", StringType),
                                        Field("FieldC", BooleanType)
                                    ]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
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
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
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
                                                UInt16Equals(
                                                    FieldAccess(
                                                        LocalAccess(
                                                            "Local4",
                                                            true,
                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    UInt16Constant(0, true),
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
                                                                    Int64_t),
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
                                Local("c", Int64_t),
                                Local("FieldB", StringType),
                                Local("Local4", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local5", BooleanType)
                            ])
                    ])
            },
            {
                "matches - class pattern",
                """
                class MyClass { pub field Field0: i64, pub field Field1: i64, pub field Field2: bool }
                var a = new MyClass{Field0 = 0, Field1 = 1, Field2 = true };
                var b = a matches MyClass {Field0: var c, Field1: _, Field2: _};
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("Field0", Int64_t),
                                        Field("Field1", Int64_t),
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
                                            {"Field0", Int64Constant(0, true)},
                                            {"Field1", Int64Constant(1, true)},
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
                                                                Int64_t),
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
                                                                    Int64_t),
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
                                Local("c", Int64_t),
                                Local("Local3", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local4", Int64_t),
                                Local("Local5", BooleanType),
                            ])
                    ])
            }
        };
    }
}
