using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ClassTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClassAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "empty class",
                "class MyClass{}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                variants: [Variant("_classVariant")])
                        ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                ["T"],
                                variants: [Variant("_classVariant")])
                        ])
            },
            {
                "generic class with instance function",
                "class MyClass<T>{pub fn SomeFn(){}}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                ["T"],
                                [Variant("_classVariant")])
                        ], methods: [
                                    Method(
                                        "MyClass__SomeFn",
                                        [MethodReturn(UnitConstant(true))],
                                        typeParameters: ["T"],
                                        parameters: [ConcreteTypeReference("MyClass", [GenericPlaceholder("T")])])
                                ])
            },
            {
                "class with instance fields",
                "class MyClass { pub field MyField: string, pub field OtherField: int}",
                LoweredProgram(types: [
                    DataType("MyClass",
                        variants: [
                            Variant(
                                "_classVariant",
                                [
                                    Field("MyField", StringType),
                                    Field("OtherField", Int),
                                ])
                        ])
                ])
            },
            {
                "class with static fields",
                """class MyClass { pub static field MyField: string = ""}""",
                LoweredProgram(types: [
                    DataType("MyClass",
                        variants: [Variant("_classVariant")],
                        staticFields: [
                            StaticField("MyField", StringType, StringConstant("", true))
                        ])
                ])
            },
            {
                "access class field",
                """
                class MyClass {pub field A: string}
                var a = new MyClass{A = ""};
                var b = a.A;
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant", [Field("A", StringType)])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        valueUseful: true,
                                        new(){{"A", StringConstant("", valueUseful: true)}}),
                                    valueUseful: false),
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        LocalAccess("a", true, ConcreteTypeReference("MyClass")),
                                        "A",
                                        "_classVariant",
                                        valueUseful: true,
                                        resolvedType: StringType),
                                    valueUseful: false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", StringType),
                            ])
                    ])
            },
            {
                "access static field",
                """
                class MyClass{pub static field MyField: string = ""}
                var a = MyClass::MyField;
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("MyField", StringType, StringConstant("", true))])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    StaticFieldAccess(
                                        ConcreteTypeReference("MyClass"),
                                        "MyField",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", StringType)])
                    ])
            },
            {
                "call static method",
                """
                MyClass::MyFn();
                class MyClass
                {
                    pub static fn MyFn(){}
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")]),
                    ],
                    methods: [
                        Method("MyClass__MyFn", [MethodReturnUnit()]),
                        Method("_Main",
                            [
                                MethodCall(
                                    FunctionReference("MyClass__MyFn"),
                                    [],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "call instance method",
                """
                class MyClass
                {
                    pub fn MyFn(){}
                }
                var a = new MyClass{};
                a.MyFn();
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(
                            "MyClass__MyFn",
                            [MethodReturnUnit()],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method(
                            "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        true),
                                    false),
                                MethodCall(
                                    FunctionReference("MyClass__MyFn"),
                                    [LocalAccess("a", true, ConcreteTypeReference("MyClass"))],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyClass"))])
                    ])
            },
            {
                "call static method inside function",
                """
                class MyClass
                {
                    pub static fn MyFn(){}
                    pub static fn OtherFn()
                    {
                        MyFn();
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn", [MethodReturnUnit()]),
                        Method("MyClass__OtherFn",
                            [
                                MethodCall(
                                    FunctionReference("MyClass__MyFn"),
                                    [],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "access instance field inside function",
                """
                class MyClass
                {
                    field MyField: string,
                    pub fn MyFn()
                    {
                        var a = MyField;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    FieldAccess(
                                        LoadArgument(0, true, ConcreteTypeReference("MyClass")),
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
                "access static field inside function",
                """
                class MyClass
                {
                    static field MyField: string = "",
                    pub fn MyFn()
                    {
                        var a = MyField;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("MyField", StringType, StringConstant("", true))])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    StaticFieldAccess(
                                        ConcreteTypeReference("MyClass"),
                                        "MyField",
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
                "call instance function inside instance function",
                """
                class MyClass
                {
                    fn MyFn()
                    {
                        OtherFn();
                    }
                    fn OtherFn(){}
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [
                                MethodCall(
                                    FunctionReference("MyClass__OtherFn"),
                                    [LoadArgument(0, true, ConcreteTypeReference("MyClass"))],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method(
                            "MyClass__OtherFn",
                            [MethodReturnUnit()],
                            parameters: [ConcreteTypeReference("MyClass")])
                    ])
            },
            {
                "assign to field through member access",
                """
                class MyClass
                {
                    pub mut field MyField: string
                }
                var mut a = new MyClass{MyField = ""};
                a.MyField = "hi";
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                VariableDeclaration("a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        true,
                                        new(){{"MyField", StringConstant("", true)}}),
                                    false),
                                FieldAssignment(
                                    LocalAccess("a", true, ConcreteTypeReference("MyClass")),
                                    "_classVariant",
                                    "MyField",
                                    StringConstant("hi", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ])
                    ])
            },
            {
                "assign to field in current type",
                """
                class MyClass
                {
                    pub mut field MyField: string,

                    mut fn SomeFn()
                    {
                        MyField = "hi";
                    }

                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                FieldAssignment(
                                    LoadArgument(0, true, ConcreteTypeReference("MyClass")),
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
                "assign to static field in current type",
                """
                class MyClass
                {
                    pub mut static field MyField: string = "",

                    static fn SomeFn()
                    {
                        MyField = "hi";
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
                            staticFields: [
                                StaticField("MyField", StringType, StringConstant("", true))
                            ])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                StaticFieldAssignment(
                                    ConcreteTypeReference("MyClass"),
                                    "MyField",
                                    StringConstant("hi", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "assign to static field through static member access",
                """
                class MyClass
                {
                    pub mut static field MyField: string = "",
                }
                MyClass::MyField = "hi";
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant")
                            ],
                            staticFields: [
                                StaticField("MyField", StringType, StringConstant("", true))
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                StaticFieldAssignment(
                                    ConcreteTypeReference("MyClass"),
                                    "MyField",
                                    StringConstant("hi", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "argument access in instance function",
                """
                class MyClass
                {
                    fn SomeFn(a: string): string { return a; }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant")
                            ])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                MethodReturn(LoadArgument(1, true, StringType))
                            ],
                            parameters: [ConcreteTypeReference("MyClass"), StringType],
                            returnType: StringType)
                    ])
            },
            {
                "argument access in static function",
                """
                class MyClass
                {
                    static fn SomeFn(a: string): string { return a; }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant")
                            ])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                MethodReturn(LoadArgument(0, true, StringType))
                            ],
                            parameters: [StringType],
                            returnType: StringType)
                    ])
            },
            {
                "this reference",
                """
                class MyClass
                {
                    fn SomeFn()
                    {
                        var a = this;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                VariableDeclaration("a",
                                    LoadArgument(0, true, ConcreteTypeReference("MyClass")),
                                    false),
                                MethodReturnUnit()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")],
                            locals: [Local("a", ConcreteTypeReference("MyClass"))])
                    ])
            },
            {
                "non generic function in generic class",
                """
                class MyClass<T>
                {
                    static fn SomeFn(){}
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", ["T"], variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn", [MethodReturnUnit()], ["T"])
                    ])
            },
            {
                "generic function in generic class",
                """
                class MyClass<T>
                {
                    static fn SomeFn<T1>(){}
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", ["T"], variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn", [MethodReturnUnit()], ["T", "T1"])
                    ])
            },
            {
                "reference static generic method in generic type",
                """
                class MyClass<T>
                {
                    pub static fn SomeFn<T1>(){}
                }
                MyClass::<string>::SomeFn::<int>()
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", ["T"], variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn", [MethodReturnUnit()], ["T", "T1"]),
                        Method("_Main",
                            [
                                MethodCall(
                                    FunctionReference(
                                        "MyClass__SomeFn",
                                        [StringType, Int]),
                                    [],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "reference generic method on instance of generic type",
                """
                class MyClass<T>
                {
                    pub fn SomeFn<T2>(){}
                }
                var a = new MyClass::<string>{};
                a.SomeFn::<int>();
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", ["T"], variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(
                            "MyClass__SomeFn",
                            [MethodReturnUnit()],
                            ["T", "T2"],
                            parameters: [ConcreteTypeReference("MyClass", [GenericPlaceholder("T")])]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass", [StringType]),
                                        "_classVariant",
                                        true),
                                    false),
                                MethodCall(
                                    FunctionReference("MyClass__SomeFn", [StringType, Int]),
                                    [LocalAccess("a", true, ConcreteTypeReference("MyClass", [StringType]))],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", [StringType]))
                            ])
                    ])
            },
            {
                "reference generic method inside generic type",
                """
                class MyClass<T>
                {
                    static fn SomeFn<T1>(){}

                    static fn OtherFn()
                    {
                        SomeFn::<string>();
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", ["T"], [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn", [MethodReturnUnit()], ["T", "T1"]),
                        Method("MyClass__OtherFn",
                            [
                                MethodCall(
                                    FunctionReference("MyClass__SomeFn",
                                        [GenericPlaceholder("T"), StringType]),
                                    [],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            ["T"])
                    ])
            },
            {
                "reference non generic method inside generic type",
                """
                class MyClass<T>
                {
                    static fn SomeFn(){}

                    static fn OtherFn()
                    {
                        SomeFn();
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", ["T"], [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn", [MethodReturnUnit()], ["T"]),
                        Method("MyClass__OtherFn",
                            [
                                MethodCall(
                                    FunctionReference("MyClass__SomeFn",
                                        [GenericPlaceholder("T")]),
                                    [],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            ["T"])
                    ])
            }
        };
    }
}
