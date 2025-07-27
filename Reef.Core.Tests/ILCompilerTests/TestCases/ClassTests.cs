using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;
using static TestHelpers;

public static class ClassTests
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "access this in instance method",
                """
                class MyClass {
                    fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("MyClass", methods:
                        [
                            Method("SomeFn",
                                parameters:
                                [
                                    Parameter("this", ConcreteTypeReference("MyClass"))
                                ],
                                locals:
                                [
                                    new ReefMethod.Local { Type = ConcreteTypeReference("MyClass"), DisplayName = "a" },
                                ],
                                instructions:
                                [
                                    new LoadArgument(new InstructionAddress(0), 0),
                                    new StoreLocal(new InstructionAddress(1), 0),
                                    LoadUnit(2),
                                    Return(3)
                                ]
                            )
                        ])
                    ])
            },
            {
                "access instance field via variable in instance method",
                """
                class MyClass {
                    field SomeField: string,

                    fn SomeFn() {
                        var a = SomeField;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            fields:
                            [
                                Field("SomeField", ConcreteTypeReference("string"))
                            ],
                            methods:
                            [
                                Method("SomeFn",
                                    parameters:
                                    [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    locals:
                                    [
                                        new ReefMethod.Local
                                            { Type = ConcreteTypeReference("string"), DisplayName = "a" },
                                    ],
                                    instructions:
                                    [
                                        new LoadArgument(new InstructionAddress(0), 0),
                                        new LoadField(new InstructionAddress(1), 0, 0),
                                        new StoreLocal(new InstructionAddress(2), 0),
                                        LoadUnit(3),
                                        Return(4)
                                    ]
                                )
                            ])
                    ])
            },
            {
                "access static field via variable in method",
                """
                class MyClass {
                    static field SomeField: string = "",

                    static fn SomeFn() {
                        var a = SomeField;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            fields:
                            [
                                Field("SomeField", ConcreteTypeReference("string"), isStatic: true,
                                    staticInitializer: [new LoadStringConstant(new InstructionAddress(0), "")])
                            ],
                            methods:
                            [
                                Method("SomeFn",
                                    parameters: [],
                                    locals:
                                    [
                                        new ReefMethod.Local
                                            { Type = ConcreteTypeReference("string"), DisplayName = "a" },
                                    ],
                                    isStatic: true,
                                    instructions:
                                    [
                                        new LoadStaticField(new InstructionAddress(0), ConcreteTypeReference("MyClass"),
                                            0, 0),
                                        new StoreLocal(new InstructionAddress(1), 0),
                                        LoadUnit(2),
                                        Return(3)
                                    ]
                                )
                            ])
                    ])
            },
            {
                "access parameter in instance method",
                """
                class MyClass {
                    fn SomeFn(param: int) {
                        var a = param;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            methods:
                            [
                                Method("SomeFn",
                                    parameters:
                                    [
                                        Parameter("this", ConcreteTypeReference("MyClass")),
                                        Parameter("param", ConcreteTypeReference("int")),
                                    ],
                                    locals:
                                    [
                                        new ReefMethod.Local { Type = ConcreteTypeReference("int"), DisplayName = "a" },
                                    ],
                                    instructions:
                                    [
                                        new LoadArgument(new InstructionAddress(0), 1),
                                        new StoreLocal(new InstructionAddress(1), 0),
                                        LoadUnit(2),
                                        Return(3)
                                    ]
                                )
                            ])
                    ])
            },


            {
                "create object without fields",
                """
                class MyClass{}
                var a = new MyClass{};
                """,
                Module(
                    types:
                    [
                        Class("MyClass")
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
                            ])
                    ])
            },
            {
                "create object with fields",
                """
                class MyClass{ pub field Field1: int, pub field Field2: string}
                var a = new MyClass{Field2 = "", Field1 = 2};
                """,
                Module(
                    types:
                    [
                        Class("MyClass", fields:
                        [
                            Field("Field1", ConcreteTypeReference("int"), isPublic: true),
                            Field("Field2", ConcreteTypeReference("string"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new CopyStack(Addr(1)),
                                new LoadStringConstant(Addr(2), ""),
                                new StoreField(Addr(3), 0, 1),
                                new CopyStack(Addr(4)),
                                new LoadIntConstant(Addr(5), 2),
                                new StoreField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 0),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "call instance method",
                """
                class MyClass {
                    pub fn SomeFn(){}
                }
                var a = new MyClass {};
                a.SomeFn(); 
                """,
                Module(
                    types:
                    [
                        Class(
                            "MyClass",
                            methods:
                            [
                                Method("SomeFn",
                                    parameters:
                                    [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions:
                                    [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadTypeFunction(Addr(3), ConcreteTypeReference("MyClass"), 0),
                                new Call(Addr(4)),
                                Drop(5),
                                LoadUnit(6),
                                Return(7)
                            ])
                    ])
            },
            {
                "call instance method with parameters",
                """
                class MyClass {
                    pub fn SomeFn(a: int, b: string){}
                }
                var a = new MyClass {};
                a.SomeFn(1, ""); 
                """,
                Module(
                    types:
                    [
                        Class(
                            "MyClass",
                            methods:
                            [
                                Method("SomeFn",
                                    parameters:
                                    [
                                        Parameter("this", ConcreteTypeReference("MyClass")),
                                        Parameter("a", ConcreteTypeReference("int")),
                                        Parameter("b", ConcreteTypeReference("string")),
                                    ],
                                    instructions:
                                    [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 1),
                                new LoadStringConstant(Addr(4), ""),
                                new LoadTypeFunction(Addr(5), ConcreteTypeReference("MyClass"), 0),
                                new Call(Addr(6)),
                                Drop(7),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "call static class method",
                """
                class MyClass {
                    pub static fn MyFn(a: int) {
                    }
                }
                MyClass::MyFn(1);
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            methods:
                            [
                                Method("MyFn",
                                    isStatic: true,
                                    parameters: [Parameter("a", ConcreteTypeReference("int"))],
                                    instructions: [LoadUnit(0), Return(1)])
                            ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            instructions:
                            [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadTypeFunction(Addr(1), ConcreteTypeReference("MyClass"), 0),
                                new Call(Addr(2)),
                                Drop(3),
                                LoadUnit(4),
                                Return(5)
                            ])
                    ])
            },
            {
                "get static field",
                """
                class MyClass { pub static field A: int = 1 }
                var a = MyClass::A;
                """,
                Module(
                    types:
                    [
                        Class("MyClass", fields:
                        [
                            Field("A",
                                isStatic: true,
                                isPublic: true,
                                type: ConcreteTypeReference("int"),
                                staticInitializer: [new LoadIntConstant(Addr(0), 1)])
                        ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions:
                            [
                                new LoadStaticField(Addr(0), ConcreteTypeReference("MyClass"), VariantIndex: 0,
                                    FieldIndex: 0),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
                            ])
                    ])
            },
            {
                "get instance field",
                """
                class MyClass { pub field MyField: int }
                var a = new MyClass { MyField = 1 };
                var b = a.MyField;
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            fields: [Field("MyField", isPublic: true, type: ConcreteTypeReference("int"))])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("int")),
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 1),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "get instance and static field",
                """
                class MyClass { pub field Ignore: int, pub field InstanceField: string, pub static field StaticField: int = 2 }
                var a = new MyClass { Ignore = 1, InstanceField = "" };
                var b = a.InstanceField;
                var c = MyClass::StaticField;
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            fields: [
                                Field("Ignore", isPublic: true, type: ConcreteTypeReference("int")),
                                Field("InstanceField", isPublic: true, type: ConcreteTypeReference("string")),
                                Field("StaticField", isPublic: true, isStatic: true, type: ConcreteTypeReference("int"), staticInitializer: [new LoadIntConstant(Addr(0), 2)])
                            ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("string")),
                                Local("c", ConcreteTypeReference("int")),
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreField(Addr(3), 0, 0),
                                new CopyStack(Addr(4)),
                                new LoadStringConstant(Addr(5), ""),
                                new StoreField(Addr(6), 0, 1),
                                new StoreLocal(Addr(7), 0),
                                new LoadLocal(Addr(8), 0),
                                new LoadField(Addr(9), 0, 1),
                                new StoreLocal(Addr(10), 1),
                                new LoadStaticField(Addr(11), ConcreteTypeReference("MyClass"), 0, 2),
                                new StoreLocal(Addr(12), 2),
                                LoadUnit(13),
                                Return(14)
                            ])
                    ])
            }
        };
    }
}