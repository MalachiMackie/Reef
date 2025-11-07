using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class FunctionObjectTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void FunctionObjectAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string _moduleId = "FunctionObjectTests";
    
    
    [Fact]
    public void SingleTest()
    {
        const string source = """
                union MyUnion{A(string)}
                var a = MyUnion::A;
                var b = a("");
                """;
        var expectedProgram = LoweredProgram(
            types:
            [
                DataType(_moduleId, "MyUnion",
                    variants:
                    [
                        Variant(
                            "A",
                            [
                                Field("_variantIdentifier", UInt16_t),
                                Field("Item0", StringType)
                            ])
                    ])
            ],
            methods:
            [
                Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                    [
                        MethodReturn(
                            CreateObject(
                                ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                "A",
                                true,
                                new()
                                {
                                    { "Item0", LoadArgument(0, true, StringType) },
                                    { "_variantIdentifier", UInt16Constant(0, true) }
                                }))
                    ],
                    parameters: [StringType],
                    returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                    [
                        VariableDeclaration(
                            "a",
                            CreateObject(
                                ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))]),
                                "_classVariant",
                                true,
                                new()
                                {
                                    {
                                        "FunctionReference",
                                        FunctionReferenceConstant(
                                            FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A"),
                                            true,
                                            FunctionType([StringType], ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))))
                                    }
                                }),
                            false),
                        VariableDeclaration(
                            "b",
                            MethodCall(
                                FunctionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))]),
                                [
                                    LocalAccess("a", true, ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))])),
                                    StringConstant("", true)
                                ],
                                true,
                                ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                            false),
                        MethodReturnUnit()
                    ],
                    locals:
                    [
                        Local("a", ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))])),
                        Local("b", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                    ])
            ]);
        
        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "assign union tuple variant method to function object",
                """
                union MyUnion{A(string)}
                var a = MyUnion::A;
                var b = a("");
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16_t),
                                        Field("Item0", StringType)
                                    ])
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
                                        new()
                                        {
                                            {"Item0", LoadArgument(0, true, StringType)},
                                            {"_variantIdentifier", UInt16Constant(0, true)}
                                        }))
                            ],
                            parameters: [StringType],
                            returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A"),
                                                    true,
                                                    FunctionType([StringType], ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))))
                                            }
                                        }),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    MethodCall(
                                        FunctionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))]),
                                        [LocalAccess("a", true, ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))])), StringConstant("", true)],
                                        true,
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))])),
                                Local("b", ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                            ])
                    ])
            },
            {
                "assign global function to function object",
                """
                fn SomeFn(){}
                var a = SomeFn;
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                MethodReturnUnit()
                            ]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new() {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
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
                        DataType(_moduleId, "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [MethodReturnUnit()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new(){
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__OtherFn"), "MyClass__OtherFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
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
                        DataType(_moduleId, "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [MethodReturnUnit()]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new(){
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__OtherFn"), "MyClass__OtherFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
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
                        DataType(_moduleId, "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [MethodReturnUnit()],
                            parameters: [ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")),
                                        "_classVariant",
                                        true),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            },
                                            {
                                                "FunctionParameter",
                                                LocalAccess(
                                                    "a",
                                                    true,
                                                    ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                Local("b", ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
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
                        DataType(_moduleId, "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("MyField", StringType)])
                            ]),
                        DataType(_moduleId, "MyClass__MyFn__Locals",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field("a", StringType),
                                        Field("param", StringType)
                                    ])
                            ]),
                        DataType(_moduleId, "MyClass__MyFn__InnerFn__Closure",
                            variants: [
                                Variant(
                                    "_classVariant",
                                    [
                                        Field("this", ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
                                        Field(
                                            "MyClass__MyFn__Locals",
                                            ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals")))
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "_a",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(
                                                0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn__Closure"))),
                                            "MyClass__MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals"))),
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
                                                0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn__Closure"))),
                                            "MyClass__MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals"))),
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
                                                0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn__Closure"))),
                                            "this",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"))),
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
                                ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn__Closure"))
                            ]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "__locals",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals")),
                                        "_classVariant",
                                        true,
                                        new(){{"param", LoadArgument(1, true, StringType)}}),
                                    false),
                                FieldAssignment(
                                    LocalAccess("__locals", true, ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals"))),
                                    "_classVariant",
                                    "a",
                                    StringConstant("", true),
                                    false,
                                    StringType),
                                VariableDeclaration(
                                    "b",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn"),
                                                    true,
                                                    FunctionType(
                                                        [ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn__Closure"))],
                                                        Unit))
                                            },
                                            {
                                                "FunctionParameter",
                                                CreateObject(
                                                    ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__InnerFn__Closure")),
                                                    "_classVariant",
                                                    true,
                                                    new()
                                                    {
                                                        {
                                                            "this",
                                                            LoadArgument(0, true, ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")))
                                                        },
                                                        {
                                                            "MyClass__MyFn__Locals",
                                                            LocalAccess(
                                                                "__locals",
                                                                true,
                                                                ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals")))
                                                        }
                                                    })
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            parameters: [ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass")), StringType],
                            locals: [
                                Local("__locals", ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn__Locals"))),
                                Local("b", ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
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
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn", [MethodReturnUnit()]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodCall(
                                    FunctionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [Unit]),
                                    [
                                        LocalAccess(
                                            "a",
                                            true,
                                            ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
                                    ],
                                    false,
                                    Unit),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local(
                                    "a",
                                    ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
                            ])
                    ])
            },
            {
                "call function object with parameters",
                """
                fn SomeFn(a: string): i64 { return 1; }
                var a = SomeFn;
                var b = a("");
                """,
                LoweredProgram(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [MethodReturn(Int64Constant(1, true))],
                            parameters: [StringType],
                            returnType: Int64_t),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, Int64_t]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn"),
                                                    true,
                                                    FunctionType([StringType], Int64_t))
                                            }
                                        }),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    MethodCall(
                                        FunctionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [StringType, Int64_t]),
                                        [
                                            LocalAccess(
                                                "a",
                                                true,
                                                ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, Int64_t])),
                                            StringConstant("", true)
                                        ],
                                        true,
                                        Int64_t),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local(
                                    "a",
                                    ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, Int64_t])),
                                Local("b", Int64_t)
                            ])
                    ])
            },
            {
                "assign generic function to function object",
                """
                class MyClass<T>
                {
                    pub static fn SomeFn<T2>(){}
                }
                var a = MyClass::<string>::SomeFn::<i64>;
                """,
                LoweredProgram(
                    types: [
                        DataType(_moduleId, "MyClass", ["T"], [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [MethodReturnUnit()],
                            [(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T"), (new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "T2")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn", [StringType, Int64_t]),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a",
                                    ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
                            ])
                    ])
            }
        };

        
    }
}

