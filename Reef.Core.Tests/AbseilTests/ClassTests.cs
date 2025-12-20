using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ClassTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClassAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    [Fact]
    public void SingleTest()
    {
        const string source = """
                              class MyClass{pub static field MyField: string = ""}
                              var a = MyClass::MyField;
                              """;
        var expectedProgram = LoweredProgram(
            types:
            [
                DataType(ModuleId,
                    "MyClass",
                    variants: [Variant("_classVariant")],
                    staticFields:
                    [
                        StaticField(
                            "MyField",
                            StringT,
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(new Local("_returnValue"), new Use(new StringConstant("")))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            [])
                    ])
            ],
            methods:
            [
                Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                    [
                        new BasicBlock(new BasicBlockId("bb0"), [
                            new Assign(
                                new Local("_local0"),
                                new Use(new Copy(new StaticField(
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                    "MyField"))))
                        ], new GoTo(new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                    ],
                    Unit,
                    locals: [new MethodLocal("_local0", "a", StringT)])
            ]);
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ClassTests";

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
            {
                "empty class",
                "class MyClass{}",
                LoweredProgram(
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants: [Variant("_classVariant")])
                    ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                LoweredProgram(
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            ["T"],
                            variants: [Variant("_classVariant")])
                    ])
            },
            {
                "generic class with instance function",
                "class MyClass<T>{pub fn SomeFn(){}}",
                LoweredProgram(
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            ["T"],
                            [Variant("_classVariant")])
                    ], methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                            "MyClass__SomeFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit,
                            typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")],
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                        [
                                            new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                                "T")
                                        ]))
                            ])
                    ])
            },
            {
                "class with instance fields",
                "class MyClass { pub field MyField: string, pub field OtherField: i64}",
                LoweredProgram(types:
                [
                    DataType(ModuleId, "MyClass",
                        variants:
                        [
                            Variant(
                                "_classVariant",
                                [
                                    Field("MyField", StringT),
                                    Field("OtherField", Int64T),
                                ])
                        ])
                ])
            },
            {
                "class with static fields",
                """class MyClass { pub static field MyField: string = ""}""",
                LoweredProgram(types:
                [
                    DataType(ModuleId, "MyClass",
                        variants: [Variant("_classVariant")],
                        staticFields:
                        [
                            StaticField(
                                "MyField",
                                StringT,
                                [
                                    new BasicBlock(new BasicBlockId("bb0"), [
                                        new Assign(
                                            new Local("_returnValue"),
                                            new Use(new StringConstant("")))
                                    ], new GoTo(new BasicBlockId("bb1"))),
                                    new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                ],
                                [])
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
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants:
                            [
                                Variant("_classVariant", [Field("A", StringT)])
                            ])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(new LoweredConcreteTypeReference("MyClass",
                                            new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                    new Assign(
                                        new Field(new Local("_local0"), "A", "_classVariant"),
                                        new Use(new StringConstant(""))),
                                    new Assign(
                                        new Local("_local1"),
                                        new Use(new Copy(new Field(new Local("_local0"), "A", "_classVariant"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new MethodLocal("_local0", "a",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                new MethodLocal("_local1", "b", StringT),
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
                    types:
                    [
                        DataType(ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields:
                            [
                                StaticField(
                                    "MyField",
                                    StringT,
                                    [
                                        new BasicBlock(new BasicBlockId("bb0"), [
                                            new Assign(new Local("_returnValue"), new Use(new StringConstant("")))
                                        ], new GoTo(new BasicBlockId("bb1"))),
                                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                    ],
                                    [])
                            ])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new Use(new Copy(new StaticField(
                                            new LoweredConcreteTypeReference("MyClass",
                                                new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                            "MyField"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", "a", StringT)])
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
                    types:
                    [
                        DataType(ModuleId, "MyClass", variants: [Variant("_classVariant")]),
                    ],
                    methods:
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                            "MyClass__MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit),
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new MethodCall(
                                    new LoweredFunctionReference(
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                    [],
                                    new Local("_local0"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new MethodLocal("_local0", null, Unit)
                            ])
                    ])
            },
            {
                "assign instance method to function variable from within type but different method",
                """
                class MyClass {
                    pub fn OtherFn(){}
                    pub fn MyFn() {
                        var a = OtherFn; 
                    }
                }
                """,
                LoweredProgram(
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants:
                            [
                                Variant("_classVariant")
                            ])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit,
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(
                                            new LoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0),
                                                [Unit]))),
                                    new Assign(
                                        new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                        new Use(new FunctionPointerConstant(
                                            new LoweredFunctionReference(
                                                new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                                                [])))),
                                    new Assign(
                                        new Field(new Local("_local0"), "FunctionParameter", "_classVariant"),
                                        new Use(new Copy(new Local("_param0"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new MethodLocal("_local0", "a",
                                    new LoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit])),
                            ],
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ])
                    ])
            },
            {
                "assign instance method to function variable from within type",
                """
                class MyClass {
                    pub fn MyFn() {
                        var a = MyFn; 
                    }
                }
                """,
                LoweredProgram(
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants:
                            [
                                Variant("_classVariant")
                            ])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(
                                            new LoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0),
                                                [Unit]))),
                                    new Assign(
                                        new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                        new Use(new FunctionPointerConstant(
                                            new LoweredFunctionReference(
                                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), [])))),
                                    new Assign(
                                        new Field(new Local("_local0"), "FunctionParameter", "_classVariant"),
                                        new Use(new Copy(new Local("_param0"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new MethodLocal("_local0", "a",
                                    new LoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
                            ],
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
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
                    types:
                    [
                        DataType(ModuleId, "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                            "MyClass__MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit,
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(
                                            new LoweredConcreteTypeReference("MyClass",
                                                new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                ], new MethodCall(
                                    new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                                        []),
                                    [new Copy(new Local("_local0"))],
                                    new Local("_local1"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new MethodLocal("_local0", "a",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                new MethodLocal("_local1", null, Unit)
                            ])
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
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn", [
                            new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                        ], Unit),
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new MethodCall(
                                    new LoweredFunctionReference(
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                    [],
                                    new Local("_local0"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", null, Unit)])
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
                    types:
                    [
                        DataType(ModuleId,
                            "MyClass",
                            variants:
                            [
                                Variant("_classVariant", [Field("MyField", StringT)])
                            ])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new Use(new Copy(new Field(new Local("_param0"), "MyField", "_classVariant"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new MethodLocal("_local0", "a", StringT),
                            ],
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ])
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
                    types:
                    [
                        DataType(ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields:
                            [
                                StaticField(
                                    "MyField",
                                    StringT,
                                    [
                                        new BasicBlock(new BasicBlockId("bb0"), [
                                            new Assign(new Local("_returnValue"), new Use(new StringConstant("")))
                                        ], new GoTo(new BasicBlockId("bb1"))),
                                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                    ],
                                    [])
                            ])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new Use(new Copy(new StaticField(
                                            new LoweredConcreteTypeReference("MyClass",
                                                new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                            "MyField"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", "a", StringT)],
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ])
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
                    types:
                    [
                        DataType(ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods:
                    [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new MethodCall(
                                    new LoweredFunctionReference(
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []),
                                    [new Copy(new Local("_param0"))],
                                    new Local("_local0"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", null, Unit)],
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                            "MyClass__OtherFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                            ],
                            Unit,
                            parameters:
                            [
                                ("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ])
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
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant", [Field("MyField", StringT)])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Field(new Local("_local0"), "MyField", "_classVariant"),
                                             new Use(new StringConstant(""))),
                                         new Assign(
                                             new Field(new Local("_local0"), "MyField", "_classVariant"),
                                             new Use(new StringConstant("hi")))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
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
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant", [Field("MyField", StringT)])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Field(new Local("_param0"),
                                             "MyField",
                                             "_classVariant"),
                                         new Use(new StringConstant("hi")))
                                 ], new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
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
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant")
                             ],
                             staticFields: [
                                 StaticField(
                                     "MyField",
                                     StringT,
                                     [
                                         new BasicBlock(new BasicBlockId("bb0"), [
                                             new Assign(new Local("_returnValue"), new Use(new StringConstant("")))
                                         ], new GoTo(new BasicBlockId("bb1"))),
                                         new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                     ], [])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(new StaticField(
                                         new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                         "MyField"),
                                         new Use(new StringConstant("hi")))
                                 ], new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit)
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
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant")
                             ],
                             staticFields: [
                                 StaticField("MyField", StringT, [
                                     new BasicBlock(new BasicBlockId("bb0"), [
                                         new Assign(new Local("_returnValue"),
                                             new Use(new StringConstant("")))
                                     ], new GoTo(new BasicBlockId("bb1"))),
                                     new BasicBlock(new BasicBlockId("bb1"), [], new Return()),
                                 ], [])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(new StaticField(
                                         new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                         "MyField"),
                                         new Use(new StringConstant("hi")))
                                 ], new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit)
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
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant")
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_returnValue"),
                                         new Use(new Copy(new Local("_param1"))))
                                 ], new Return()),
                             ],
                             StringT,
                             parameters: [
                                 ("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 ("a", StringT)])
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
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant")
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_returnValue"),
                                         new Use(new Copy(new Local("_param0"))))
                                 ], new Return())
                             ],
                             StringT,
                             parameters: [("a", StringT)])
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
                         DataType(ModuleId, "MyClass", variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new Use(new Copy(new Local("_param0"))))
                                 ], new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return()),
                                 // VariableDeclaration("a",
                                 //     LoadArgument(0, true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
                                 //     false),
                                 // MethodReturnUnit()
                             ],
                             Unit,
                             parameters: [("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))],
                             locals: [new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
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
                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
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
                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             typeParameters: [
                                 (new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"),
                                 (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")
                             ])
                     ])
             },
             {
                 "reference static generic method in generic type",
                 """
                 class MyClass<T>
                 {
                     pub static fn SomeFn<T1>(){}
                 }
                 MyClass::<string>::SomeFn::<i64>();
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                             [StringT, Int64T]),
                                         [],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", null, Unit)])
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
                 a.SomeFn::<i64>();
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), 
                             "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit,
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")],
                             parameters: [
                                 (
                                     "this",
                                     new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
                                 )
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
                                                     "MyClass",
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringT]))),
                                     ],
                                     new MethodCall(
                                             new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                                 [StringT, Int64T]),
                                             [new Copy(new Local("_local0"))],
                                             new Local("_local1"),
                                             new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringT])),
                                 new MethodLocal("_local1", null, Unit)
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
                         DataType(ModuleId, "MyClass", ["T"], [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit,
                             typeParameters: [
                                 (new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"),
                                 (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")
                             ]),
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                             "MyClass__OtherFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                             [
                                                 new LoweredGenericPlaceholder(
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                                     "T"),
                                                 StringT
                                             ]),
                                         [],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", null, Unit)],
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
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
                         DataType(ModuleId, "MyClass", ["T"], [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit,
                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")]),
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                             "MyClass__OtherFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                             [
                                                 new LoweredGenericPlaceholder(
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                                     "T")
                                             ]),
                                         [],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", null, Unit)
                             ],
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
                     ])
             }
        };
    }
}