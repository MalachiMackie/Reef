using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class MethodTests
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "access parameter",
                """
                static fn SomeFn(a: int, b: int) {
                    var foo = a;
                    var bar = b;
                }
                """,
                Module(
                    methods:
                    [
                        Method("SomeFn",
                            isStatic: true,
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "foo", Type = ConcreteTypeReference("int") },
                                new ReefMethod.Local { DisplayName = "bar", Type = ConcreteTypeReference("int") },
                            ],
                            parameters:
                            [
                                Parameter("a", ConcreteTypeReference("int")),
                                Parameter("b", ConcreteTypeReference("int")),
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 1),
                                new StoreLocal(Addr(3), 1),
                                LoadUnit(4),
                                Return(5)
                            ]),
                    ])
            },
            {
                "call global method",
                """
                static fn FirstFn(){}
                FirstFn();
                """,
                Module(
                    methods:
                    [
                        Method("FirstFn", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                        Method("!Main", isStatic: true, instructions:
                        [
                            new LoadGlobalFunction(Addr(0), FunctionDefinitionReference("FirstFn")),
                            new Call(Addr(1)),
                            Drop(2),
                            LoadUnit(3),
                            Return(4)
                        ])
                    ]
                )
            },
            {
                "call instance and static methods",
                """
                class MyClass {
                    static fn Ignore() {}
                    pub static fn StaticFn() {}
                    pub fn InstanceFn() {}
                }
                new MyClass{}.InstanceFn();
                MyClass::StaticFn();
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("Ignore", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                                Method("StaticFn", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                                Method(
                                    "InstanceFn",
                                    isStatic: false,
                                    parameters: [Parameter("this", ConcreteTypeReference("MyClass"))],
                                    instructions: [LoadUnit(0), Return(1)]),
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new LoadTypeFunction(Addr(1), ConcreteTypeReference("MyClass"), 2, []),
                                new Call(Addr(2)),
                                Drop(3),
                                new LoadTypeFunction(Addr(4), ConcreteTypeReference("MyClass"), 1, []),
                                new Call(Addr(5)),
                                Drop(6),
                                LoadUnit(7),
                                Return(8)
                            ])
                    ])
            },
            {
                "functions in inner block",
                """
                static fn SomeFn() {
                    { 
                        fn InnerFn() {
                        }
                        
                        InnerFn();
                    }
                }
                """,
                Module(
                    methods: [
                        Method(
                            "InnerFn",
                            isStatic: false,
                            instructions: [LoadUnit(0), Return(1)]),
                        Method("SomeFn",
                            isStatic: true,
                            instructions: [
                                new LoadGlobalFunction(Addr(0), FunctionDefinitionReference("InnerFn")),
                                new Call(Addr(1)),
                                Drop(2),
                                LoadUnit(3),
                                Drop(4),
                                LoadUnit(5),
                                Return(6)
                            ])
                    ])
            },
            {
                "assign global function to variable",
                """
                fn SomeFn(param: int) {
                }
                var a = SomeFn;
                a(1);
                """,
                Module(
                    methods: [
                        Method("SomeFn",
                            parameters: [
                                Parameter("param", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                LoadUnit(0),
                                Return(1)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("Function`2", 
                                    [ConcreteTypeReference("int"), ConcreteTypeReference("Unit")]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference(
                                    "Function`2",
                                    [ConcreteTypeReference("int"), ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(1)),
                                new LoadGlobalFunction(Addr(2), FunctionDefinitionReference("SomeFn")),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadIntConstant(Addr(6), 1),
                                new LoadTypeFunction(Addr(7), ConcreteTypeReference(
                                    "Function`2",
                                    [ConcreteTypeReference("int"), ConcreteTypeReference("Unit")]), 0, []),
                                new Call(Addr(8)),
                                Drop(9),
                                LoadUnit(10),
                                Return(11)
                            ])
                    ])
            },
            {
                "assign instance function to variable",
                """
                class MyClass {
                    pub fn MyFn() {
                    }
                }
                var a = new MyClass{};
                var b = a.MyFn;
                b();
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(3)),
                                new LoadTypeFunction(Addr(4), ConcreteTypeReference("MyClass"), 0, []),
                                new StoreField(Addr(5), 0, 0),
                                new CopyStack(Addr(6)),
                                new LoadLocal(Addr(7), 0),
                                new StoreField(Addr(8), 0, 1),
                                new StoreLocal(Addr(9), 1),
                                new LoadLocal(Addr(10), 1),
                                new LoadTypeFunction(Addr(11), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0, []),
                                new Call(Addr(12)),
                                Drop(13),
                                LoadUnit(14),
                                Return(15)
                            ])
                    ])
            },
            {
                "assign static type function to variable",
                """
                class MyClass { 
                    pub static fn MyFn() {}
                }
                var a = MyClass::MyFn;
                a();
                """,
                Module(
                    types: [
                        Class("MyClass", methods: [
                            Method("MyFn",
                                isStatic: true,
                                instructions: [
                                    LoadUnit(0),
                                    Return(1)
                                ])
                        ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(1)),
                                new LoadTypeFunction(Addr(2), ConcreteTypeReference("MyClass"), 0, []),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadTypeFunction(Addr(6), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0, []),
                                new Call(Addr(7)),
                                Drop(8),
                                LoadUnit(9),
                                Return(10)
                            ])
                    ])
            },
            {
                "assign static type function with parameters and return type to variable",
                """
                class MyClass { 
                    pub static fn MyFn(a: string, b: int): bool { return true; }
                }
                var a = MyClass::MyFn;
                a("", 1);
                """,
                Module(
                    types: [
                        Class("MyClass", methods: [
                            Method("MyFn",
                                isStatic: true,
                                parameters: [
                                    Parameter("a", ConcreteTypeReference("string")),
                                    Parameter("b", ConcreteTypeReference("int"))
                                ],
                                returnType: ConcreteTypeReference("bool"),
                                instructions: [
                                    new LoadBoolConstant(Addr(0), true),
                                    Return(1)
                                ])
                        ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference(
                                    "Function`3",
                                    [
                                        ConcreteTypeReference("string"),
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("bool")
                                    ]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference(
                                    "Function`3",
                                    [
                                        ConcreteTypeReference("string"),
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("bool")
                                    ])),
                                new CopyStack(Addr(1)),
                                new LoadTypeFunction(Addr(2), ConcreteTypeReference("MyClass"), 0, []),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadStringConstant(Addr(6), ""),
                                new LoadIntConstant(Addr(7), 1),
                                new LoadTypeFunction(Addr(8), ConcreteTypeReference(
                                    "Function`3",
                                    [
                                        ConcreteTypeReference("string"),
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("bool")
                                    ]), 0, []),
                                new Call(Addr(9)),
                                Drop(10),
                                LoadUnit(11),
                                Return(12)
                            ])
                    ])
            },
            {
                "assign instance function to variable from within method",
                """
                class MyClass {
                    fn MyFn() {
                        var a = MyFn;
                    }
                }
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    locals: [
                                        Local("a", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                                    ],
                                    instructions: [
                                        new CreateObject(Addr(0), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                        new CopyStack(Addr(1)),
                                        new LoadTypeFunction(Addr(2), ConcreteTypeReference("MyClass"), 0, []),
                                        new StoreField(Addr(3), 0, 0),
                                        new CopyStack(Addr(4)),
                                        new LoadArgument(Addr(5), 0),
                                        new StoreField(Addr(6), 0, 1),
                                        new StoreLocal(Addr(7), 0),
                                        LoadUnit(8),
                                        Return(9)
                                    ])
                            ])
                    ])
            },
            {
                "call instance function from within instance function",
                """
                class MyClass {
                    fn MyFn() {
                        MyFn();
                    }
                }
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        new LoadArgument(Addr(0), 0),
                                        new LoadTypeFunction(Addr(1), ConcreteTypeReference("MyClass"), 0, []),
                                        new Call(Addr(2)),
                                        Drop(3),
                                        LoadUnit(4),
                                        Return(5)
                                    ])
                            ])
                    ])
            },
            {
                "assign instance function to variable from within same instance but different method",
                """
                class MyClass {
                    fn MyFn() {}
                    fn OtherFn() {
                        var a = MyFn;
                    }
                }
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ]),
                                Method("OtherFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    locals: [
                                        Local("a", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                                    ],
                                    instructions: [
                                        new CreateObject(Addr(0), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                        new CopyStack(Addr(1)),
                                        new LoadTypeFunction(Addr(2), ConcreteTypeReference("MyClass"), 0, []),
                                        new StoreField(Addr(3), 0, 0),
                                        new CopyStack(Addr(4)),
                                        new LoadArgument(Addr(5), 0),
                                        new StoreField(Addr(6), 0, 1),
                                        new StoreLocal(Addr(7), 0),
                                        LoadUnit(8),
                                        Return(9)
                                    ])
                            ])
                    ])
            },
            {
                "call generic method",
                """
                fn MyFn<T>() {}
                MyFn::<string>();
                """,
                Module(
                    methods: [
                        Method("MyFn",
                            typeParameters: ["T"],
                            instructions: [
                                LoadUnit(0),
                                Return(1)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new LoadGlobalFunction(Addr(0), FunctionDefinitionReference("MyFn", [ConcreteTypeReference("string")])),
                                new Call(Addr(1)),
                                Drop(2),
                                LoadUnit(3),
                                Return(4)
                            ])
                    ])
            },
            {
                "assign generic method to variable",
                """
                fn MyFn<T>(){}
                var a = MyFn::<string>;
                """,
                Module(
                    methods: [
                        Method("MyFn",
                            typeParameters: ["T"],
                            instructions: [
                                LoadUnit(0),
                                Return(1)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(1)),
                                new LoadGlobalFunction(Addr(2), FunctionDefinitionReference("MyFn", [ConcreteTypeReference("string")])),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                LoadUnit(5),
                                Return(6)
                            ])
                    ])
            },
            {
                "call static type generic function",
                """
                class MyClass {
                    pub static fn MyFn<T>(){}
                }
                MyClass::MyFn::<string>();
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    isStatic: true,
                                    typeParameters: ["T"],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new LoadTypeFunction(Addr(0),
                                    ConcreteTypeReference("MyClass"),
                                    0,
                                    [ConcreteTypeReference("string")]),
                                new Call(Addr(1)),
                                Drop(2),
                                LoadUnit(3),
                                Return(4)
                            ])
                    ])
            },
            {
                "call instance type generic function",
                """
                class MyClass {
                    pub fn MyFn<T>(){}
                }
                var a = new MyClass{};
                a.MyFn::<string>();
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    typeParameters: ["T"],
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadTypeFunction(Addr(3), ConcreteTypeReference("MyClass"), 0, [ConcreteTypeReference("string")]),
                                new Call(Addr(4)),
                                Drop(5),
                                LoadUnit(6),
                                Return(7)
                            ])
                    ])
            },
            {
                "call generic method within type",
                """
                class MyClass {
                    fn MyFn<T>() {}
                    fn OtherFn() {
                        MyFn::<string>();
                    }
                }
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    typeParameters: ["T"],
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ]),
                                Method("OtherFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        new LoadArgument(Addr(0), 0),
                                        new LoadTypeFunction(Addr(1), ConcreteTypeReference("MyClass"), 0, [ConcreteTypeReference("string")]),
                                        new Call(Addr(2)),
                                        Drop(3),
                                        LoadUnit(4),
                                        Return(5)
                                    ])
                            ])
                    ])
            }
        };
    }
}