using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ClosureTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClosureAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }
    
    [Fact]
    public void SingleTest()
    {
        var source = """
                class MyClass
                {
                    field MyField: string,

                    fn MyFn()
                    {
                        var a = MyField;
                        fn InnerFn()
                        {
                            var b = MyField;
                        }
                    }
                }
                """;
        var expectedProgram = LoweredProgram(
            types:
            [
                DataType(
                    "MyClass",
                    variants:
                    [
                        Variant("_classVariant",
                            [Field("MyField", StringType)])
                    ]),
                DataType(
                    "MyClass__MyFn__InnerFn__Closure",
                    variants:
                    [
                        Variant("_classVariant", [Field("this", ConcreteTypeReference("MyClass"))])
                    ])
            ],
            methods:
            [
                Method("MyClass__MyFn__InnerFn",
                    [
                        VariableDeclaration(
                            "b",
                            FieldAccess(
                                FieldAccess(
                                    LoadArgument(
                                        0,
                                        true,
                                        ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")),
                                    "this",
                                    "_classVariant",
                                    true,
                                    ConcreteTypeReference("MyClass")),
                                "MyField",
                                "_classVariant",
                                true,
                                StringType),
                            false),
                        MethodReturnUnit()
                    ],
                    locals: [Local("b", StringType)],
                    parameters: [ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")]),
                Method(
                    "MyClass__MyFn",
                    [
                        VariableDeclaration(
                            "a",
                            FieldAccess(
                                LoadArgument(
                                    0, true, ConcreteTypeReference("MyClass")),
                                "MyField",
                                "_classVariant",
                                true,
                                StringType),
                            false),
                        MethodReturnUnit()
                    ],
                    locals: [Local("a", StringType)],
                    parameters: [ConcreteTypeReference("MyClass")])
            ]);
        
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }
    

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "Closure accesses local variable",
                """
                fn MyFn()
                {
                    var a = "";
                    fn InnerFn() {
                        var b = a;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyFn__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType("MyFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant", [Field("MyFn__Locals", ConcreteTypeReference("MyFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(
                            "MyFn",
                            [
                                VariableDeclaration(
                                    "__locals", 
                                    CreateObject(
                                        ConcreteTypeReference("MyFn__Locals"),
                                        "_classVariant",
                                        true),
                                    false),
                                FieldAssignment(
                                    LocalAccess("__locals", true, ConcreteTypeReference("MyFn__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("MyFn__Locals"))
                            ]),
                        Method(
                            "MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(0, true, ConcreteTypeReference("MyFn__InnerFn__Closure")),
                                            "MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyFn__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            parameters: [
                                ConcreteTypeReference("MyFn__InnerFn__Closure")
                            ],
                            locals: [
                                Local("b", StringType)
                            ])
                    ])
            },
            {
                "access local that is referenced in closure",
                """
                var a = "";
                var c = a;
                fn InnerFn() {
                    var b = a;
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType("_Main__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant", [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(
                            "_Main",
                            [
                                VariableDeclaration(
                                    "__locals", 
                                    CreateObject(
                                        ConcreteTypeReference("_Main__Locals"),
                                        "_classVariant",
                                        true),
                                    false),
                                FieldAssignment(
                                    LocalAccess("__locals", true, ConcreteTypeReference("_Main__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("", true),
                                    false,
                                    StringType),
                                VariableDeclaration(
                                    "c",
                                    FieldAccess(
                                        LocalAccess(
                                            "__locals",
                                            true,
                                            ConcreteTypeReference("_Main__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("_Main__Locals")),
                                Local("c", StringType),
                            ]),
                        Method(
                            "_Main__InnerFn",
                            [
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(0, true, ConcreteTypeReference("_Main__InnerFn__Closure")),
                                            "_Main__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("_Main__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            parameters: [
                                ConcreteTypeReference("_Main__InnerFn__Closure")
                            ],
                            locals: [
                                Local("b", StringType)
                            ])
                    ])
            },
            {
                "parameter used in closure",
                """
                fn MyFn(a: string)
                {
                    var b = a;
                    fn InnerFn()
                    {
                        var c = a;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyFn__Locals",
                            variants: [Variant("_classVariant", [Field("a", StringType)])]),
                        DataType(
                            "MyFn__InnerFn__Closure",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [Field("MyFn__Locals", ConcreteTypeReference("MyFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(
                            "MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "c",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(
                                                0,
                                                true,
                                                ConcreteTypeReference("MyFn__InnerFn__Closure")),
                                            "MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyFn__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [Local("c", StringType)],
                            parameters: [ConcreteTypeReference("MyFn__InnerFn__Closure")]),
                        Method(
                            "MyFn",
                            [
                                VariableDeclaration(
                                    "__locals",
                                    CreateObject(
                                        ConcreteTypeReference("MyFn__Locals"),
                                        "_classVariant",
                                        true,
                                        new(){{"a", LoadArgument(0, true, StringType)}}),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        LocalAccess(
                                            "__locals",
                                            true,
                                            ConcreteTypeReference("MyFn__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("MyFn__Locals")),
                                Local("b", StringType)
                            ],
                            parameters: [StringType])
                    ])
            },
            {
                "field used in closure",
                """
                class MyClass
                {
                    field MyField: string,

                    fn MyFn()
                    {
                        var a = MyField;
                        fn InnerFn()
                        {
                            var b = MyField;
                        }
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    [Field("MyField", StringType)])
                            ]),
                        DataType(
                            "MyClass__MyFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant", [Field("this", ConcreteTypeReference("MyClass"))])
                            ])
                    ],
                    methods: [
                        Method("MyClass__MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(
                                                0,
                                                true,
                                                ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")),
                                            "this",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyClass")),
                                        "MyField",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [Local("b", StringType)],
                            parameters: [ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")]),
                            Method(
                                "MyClass__MyFn",
                                [
                                    VariableDeclaration(
                                        "a",
                                        FieldAccess(
                                            LoadArgument(
                                                0, true, ConcreteTypeReference("MyClass")),
                                            "MyField",
                                            "_classVariant",
                                            true,
                                            StringType),
                                        false),
                                    MethodReturnUnit()
                                ],
                                locals: [Local("a", StringType)],
                                parameters: [ConcreteTypeReference("MyClass")])
                    ])
            },
            {
                "static field used in inner closure",
                """
                class MyClass
                {
                    static field MyField: string = "",

                    fn MyFn()
                    {
                        fn InnerFn()
                        {
                            var b = MyField;
                        }
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant")
                            ],
                            staticFields: [StaticField("MyField", StringType, StringConstant("", true))]),
                    ],
                    methods: [
                        Method("MyClass__MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "b",
                                    StaticFieldAccess(
                                        ConcreteTypeReference("MyClass"),
                                        "MyField",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [Local("b", StringType)],
                            parameters: []),
                            Method(
                                "MyClass__MyFn",
                                [
                                    MethodReturnUnit()
                                ],
                                locals: [],
                                parameters: [ConcreteTypeReference("MyClass")])
                    ])
            },
            {
                "assigning local in closure",
                """
                var mut a = "";
                a = "hi";
                fn InnerFn()
                {
                    a = "bye";
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", StringType)])
                            ]),
                        DataType(
                            "_Main__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(
                            "_Main__InnerFn",
                            [
                                FieldAssignment(
                                    FieldAccess(
                                        LoadArgument(
                                            0, true, ConcreteTypeReference("_Main__InnerFn__Closure")),
                                        "_Main__Locals",
                                        "_classVariant",
                                        true,
                                        ConcreteTypeReference("_Main__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("bye", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            parameters: [
                                ConcreteTypeReference("_Main__InnerFn__Closure")
                            ]),
                        Method(
                            "_Main",
                            [
                                VariableDeclaration(
                                    "__locals",
                                    CreateObject(
                                        ConcreteTypeReference("_Main__Locals"),
                                        "_classVariant",
                                        true,
                                        []),
                                    false),
                                FieldAssignment(
                                    LocalAccess(
                                        "__locals",
                                        true,
                                        ConcreteTypeReference("_Main__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("", true),
                                    false,
                                    StringType),
                                FieldAssignment(
                                    LocalAccess(
                                        "__locals",
                                        true,
                                        ConcreteTypeReference("_Main__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("hi", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("__locals",
                                    ConcreteTypeReference("_Main__Locals"))
                            ])
                    ])
            },
            {
                "assigning field in closure",
                """
                class MyClass
                {
                    mut field MyField: string,

                    mut fn MyFn()
                    {
                        MyField = "hi";
                        mut fn InnerFn()
                        {
                            MyField = "bye";
                        }
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [Field("MyField", StringType)])
                            ]),
                        DataType(
                            "MyClass__MyFn__InnerFn__Closure",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    fields: [Field("this", ConcreteTypeReference("MyClass"))])
                            ])
                    ],
                    methods: [
                        Method(
                            "MyClass__MyFn__InnerFn",
                            [
                                FieldAssignment(
                                    FieldAccess(
                                        LoadArgument(
                                            0,
                                            true,
                                            ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")),
                                        "this",
                                        "_classVariant",
                                        true,
                                        ConcreteTypeReference("MyClass")),
                                    "_classVariant",
                                    "MyField",
                                    StringConstant("bye", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            parameters: [
                                ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")
                            ]),
                        Method(
                            "MyClass__MyFn",
                            [
                                FieldAssignment(
                                    LoadArgument(
                                        0, true, ConcreteTypeReference("MyClass")),
                                    "_classVariant",
                                    "MyField",
                                    StringConstant("hi", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            parameters: [
                                ConcreteTypeReference("MyClass")
                            ])
                    ])
            },
            {
                "calling closure",
                """
                class MyClass
                {
                    mut field MyField: string,

                    mut fn MyFn(param: string)
                    {
                        var a = "";
                        fn InnerFn()
                        {
                            var _a = a;
                            var _param = param;
                            var _myField = MyField;
                        }
                        InnerFn();
                    }
                }
                """,
                LoweredProgram()
            }
        };
    }

}
