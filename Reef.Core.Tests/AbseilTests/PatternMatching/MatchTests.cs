using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests.PatternMatching;

public class MatchTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void MatchAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram, false, false);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string _moduleId = "MatchTests";

    [Fact]
    public void SingleTest()
    {
        var source = """
                union OtherUnion {A, B, C, D}
                union MyUnion {X{field MyField: OtherUnion}, Y}

                var a = MyUnion::Y;
                match(a) {
                    MyUnion::X {MyField: OtherUnion::A} var something => 1,
                    MyUnion::X {MyField: var myField} var somethingElse => 2,
                    var myUnion => 4,
                };
                """;
                var expectedProgram = LoweredProgram(
                    types: [
                        DataType(_moduleId, "OtherUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("D", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "X",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("MyField", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))
                                    ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "Y",
                                        true,
                                        new(){
                                            {"_variantIdentifier", UInt16Constant(1, true)}
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local5",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "X",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Block(
                                                        [
                                                            VariableDeclaration("Local6",
                                                                FieldAccess(
                                                                    LocalAccess(
                                                                        "Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "MyField",
                                                                    "X",
                                                                    true,
                                                                    ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                false),
                                                            SwitchInt(
                                                                FieldAccess(
                                                                    LocalAccess("Local6", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                    "_variantIdentifier",
                                                                    "A",
                                                                    true,
                                                                    UInt16_t),
                                                                new()
                                                                {
                                                                    {
                                                                        0,
                                                                        Block(
                                                                            [
                                                                                VariableDeclaration(
                                                                                    "something",
                                                                                    LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                                    false),
                                                                                Int64Constant(1, true)
                                                                            ],
                                                                            Int64_t,
                                                                            true)
                                                                    }
                                                                },
                                                                Block(
                                                                    [
                                                                        VariableDeclaration(
                                                                            "myField",
                                                                            LocalAccess("Local6", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                            false),
                                                                        Block(
                                                                            [
                                                                                VariableDeclaration(
                                                                                    "somethingElse",
                                                                                    LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                                    false),
                                                                                Int64Constant(2, true)
                                                                            ],
                                                                            Int64_t,
                                                                            true),
                                                                    ],
                                                                    Int64_t,
                                                                    true),
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        Int64_t,
                                                        true)
                                                }
                                            },
                                            Block(
                                                [
                                                    VariableDeclaration(
                                                        "myUnion",
                                                        LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        false),
                                                    Int64Constant(4, true)
                                                ],
                                                Int64_t,
                                                true),
                                            true,
                                            Int64_t)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("something", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("somethingElse", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("myUnion", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("myField", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                Local("Local5", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local6", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
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
                "match on union variant",
                """
                union MyUnion{A, B, C}
                var a = MyUnion::A;
                match(a) {
                    MyUnion::A => 1,
                    MyUnion::B => 2,
                    MyUnion::C => 3,
                };
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
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
                                        new(){
                                            {"_variantIdentifier", UInt16Constant(0, true)}
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "A",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Int64Constant(1, true)
                                                },
                                                {
                                                    1,
                                                    Int64Constant(2, true)
                                                },
                                                {
                                                    2,
                                                    Int64Constant(3, true)
                                                },
                                            },
                                            Unreachable(),
                                            true,
                                            Int64_t),
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local1", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                            ])
                    ])
            },
            {
                "match on union variant with discard",
                """
                union MyUnion{A, B, C}
                var a = MyUnion::A;
                match(a) {
                    MyUnion::A => 1,
                    _ => 2
                };
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
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
                                        new(){
                                            {"_variantIdentifier", UInt16Constant(0, true)}
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration("Local1", LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))), false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "A",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Int64Constant(1, true)
                                                }
                                            },
                                            Int64Constant(2, true),
                                            true,
                                            Int64_t),
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local1", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                            ])
                    ])
            },
            {
                "match union tuple variant sub patterns",
                """
                union OtherUnion {A, B, C}
                union MyUnion {X(OtherUnion), Y}

                var a = MyUnion::Y;
                match(a) {
                    MyUnion::X(OtherUnion::A) => 1,
                    MyUnion::X(OtherUnion::B) => 2,
                    MyUnion::X(OtherUnion::C) => 3,
                    MyUnion::Y => 4,
                };
                """,
                LoweredProgram(
                    types:
                    [
                        DataType(_moduleId, "OtherUnion",
                            variants:
                            [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyUnion",
                            variants:
                            [
                                Variant(
                                    "X",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("Item0", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))
                                    ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                    ],
                    methods:
                    [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "X",
                                        true,
                                        new()
                                        {
                                            {
                                                "Item0", LoadArgument(0, true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))
                                            },
                                            {
                                                "_variantIdentifier",
                                                UInt16Constant(0, true)
                                            }
                                        }))
                            ],
                            parameters: [ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "Y",
                                        true,
                                        new()
                                        {
                                            { "_variantIdentifier", UInt16Constant(1, true) }
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "X",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local2",
                                                                FieldAccess(
                                                                    LocalAccess(
                                                                        "Local1",
                                                                        true,
                                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "Item0",
                                                                    "X",
                                                                    true,
                                                                    ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                false),
                                                            SwitchInt(
                                                                FieldAccess(
                                                                    LocalAccess("Local2", true,
                                                                        ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                    "_variantIdentifier",
                                                                    "A",
                                                                    true,
                                                                    UInt16_t),
                                                                new()
                                                                {
                                                                    {
                                                                        0,
                                                                        Int64Constant(1, true)
                                                                    },
                                                                    {
                                                                        1,
                                                                        Int64Constant(2, true)
                                                                    },
                                                                    {
                                                                        2,
                                                                        Int64Constant(3, true)
                                                                    },
                                                                },
                                                                Unreachable(),
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        Int64_t,
                                                        true)
                                                },
                                                {
                                                    1,
                                                    Int64Constant(4, true)
                                                }
                                            },
                                            Unreachable(),
                                            true,
                                            Int64_t)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local1", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local2", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                            ])
                    ])
            },
            {
                "match union tuple variant sub patterns and variant pattern",
                """
                union OtherUnion{A, B}
                union MyUnion {X(OtherUnion), Y}
                
                var a = MyUnion::Y;
                match (a) {
                    MyUnion::X(OtherUnion::A) => 1,
                    MyUnion::X => 2,
                    MyUnion::Y => 3
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "OtherUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "X",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("Item0", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                    ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16_t)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "X",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                            {"Item0", LoadArgument(0, true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))}
                                        }))
                            ],
                            parameters: [ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "Y",
                                        true,
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "X",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local2",
                                                                FieldAccess(
                                                                    LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "Item0",
                                                                    "X",
                                                                    true,
                                                                    ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                false),
                                                            SwitchInt(
                                                                FieldAccess(
                                                                    LocalAccess("Local2", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                    "_variantIdentifier",
                                                                    "A",
                                                                    true,
                                                                    UInt16_t),
                                                                new()
                                                                {
                                                                    {0, Int64Constant(1, true)}
                                                                },
                                                                Int64Constant(2, true),
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        Int64_t,
                                                        true)
                                                },
                                                { 1, Int64Constant(3, true) }
                                            },
                                            Unreachable(),
                                            true,
                                            Int64_t)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local1", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local2", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                            ])
                    ])
            },
            {
                "match union tuple variant sub patterns and discard",
                """
                union OtherUnion {A, B, C, D}
                union MyUnion {X(OtherUnion), Y}

                var a = MyUnion::Y;
                match(a) {
                    MyUnion::X(OtherUnion::A) => 1,
                    MyUnion::X(OtherUnion::B) => 2,
                    MyUnion::X(OtherUnion::C) => 3,
                    _ => 4,
                };
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "OtherUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("D", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "X",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("Item0", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))
                                    ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "X",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                            {"Item0", LoadArgument(0, true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))}
                                        }))
                            ],
                            parameters: [ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "Y",
                                        true,
                                        new(){
                                            {"_variantIdentifier", UInt16Constant(1, true)}
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "X",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local2",
                                                                FieldAccess(
                                                                    LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "Item0",
                                                                    "X",
                                                                    true,
                                                                    ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                false),
                                                            SwitchInt(
                                                                FieldAccess(
                                                                    LocalAccess("Local2", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                    "_variantIdentifier",
                                                                    "A",
                                                                    true,
                                                                    UInt16_t),
                                                                new()
                                                                {
                                                                    { 0, Int64Constant(1, true) },
                                                                    { 1, Int64Constant(2, true) },
                                                                    { 2, Int64Constant(3, true) },
                                                                },
                                                                Int64Constant(4, true),
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        Int64_t,
                                                        true)
                                                }
                                            },
                                            Int64Constant(4, true),
                                            true,
                                            Int64_t)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local1", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local2", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))
                            ])
                    ])
            },
            {
                "match tuple variant with multiple sub patterns",
                """
                union OtherUnion {A, B}
                union MyUnion {X(OtherUnion, OtherUnion), Y}
                var a = MyUnion::Y;
                match (a) {
                    MyUnion::Y => 0,
                    MyUnion::X(OtherUnion::A, OtherUnion::A) => 1,
                    MyUnion::X(OtherUnion::A, OtherUnion::B) => 2,
                    MyUnion::X(OtherUnion::B, OtherUnion::A) => 3,
                    MyUnion::X(OtherUnion::B, OtherUnion::B) => 4,
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "OtherUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "X",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("Item0", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                        Field("Item1", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                    ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16_t)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__X"), "MyUnion__Create__X",
                            [
                                MethodReturn(
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "X",
                                        true,
                                        new()
                                        {
                                            {"Item0", LoadArgument(0, true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))},
                                            {"Item1", LoadArgument(1, true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))},
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                        }))
                            ],
                            parameters: [ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")), ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a", 
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "Y",
                                        true,
                                        new(){{"_variantIdentifier", UInt16Constant(1, true)}}),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "X",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    1,
                                                    Int64Constant(0, true)
                                                },
                                                {
                                                    0,
                                                    Block(
                                                        [
                                                            VariableDeclaration(
                                                                "Local4",
                                                                FieldAccess(
                                                                    LocalAccess(
                                                                        "Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "Item0",
                                                                    "X",
                                                                    true,
                                                                    ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                false),
                                                            SwitchInt(
                                                                FieldAccess(
                                                                    LocalAccess("Local4", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                    "_variantIdentifier",
                                                                    "A",
                                                                    true,
                                                                    UInt16_t),
                                                                new()
                                                                {
                                                                    {
                                                                        0,
                                                                        Block(
                                                                            [
                                                                                VariableDeclaration(
                                                                                    "Local2",
                                                                                    FieldAccess(
                                                                                        LocalAccess(
                                                                                            "Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                                        "Item1",
                                                                                        "X",
                                                                                        true,
                                                                                        ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                                    false),
                                                                                SwitchInt(
                                                                                    FieldAccess(
                                                                                        LocalAccess("Local2", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                                        "_variantIdentifier",
                                                                                        "A",
                                                                                        true,
                                                                                        UInt16_t),
                                                                                    new()
                                                                                    {
                                                                                        {
                                                                                            0,
                                                                                            Int64Constant(1, true)
                                                                                        },
                                                                                        {
                                                                                            1,
                                                                                            Int64Constant(2, true)
                                                                                        }
                                                                                    },
                                                                                    Unreachable(),
                                                                                    true,
                                                                                    Int64_t)
                                                                            ],
                                                                            Int64_t,
                                                                            true)
                                                                    },
                                                                    {
                                                                        1,
                                                                        Block(
                                                                            [
                                                                                VariableDeclaration(
                                                                                    "Local3",
                                                                                    FieldAccess(
                                                                                        LocalAccess(
                                                                                            "Local1", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                                        "Item1",
                                                                                        "X",
                                                                                        true,
                                                                                        ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                                    false),
                                                                                SwitchInt(
                                                                                    FieldAccess(
                                                                                        LocalAccess("Local3", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                                        "_variantIdentifier",
                                                                                        "A",
                                                                                        true,
                                                                                        UInt16_t),
                                                                                    new()
                                                                                    {
                                                                                        {
                                                                                            0,
                                                                                            Int64Constant(3, true)
                                                                                        },
                                                                                        {
                                                                                            1,
                                                                                            Int64Constant(4, true)
                                                                                        }
                                                                                    },
                                                                                    Unreachable(),
                                                                                    true,
                                                                                    Int64_t)
                                                                            ],
                                                                            Int64_t,
                                                                            true)
                                                                    }
                                                                },
                                                                Unreachable(),
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        Int64_t,
                                                        true)
                                                }
                                            },
                                            Unreachable(),
                                            true,
                                            Int64_t)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local1", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local2", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                Local("Local3", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                Local("Local4", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                            ])
                    ])
            },
            {
                "match union class variant sub patterns and discard",
                """
                union OtherUnion {A, B, C, D}
                union MyUnion {X{field MyField: OtherUnion}, Y}

                var a = MyUnion::Y;
                match(a) {
                    MyUnion::X {MyField: OtherUnion::A} var something => 1,
                    MyUnion::X {MyField: var myField} var somethingElse => 2,
                    var myUnion => 4,
                };
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "OtherUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("D", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "X",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("MyField", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion")))
                                    ]),
                                Variant("Y", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "Y",
                                        true,
                                        new(){
                                            {"_variantIdentifier", UInt16Constant(1, true)}
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local5",
                                            LocalAccess("a", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                            false),
                                        SwitchInt(
                                            FieldAccess(
                                                LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                "_variantIdentifier",
                                                "X",
                                                true,
                                                UInt16_t),
                                            new()
                                            {
                                                {
                                                    0,
                                                    Block(
                                                        [
                                                            VariableDeclaration("Local6",
                                                                FieldAccess(
                                                                    LocalAccess(
                                                                        "Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                    "MyField",
                                                                    "X",
                                                                    true,
                                                                    ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                false),
                                                            SwitchInt(
                                                                FieldAccess(
                                                                    LocalAccess("Local6", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                    "_variantIdentifier",
                                                                    "A",
                                                                    true,
                                                                    UInt16_t),
                                                                new()
                                                                {
                                                                    {
                                                                        0,
                                                                        Block(
                                                                            [
                                                                                VariableDeclaration(
                                                                                    "something",
                                                                                    LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                                    false),
                                                                                Int64Constant(1, true)
                                                                            ],
                                                                            Int64_t,
                                                                            true)
                                                                    }
                                                                },
                                                                Block(
                                                                    [
                                                                        VariableDeclaration(
                                                                            "myField",
                                                                            LocalAccess("Local6", true, ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                                                            false),
                                                                        Block(
                                                                            [
                                                                                VariableDeclaration(
                                                                                    "somethingElse",
                                                                                    LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                                    false),
                                                                                Int64Constant(2, true)
                                                                            ],
                                                                            Int64_t,
                                                                            true),
                                                                    ],
                                                                    Int64_t,
                                                                    true),
                                                                true,
                                                                Int64_t)
                                                        ],
                                                        Int64_t,
                                                        true)
                                                }
                                            },
                                            Block(
                                                [
                                                    VariableDeclaration(
                                                        "myUnion",
                                                        LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        false),
                                                    Int64Constant(4, true)
                                                ],
                                                Int64_t,
                                                true),
                                            true,
                                            Int64_t)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("something", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("somethingElse", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("myUnion", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("myField", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                                Local("Local5", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local6", ConcreteTypeReference("OtherUnion", new DefId(_moduleId, $"{_moduleId}.OtherUnion"))),
                            ])
                    ])
            },
            {
                "match type pattern",
                """
                match (1) {
                    i64 => 1
                }
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                Block(
                                    [
                                        VariableDeclaration("Local0", Int64Constant(1, true), false),
                                        Int64Constant(1, true)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("Local0", Int64_t)
                            ])
                    ])
            },
            {
                "match class pattern",
                """
                union MyUnion{A, B}
                class MyClass{pub field MyField: MyUnion}

                var a = new MyClass{MyField = MyUnion::A};
                match (a) {
                    MyClass{MyField: MyUnion::A} => 1,
                    MyClass{MyField: MyUnion::B} var something => 2,
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
                            ]),
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration("a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "MyField",
                                                CreateObject(
                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                                    "A",
                                                    true,
                                                    new()
                                                    {
                                                        {"_variantIdentifier", UInt16Constant(0, true)}
                                                    })
                                            }
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration("Local2",
                                            LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                            false),
                                        Block(
                                            [
                                                VariableDeclaration(
                                                    "Local3",
                                                    FieldAccess(
                                                        LocalAccess("Local2", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                        "MyField",
                                                        "_classVariant",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    false),
                                                SwitchInt(
                                                    FieldAccess(
                                                        LocalAccess("Local3", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    new()
                                                    {
                                                        {
                                                            0,
                                                            Int64Constant(1, true)
                                                        },
                                                        {
                                                            1,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "something",
                                                                        LocalAccess("Local2", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                        false),
                                                                    Int64Constant(2, true)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        }
                                                    },
                                                    Unreachable(),
                                                    true,
                                                    Int64_t)
                                            ],
                                            Int64_t,
                                            true),
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("something", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local2", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local3", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                            ])
                    ])
            },
            {
                "match partial class pattern with discard",
                """
                union MyUnion{A, B}
                class MyClass{pub field MyField: MyUnion}

                var a = new MyClass{MyField = MyUnion::A};
                match (a) {
                    MyClass{MyField: MyUnion::A} => 1,
                    _ => 2
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)])
                            ]),
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration("a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "MyField",
                                                CreateObject(
                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                                    "A",
                                                    true,
                                                    new()
                                                    {
                                                        {"_variantIdentifier", UInt16Constant(0, true)}
                                                    })
                                            }
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                            false),
                                        Block(
                                            [
                                                VariableDeclaration(
                                                    "Local2",
                                                    FieldAccess(
                                                        LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                        "MyField",
                                                        "_classVariant",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    false),
                                                SwitchInt(
                                                    FieldAccess(
                                                        LocalAccess("Local2", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    new()
                                                    {
                                                        {
                                                            0,
                                                            Int64Constant(1, true)
                                                        }
                                                    },
                                                    Int64Constant(2, true),
                                                    true,
                                                    Int64_t)
                                            ],
                                            Int64_t,
                                            true)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local1", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                            ])
                    ])
            },
            {
                "match partial class pattern with discard 2",
                """
                union MyUnion{A, B, C}
                class MyClass{pub field MyField: MyUnion, pub field SecondField: MyUnion}

                var a = new MyClass {
                    MyField = MyUnion::A,
                    SecondField = MyUnion::B,
                };
                match (a) {
                    MyClass { MyField: MyUnion::A, SecondField: MyUnion::A } => 1,
                    MyClass { MyField: MyUnion::B, SecondField: MyUnion::A } => 2,
                    MyClass { MyField: MyUnion::C, SecondField: MyUnion::A } => 4,
                    _ => 3
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)])
                            ]),
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field("MyField", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                        Field("SecondField", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration("a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "MyField",
                                                CreateObject(
                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                                    "A",
                                                    true,
                                                    new()
                                                    {
                                                        {"_variantIdentifier", UInt16Constant(0, true)}
                                                    })
                                            },
                                            {
                                                "SecondField",
                                                CreateObject(
                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                                    "B",
                                                    true,
                                                    new()
                                                    {
                                                        {"_variantIdentifier", UInt16Constant(1, true)}
                                                    })
                                            }
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration(
                                            "Local1",
                                            LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                            false),
                                        Block(
                                            [
                                                VariableDeclaration(
                                                    "Local5",
                                                    FieldAccess(
                                                        LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                        "MyField",
                                                        "_classVariant",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    false),
                                                SwitchInt(
                                                    FieldAccess(
                                                        LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    new()
                                                    {
                                                        {
                                                            0,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "Local2",
                                                                        FieldAccess(
                                                                            LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                            "SecondField",
                                                                            "_classVariant",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        false),
                                                                    SwitchInt(
                                                                        FieldAccess(
                                                                            LocalAccess("Local2", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                            "_variantIdentifier",
                                                                            "A",
                                                                            true,
                                                                            UInt16_t),
                                                                        new()
                                                                        {
                                                                            {
                                                                                0,
                                                                                Int64Constant(1, true)
                                                                            }
                                                                        },
                                                                        Int64Constant(3, true),
                                                                        true,
                                                                        Int64_t)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        },
                                                        {
                                                            1,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "Local3",
                                                                        FieldAccess(
                                                                            LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                            "SecondField",
                                                                            "_classVariant",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        false),
                                                                    SwitchInt(
                                                                        FieldAccess(
                                                                            LocalAccess("Local3", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                            "_variantIdentifier",
                                                                            "A",
                                                                            true,
                                                                            UInt16_t),
                                                                        new()
                                                                        {
                                                                            {
                                                                                0,
                                                                                Int64Constant(2, true)
                                                                            }
                                                                        },
                                                                        Int64Constant(3, true),
                                                                        true,
                                                                        Int64_t)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        },
                                                        {
                                                            2,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "Local4",
                                                                        FieldAccess(
                                                                            LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                            "SecondField",
                                                                            "_classVariant",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        false),
                                                                    SwitchInt(
                                                                        FieldAccess(
                                                                            LocalAccess("Local4", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                            "_variantIdentifier",
                                                                            "A",
                                                                            true,
                                                                            UInt16_t),
                                                                        new()
                                                                        {
                                                                            {
                                                                                0,
                                                                                Int64Constant(4, true)
                                                                            }
                                                                        },
                                                                        Int64Constant(3, true),
                                                                        true,
                                                                        Int64_t)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        }
                                                    },
                                                    Int64Constant(3, true),
                                                    true,
                                                    Int64_t)
                                            ],
                                            Int64_t,
                                            true)
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local1", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local3", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local4", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local5", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                            ])
                    ])
            },
            {
                "Mixture of class and union patterns",
                """
                union MyUnion{A, B, C}
                class MyClass{pub field MyField: MyUnion, pub field SecondField: MyUnion}

                var a = new MyClass {
                    MyField = MyUnion::A,
                    SecondField = MyUnion::B,
                };
                match (a) {
                    MyClass { MyField: MyUnion::A, SecondField: MyUnion::A } => 1,
                    MyClass { MyField: MyUnion::A, SecondField: _          } => 2,
                    MyClass { MyField: MyUnion::B, SecondField: MyUnion::B } => 3,
                    MyClass { MyField: MyUnion::C, SecondField: MyUnion::A } => 4,
                    _ => 5
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                                Variant("C", [Field("_variantIdentifier", UInt16_t)]),
                            ]),
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field("MyField", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                        Field("SecondField", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
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
                                            {
                                                "MyField",
                                                CreateObject(
                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                                    "A",
                                                    true,
                                                    new(){{"_variantIdentifier", UInt16Constant(0, true)}})
                                            },
                                            {
                                                "SecondField",
                                                CreateObject(
                                                    ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                                    "B",
                                                    true,
                                                    new(){{"_variantIdentifier", UInt16Constant(1, true)}})
                                            },
                                        }),
                                    false),
                                Block(
                                    [
                                        VariableDeclaration("Local1", LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))), false),
                                        Block(
                                            [
                                                VariableDeclaration("Local5", 
                                                    FieldAccess(
                                                        LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                        "MyField",
                                                        "_classVariant",
                                                        true,
                                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                    false),
                                                SwitchInt(
                                                    FieldAccess(
                                                        LocalAccess("Local5", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                        "_variantIdentifier",
                                                        "A",
                                                        true,
                                                        UInt16_t),
                                                    new()
                                                    {
                                                        {
                                                            0,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "Local2",
                                                                        FieldAccess(LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                            "SecondField",
                                                                            "_classVariant",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        false),
                                                                    SwitchInt(
                                                                        FieldAccess(
                                                                            LocalAccess("Local2", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                            "_variantIdentifier",
                                                                            "A",
                                                                            true,
                                                                            UInt16_t),
                                                                        new()
                                                                        {
                                                                            {
                                                                                0,
                                                                                Int64Constant(1, true)
                                                                            }
                                                                        },
                                                                        Int64Constant(2, true),
                                                                        true,
                                                                        Int64_t)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        },
                                                        {
                                                            1,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "Local3",
                                                                        FieldAccess(LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                            "SecondField",
                                                                            "_classVariant",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        false),
                                                                    SwitchInt(
                                                                        FieldAccess(
                                                                            LocalAccess("Local3", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                            "_variantIdentifier",
                                                                            "A",
                                                                            true,
                                                                            UInt16_t),
                                                                        new()
                                                                        {
                                                                            {
                                                                                1,
                                                                                Int64Constant(3, true)
                                                                            }
                                                                        },
                                                                        Int64Constant(5, true),
                                                                        true,
                                                                        Int64_t)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        },
                                                        {
                                                            2,
                                                            Block(
                                                                [
                                                                    VariableDeclaration(
                                                                        "Local4",
                                                                        FieldAccess(LocalAccess("Local1", true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                                                            "SecondField",
                                                                            "_classVariant",
                                                                            true,
                                                                            ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                        false),
                                                                    SwitchInt(
                                                                        FieldAccess(
                                                                            LocalAccess("Local4", true, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                                                            "_variantIdentifier",
                                                                            "A",
                                                                            true,
                                                                            UInt16_t),
                                                                        new()
                                                                        {
                                                                            {
                                                                                0,
                                                                                Int64Constant(4, true)
                                                                            }
                                                                        },
                                                                        Int64Constant(5, true),
                                                                        true,
                                                                        Int64_t)
                                                                ],
                                                                Int64_t,
                                                                true)
                                                        },
                                                    },
                                                    Int64Constant(5, true),
                                                    true,
                                                    Int64_t)
                                            ],
                                            Int64_t,
                                            true),
                                        
                                    ],
                                    Int64_t,
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("Local1", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                // MyField 
                                Local("Local5", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                // SecondField
                                Local("Local2", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local3", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                Local("Local4", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                            ])
                    ])
            }
        };
    }
}
