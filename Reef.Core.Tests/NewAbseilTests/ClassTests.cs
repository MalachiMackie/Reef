using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ClassTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClassAbseilTest(string description, string source, NewLoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

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
        var expectedProgram = NewLoweredProgram(
            types:
            [
                NewDataType(ModuleId,
                    "MyClass",
                    variants: [NewVariant("_classVariant")],
                    staticFields:
                    [
                        NewStaticField(
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
                NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                    [
                        new BasicBlock(new BasicBlockId("bb0"), [
                            new Assign(
                                new Local("_local0"),
                                new Use(new Copy(new StaticField(
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                    "MyField"))))
                        ], new GoTo(new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                    ],
                    Unit,
                    locals: [new NewMethodLocal("_local0", "a", StringT)])
            ]);
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ClassTests";

    public static TheoryData<string, string, NewLoweredModule> TestCases()
    {
        return new()
        {
            {
                "empty class",
                "class MyClass{}",
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            variants: [NewVariant("_classVariant")])
                    ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            ["T"],
                            variants: [NewVariant("_classVariant")])
                    ])
            },
            {
                "generic class with instance function",
                "class MyClass<T>{pub fn SomeFn(){}}",
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            ["T"],
                            [NewVariant("_classVariant")])
                    ], methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                            "MyClass__SomeFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit,
                            typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")],
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                        [
                                            new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                                "T")
                                        ]))
                            ])
                    ])
            },
            {
                "class with instance fields",
                "class MyClass { pub field MyField: string, pub field OtherField: i64}",
                NewLoweredProgram(types:
                [
                    NewDataType(ModuleId, "MyClass",
                        variants:
                        [
                            NewVariant(
                                "_classVariant",
                                [
                                    NewField("MyField", StringT),
                                    NewField("OtherField", Int64T),
                                ])
                        ])
                ])
            },
            {
                "class with static fields",
                """class MyClass { pub static field MyField: string = ""}""",
                NewLoweredProgram(types:
                [
                    NewDataType(ModuleId, "MyClass",
                        variants: [NewVariant("_classVariant")],
                        staticFields:
                        [
                            NewStaticField(
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            variants:
                            [
                                NewVariant("_classVariant", [NewField("A", StringT)])
                            ])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(new NewLoweredConcreteTypeReference("MyClass",
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
                                new NewMethodLocal("_local0", "a",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                new NewMethodLocal("_local1", "b", StringT),
                            ])
                    ])
            },
            {
                "access static field",
                """
                class MyClass{pub static field MyField: string = ""}
                var a = MyClass::MyField;
                """,
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId,
                            "MyClass",
                            variants: [NewVariant("_classVariant")],
                            staticFields:
                            [
                                NewStaticField(
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
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new Use(new Copy(new StaticField(
                                            new NewLoweredConcreteTypeReference("MyClass",
                                                new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                            "MyField"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", "a", StringT)])
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass", variants: [NewVariant("_classVariant")]),
                    ],
                    methods:
                    [
                        NewMethod(
                            new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                            "MyClass__MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new MethodCall(
                                    new NewLoweredFunctionReference(
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                    [],
                                    new Local("_local0"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new NewMethodLocal("_local0", null, Unit)
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            variants:
                            [
                                NewVariant("_classVariant")
                            ])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit,
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ]),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(
                                            new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0),
                                                [Unit]))),
                                    new Assign(
                                        new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                        new Use(new FunctionPointerConstant(
                                            new NewLoweredFunctionReference(
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
                                new NewMethodLocal("_local0", "a",
                                    new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit])),
                            ],
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            variants:
                            [
                                NewVariant("_classVariant")
                            ])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(
                                            new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0),
                                                [Unit]))),
                                    new Assign(
                                        new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                        new Use(new FunctionPointerConstant(
                                            new NewLoweredFunctionReference(
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
                                new NewMethodLocal("_local0", "a",
                                    new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
                            ],
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass", variants: [NewVariant("_classVariant")])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                            "MyClass__MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                            Unit,
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ]),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new CreateObject(
                                            new NewLoweredConcreteTypeReference("MyClass",
                                                new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                ], new MethodCall(
                                    new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                                        []),
                                    [new Copy(new Local("_local0"))],
                                    new Local("_local1"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals:
                            [
                                new NewMethodLocal("_local0", "a",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                new NewMethodLocal("_local1", null, Unit)
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId, "MyClass",
                            variants: [NewVariant("_classVariant")])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn", [
                            new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                        ], Unit),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new MethodCall(
                                    new NewLoweredFunctionReference(
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                    [],
                                    new Local("_local0"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, Unit)])
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId,
                            "MyClass",
                            variants:
                            [
                                NewVariant("_classVariant", [NewField("MyField", StringT)])
                            ])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
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
                                new NewMethodLocal("_local0", "a", StringT),
                            ],
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId,
                            "MyClass",
                            variants: [NewVariant("_classVariant")],
                            staticFields:
                            [
                                NewStaticField(
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
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(
                                        new Local("_local0"),
                                        new Use(new Copy(new StaticField(
                                            new NewLoweredConcreteTypeReference("MyClass",
                                                new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                            "MyField"))))
                                ], new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", "a", StringT)],
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
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
                NewLoweredProgram(
                    types:
                    [
                        NewDataType(ModuleId,
                            "MyClass",
                            variants: [NewVariant("_classVariant")])
                    ],
                    methods:
                    [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new MethodCall(
                                    new NewLoweredFunctionReference(
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []),
                                    [new Copy(new Local("_param0"))],
                                    new Local("_local0"),
                                    new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [new NewMethodLocal("_local0", null, Unit)],
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                            ]),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                            "MyClass__OtherFn",
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                            ],
                            Unit,
                            parameters:
                            [
                                ("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant", [NewField("MyField", StringT)])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
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
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant", [NewField("MyField", StringT)])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
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
                                 ("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ],
                             staticFields: [
                                 NewStaticField(
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
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(new StaticField(
                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ],
                             staticFields: [
                                 NewStaticField("MyField", StringT, [
                                     new BasicBlock(new BasicBlockId("bb0"), [
                                         new Assign(new Local("_returnValue"),
                                             new Use(new StringConstant("")))
                                     ], new GoTo(new BasicBlockId("bb1"))),
                                     new BasicBlock(new BasicBlockId("bb1"), [], new Return()),
                                 ], [])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(new StaticField(
                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_returnValue"),
                                         new Use(new Copy(new Local("_param1"))))
                                 ], new Return()),
                             ],
                             StringT,
                             parameters: [
                                 ("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
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
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))],
                             locals: [new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                             [StringT, Int64T]),
                                         [],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new NewMethodLocal("_local0", null, Unit)])
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), 
                             "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit,
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")],
                             parameters: [
                                 (
                                     "this",
                                     new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
                                 )
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference(
                                                     "MyClass",
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringT]))),
                                     ],
                                     new MethodCall(
                                             new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                                 [StringT, Int64T]),
                                             [new Copy(new Local("_local0"))],
                                             new Local("_local1"),
                                             new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringT])),
                                 new NewMethodLocal("_local1", null, Unit)
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(
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
                         NewMethod(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                             "MyClass__OtherFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                             [
                                                 new NewLoweredGenericPlaceholder(
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
                             locals: [new NewMethodLocal("_local0", null, Unit)],
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                             "MyClass__SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit,
                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")]),
                         NewMethod(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                             "MyClass__OtherFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
                                             [
                                                 new NewLoweredGenericPlaceholder(
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
                                 new NewMethodLocal("_local0", null, Unit)
                             ],
                             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
                     ])
             }
        };
    }
}