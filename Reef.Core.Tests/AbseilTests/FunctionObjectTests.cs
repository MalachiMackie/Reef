using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class FunctionObjectTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void FunctionObjectAbseilTest(string description, string source, LoweredProgram expectedProgram)
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
                "assign global function to function object",
                """
                fn SomeFn(){}
                var a = SomeFn;
                """,
                LoweredProgram(
                    methods: [
                        Method("SomeFn",
                            [
                                MethodReturnUnit()
                            ]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new() {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("SomeFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assign static function to function object inside type",
                """
                class MyClass {
                    static fn OtherFn(){}
                    static fn MyFn() {
                        var a = OtherFn;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__OtherFn",
                            [MethodReturnUnit()]),
                        Method("MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new(){
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__OtherFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assign static function to function object",
                """
                class MyClass {
                    pub static fn OtherFn(){}
                }
                var a = MyClass::OtherFn;
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__OtherFn",
                            [MethodReturnUnit()]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new(){
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__OtherFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assign instance function to function object",
                """
                class MyClass {
                    pub fn MyFn(){}
                }
                var a = new MyClass{};
                var b = a.MyFn;
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
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        true),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__MyFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            },
                                            {
                                                "FunctionParameter",
                                                LocalAccess(
                                                    "a",
                                                    true,
                                                    ConcreteTypeReference("MyClass"))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assigning closure to function object",
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
                        var b = InnerFn;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType(
                            "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ]),
                        DataType(
                            "MyClass__MyFn__Locals",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field("a", StringType),
                                        Field("param", StringType)
                                    ])
                            ]),
                        DataType(
                            "MyClass__MyFn__InnerFn__Closure",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field("this", ConcreteTypeReference("MyClass")),
                                        Field(
                                            "MyClass__MyFn__Locals",
                                            ConcreteTypeReference("MyClass__MyFn__Locals"))
                                    ])
                            ])
                    ],
                    methods: [
                        Method(
                            "MyClass__MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "_a",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(
                                                0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")),
                                            "MyClass__MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyClass__MyFn__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                VariableDeclaration(
                                    "_param",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(
                                                0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")),
                                            "MyClass__MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyClass__MyFn__Locals")),
                                        "param",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                VariableDeclaration(
                                    "_myField",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(
                                                0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")),
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
                            locals: [
                                Local("_a", StringType),
                                Local("_param", StringType),
                                Local("_myField", StringType)
                            ],
                            parameters: [
                                ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")
                            ]),
                        Method(
                            "MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "__locals",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass__MyFn__Locals"),
                                        "_classVariant",
                                        true,
                                        new(){{"param", LoadArgument(1, true, StringType)}}),
                                    false),
                                FieldAssignment(
                                    LocalAccess("__locals", true, ConcreteTypeReference("MyClass__MyFn__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("", true),
                                    false,
                                    StringType),
                                VariableDeclaration(
                                    "b",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__MyFn__InnerFn"),
                                                    true,
                                                    FunctionType(
                                                        [ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure")],
                                                        Unit))
                                            },
                                            {
                                                "FunctionParameter",
                                                CreateObject(
                                                    ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure"),
                                                    "_classVariant",
                                                    true,
                                                    new()
                                                    {
                                                        {
                                                            "this",
                                                            LoadArgument(0, true, ConcreteTypeReference("MyClass"))
                                                        },
                                                        {
                                                            "MyClass__MyFn__Locals",
                                                            LocalAccess(
                                                                "__locals",
                                                                true,
                                                                ConcreteTypeReference("MyClass__MyFn__Locals"))
                                                        }
                                                    })
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            parameters: [ConcreteTypeReference("MyClass"), StringType],
                            locals: [
                                Local("__locals", ConcreteTypeReference("MyClass__MyFn__Locals")),
                                Local("b", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "call function object without parameters",
                """
                fn SomeFn() {}
                var a = SomeFn;
                a();
                """,
                LoweredProgram(
                    methods: [
                        Method("SomeFn", [MethodReturnUnit()]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("SomeFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodCall(
                                    FunctionReference("Function`1__Call"),
                                    [
                                        LocalAccess(
                                            "a",
                                            true,
                                            ConcreteTypeReference("Function`1", [Unit]))
                                    ],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local(
                                    "a",
                                    ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "call function object with parameters",
                """
                fn SomeFn(a: string): int { return 1; }
                var a = SomeFn;
                var b = a("");
                """,
                LoweredProgram(
                    methods: [
                        Method(
                            "SomeFn",
                            [MethodReturn(IntConstant(1, true))],
                            parameters: [StringType],
                            returnType: Int),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`2", [StringType, Int]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("SomeFn"),
                                                    true,
                                                    FunctionType([StringType], Int))
                                            }
                                        }),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    MethodCall(
                                        FunctionReference("Function`2__Call"),
                                        [
                                            LocalAccess(
                                                "a",
                                                true,
                                                ConcreteTypeReference("Function`2", [StringType, Int])),
                                            StringConstant("", true)
                                        ],
                                        true,
                                        Int),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local(
                                    "a",
                                    ConcreteTypeReference("Function`2", [StringType, Int])),
                                Local("b", Int)
                            ])
                    ])
            }
        };

        
    }
}

