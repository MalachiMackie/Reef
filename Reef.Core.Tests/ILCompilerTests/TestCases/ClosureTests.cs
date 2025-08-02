using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class ClosureTests
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            {
                "fn closure",
                """
                static fn SomeFn(param: int, param2: string) {
                    fn InnerFn(): int {
                        var a = param2;
                        return param;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("SomeFn_Locals", fields: [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true),
                            Field("Field_1", ConcreteTypeReference("string"), isPublic: true)
                        ])
                    ],
                    methods:
                    [
                        Method("InnerFn",
                            isStatic: false,
                            parameters:
                            [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("SomeFn_Locals"))
                            ],
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("string") }
                            ],
                            returnType: ConcreteTypeReference("int"),
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), VariantIndex: 0, FieldIndex: 1),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 0),
                                new LoadField(Addr(4), VariantIndex: 0, FieldIndex: 0),
                                Return(5)
                            ]),
                        Method("SomeFn", isStatic: true, parameters:
                        [
                            Parameter("param", ConcreteTypeReference("int")),
                            Parameter("param2", ConcreteTypeReference("string")),
                        ], instructions: [LoadUnit(0), Return(1)]),
                    ])
            },
            {
                "access parameter in closure",
                """
                var a: int;
                fn SomeMethod(param: string) {
                    var b = param;
                    var c = a;
                }
                """,
                Module(
                    types:
                    [
                        Class("!Main_Locals", fields:
                        [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        
                        Method("SomeMethod", parameters:
                            [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals")),
                                Parameter("param", ConcreteTypeReference("string"))
                            ], locals:
                            [
                                new ReefMethod.Local { DisplayName = "b", Type = ConcreteTypeReference("string") },
                                new ReefMethod.Local { DisplayName = "c", Type = ConcreteTypeReference("int") },
                            ], instructions:
                            [
                                new LoadArgument(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 0),
                                new LoadField(Addr(3), VariantIndex: 0, FieldIndex: 0),
                                new StoreLocal(Addr(4), LocalIndex: 1),
                                LoadUnit(5),
                                Return(6)
                            ]),
                        Method("!Main",
                            locals: [new ReefMethod.Local() { DisplayName = "locals", Type = ConcreteTypeReference("!Main_Locals") }],
                            isStatic: true,
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
                            ]),
                    ])
            },
            {
                "access outer parameter in closure",
                """
                static fn OuterFn(outerParam: string) {
                    fn SomeMethod() {
                        var a = outerParam;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("OuterFn_Locals", fields:
                        [
                            Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("SomeMethod", parameters:
                            [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("OuterFn_Locals")),
                            ], locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("string") },
                            ], instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("OuterFn",
                            isStatic: true,
                            parameters:
                            [
                                Parameter("outerParam", ConcreteTypeReference("string"))
                            ],
                            instructions: [LoadUnit(0), Return(1)]),
                    ])
            },
            {
                "call closure",
                """
                var a = "";
                SomeFn();

                fn SomeFn() {
                    var b = a;
                }
                """,
                Module(
                    types:
                    [
                        Class("!Main_Locals",
                            fields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals")),
                            ],
                            locals:
                            [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("locals", ConcreteTypeReference("!Main_Locals"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadStringConstant(Addr(2), ""),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadGlobalFunction(Addr(6), FunctionReference("SomeFn")),
                                new Call(Addr(7)),
                                Drop(8),
                                LoadUnit(9),
                                Return(10)
                            ])
                    ])
            },
            {
                "call closure that references parameter",
                """
                static fn Outer(a: string) {
                    fn SomeFn() {
                        var b = a;
                    }
                    SomeFn();
                }
                """,
                Module(
                    types:
                    [
                        Class("Outer_Locals",
                            fields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("Outer_Locals")),
                            ],
                            locals:
                            [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("Outer",
                            isStatic: true,
                            parameters:
                            [
                                Parameter("a", ConcreteTypeReference("string"))
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("Outer_Locals"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("Outer_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 0),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadGlobalFunction(Addr(6), FunctionReference("SomeFn")),
                                new Call(Addr(7)),
                                Drop(8),
                                LoadUnit(9),
                                Return(10)
                            ])
                    ])
            },
            {
                "call closure with parameter",
                """
                var a = "";
                SomeFn(1);

                fn SomeFn(c: int) {
                    var b = a;
                }
                """,
                Module(
                    types:
                    [
                        Class("!Main_Locals",
                            fields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals")),
                                Parameter("c", ConcreteTypeReference("int"))
                            ],
                            locals:
                            [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                Local("locals", ConcreteTypeReference("!Main_Locals"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadStringConstant(Addr(2), ""),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadIntConstant(Addr(6), 1),
                                new LoadGlobalFunction(Addr(7), FunctionReference("SomeFn")),
                                new Call(Addr(8)),
                                Drop(9),
                                LoadUnit(10),
                                Return(11)
                            ])
                    ])
            },
            {
                "closure references two functions out",
                """
                static fn First(a: string) {
                    fn Second() {
                        fn Third() {
                            var c = 1;
                            fn Fourth() {
                                var b = a;
                                var d = c;
                            }
                            
                            Fourth();
                        }
                        Third();
                    }
                    Second();
                }
                """,
                Module(
                    types:
                    [
                        Class("Third_Locals",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                            ]),
                        Class("First_Locals",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                            ])
                    ],
                    methods:
                    [
                        Method("Fourth",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("First_Locals")),
                                Parameter("ClosureParameter_1", ConcreteTypeReference("Third_Locals"))
                            ],
                            locals: [
                                Local("b", ConcreteTypeReference("string")),
                                Local("d", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 1),
                                new LoadField(Addr(4), 0, 0),
                                new StoreLocal(Addr(5), 1),
                                LoadUnit(6),
                                Return(7)
                            ]),
                        Method("Third",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("First_Locals"))
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("Third_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Third_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadIntConstant(Addr(2), 1),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadArgument(Addr(5), 0),
                                new LoadLocal(Addr(6), 0),
                                new LoadGlobalFunction(Addr(7), FunctionReference("Fourth")),
                                new Call(Addr(8)),
                                Drop(9),
                                LoadUnit(10),
                                Return(11)
                            ]),
                        Method("Second",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("First_Locals"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadGlobalFunction(Addr(1), FunctionReference("Third")),
                                new Call(Addr(2)),
                                Drop(3),
                                LoadUnit(4),
                                Return(5)
                            ]),
                        Method("First",
                            isStatic: true,
                            parameters: [
                                Parameter("a", ConcreteTypeReference("string"))
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("First_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("First_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 0),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadGlobalFunction(Addr(6), FunctionReference("Second")),
                                new Call(Addr(7)),
                                Drop(8),
                                LoadUnit(9),
                                Return(10)
                            ])
                    ])
            },
            {
                "accessing variable in and out of closure",
                """
                var a = 1;
                var c = a;
                InnerFn();
                fn InnerFn() {
                    var b = a;
                }
                """,
                Module(types: [
                    Class("!Main_Locals",
                        fields: [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                        ])
                ], methods: [
                    Method("InnerFn",
                        parameters: [
                            Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals"))
                        ],
                        locals: [
                            Local("b", ConcreteTypeReference("int"))
                        ],
                        instructions: [
                            new LoadArgument(Addr(0), 0),
                            new LoadField(Addr(1), 0, 0),
                            new StoreLocal(Addr(2), 0),
                            LoadUnit(3),
                            Return(4)
                        ]),
                    Method("!Main",
                        isStatic: true,
                        locals: [
                            Local("locals", ConcreteTypeReference("!Main_Locals")),
                            Local("c", ConcreteTypeReference("int"))
                        ],
                        instructions: [
                            new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                            new StoreLocal(Addr(1), 0),
                            new LoadIntConstant(Addr(2), 1),
                            new LoadLocal(Addr(3), 0),
                            new StoreField(Addr(4), 0, 0),
                            new LoadLocal(Addr(5), 0),
                            new LoadField(Addr(6), 0, 0),
                            new StoreLocal(Addr(7), 1),
                            new LoadLocal(Addr(8), 0),
                            new LoadGlobalFunction(Addr(9), FunctionReference("InnerFn")),
                            new Call(Addr(10)),
                            Drop(11),
                            LoadUnit(12),
                            Return(13)
                        ])
                ])
            },
            {
                "multiple closures",
                """
                var a = 1;
                var b = 2;
                var c = 3;
                
                InnerFn1();
                InnerFn1();
                InnerFn2();
                
                fn InnerFn1() {
                    var d = a;
                }
                fn InnerFn2() {
                    var e = b;
                }
                """,
                Module(types: [
                    Class("!Main_Locals",
                        fields: [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true),
                            Field("Field_1", ConcreteTypeReference("int"), isPublic: true),
                        ])
                    ],
                    methods: [
                        Method("InnerFn1",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals"))
                            ],
                            locals: [
                                Local("d", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("InnerFn2",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals"))
                            ],
                            locals: [
                                Local("e", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 1),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals")),
                                Local("c", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadIntConstant(Addr(2), 1),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadIntConstant(Addr(5), 2),
                                new LoadLocal(Addr(6), 0),
                                new StoreField(Addr(7), 0, 1),
                                new LoadIntConstant(Addr(8), 3),
                                new StoreLocal(Addr(9), 1),
                                new LoadLocal(Addr(10), 0),
                                new LoadGlobalFunction(Addr(11), FunctionReference("InnerFn1")),
                                new Call(Addr(12)),
                                Drop(13),
                                new LoadLocal(Addr(14), 0),
                                new LoadGlobalFunction(Addr(15), FunctionReference("InnerFn1")),
                                new Call(Addr(16)),
                                Drop(17),
                                new LoadLocal(Addr(18), 0),
                                new LoadGlobalFunction(Addr(19), FunctionReference("InnerFn2")),
                                new Call(Addr(20)),
                                Drop(21),
                                LoadUnit(22),
                                Return(23)
                            ])
                    ])
            },
            {
                "parameter referenced in closure",
                """
                static fn SomeFn(a: int) { 
                    fn InnerFn() {
                        var b = a;
                    }
                    var c = 2;
                    InnerFn();
                    var d = a;
                }
                """,
                Module(
                    types: [
                        Class("SomeFn_Locals",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("InnerFn",
                            locals: [
                                Local("b", ConcreteTypeReference("int"))
                            ],
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("SomeFn_Locals"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("SomeFn",
                            isStatic: true,
                            parameters: [
                                Parameter("a", ConcreteTypeReference("int")),
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("SomeFn_Locals")),
                                Local("c", ConcreteTypeReference("int")),
                                Local("d", ConcreteTypeReference("int")),
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("SomeFn_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 0),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadIntConstant(Addr(5), 2),
                                new StoreLocal(Addr(6), 1),
                                new LoadLocal(Addr(7), 0),
                                new LoadGlobalFunction(Addr(8), FunctionReference("InnerFn")),
                                new Call(Addr(9)),
                                Drop(10),
                                new LoadLocal(Addr(11), 0),
                                new LoadField(Addr(12), 0, 0),
                                new StoreLocal(Addr(13), 2),
                                LoadUnit(14),
                                Return(15)
                            ])
                    ])
            },
            {
                "closure references variables from multiple functions",
                """
                static fn Outer(a: int) {
                    var d = a;
                    Inner1();
                    fn Inner1() {
                        var b = 2;
                        Inner2();
                        
                        fn Inner2() {
                            var aa = a;
                            var bb = b;
                        }
                    }
                }
                """,
                Module(
                    types: [
                        Class("Outer_Locals",
                            fields: [
                                Field("Field_0", isPublic: true, type: ConcreteTypeReference("int"))
                            ]),
                        Class("Inner1_Locals",
                            fields: [
                                Field("Field_0", isPublic: true, type: ConcreteTypeReference("int"))
                            ])
                    ],
                    methods: [
                        Method("Inner2",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("Outer_Locals")),
                                Parameter("ClosureParameter_1", ConcreteTypeReference("Inner1_Locals")),
                            ],
                            locals: [
                                Local("aa", ConcreteTypeReference("int")),
                                Local("bb", ConcreteTypeReference("int")),
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 1),
                                new LoadField(Addr(4), 0, 0),
                                new StoreLocal(Addr(5), 1),
                                LoadUnit(6),
                                Return(7)
                            ]),
                        Method("Inner1",
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("Outer_Locals")),
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("Inner1_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Inner1_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadIntConstant(Addr(2), 2),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadArgument(Addr(5), 0),
                                new LoadLocal(Addr(6), 0),
                                new LoadGlobalFunction(Addr(7), FunctionReference("Inner2")),
                                new Call(Addr(8)),
                                Drop(9),
                                LoadUnit(10),
                                Return(11)
                            ]),
                        Method("Outer",
                            isStatic: true,
                            parameters: [
                                Parameter("a", ConcreteTypeReference("int")),
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("Outer_Locals")),
                                Local("d", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Outer_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 0),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 1),
                                new LoadLocal(Addr(8), 0),
                                new LoadGlobalFunction(Addr(9), FunctionReference("Inner1")),
                                new Call(Addr(10)),
                                Drop(11),
                                LoadUnit(12),
                                Return(13)
                            ])
                    ])
            },
            {
                "mutating referenced value",
                """
                var mut a = 1;
                fn InnerFn() {
                    var b = a;
                    a = 2;
                }
                """,
                Module(
                    types: [
                        Class("!Main_Locals",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("InnerFn",
                            locals: [
                                Local("b", ConcreteTypeReference("int"))
                            ],
                            parameters: [
                                Parameter("ClosureParameter_0", ConcreteTypeReference("!Main_Locals"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 2),
                                new LoadArgument(Addr(4), 0),
                                new StoreField(Addr(5), 0, 0),
                                LoadUnit(6),
                                Return(7)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals")),
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadIntConstant(Addr(2), 1),
                                new LoadLocal(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                LoadUnit(5),
                                Return(6)
                            ])
                    ])
            },
            {
                "assign closure to variable",
                """
                var a = 1;
                fn Inner() {
                    var b = a;
                }
                var c = Inner;
                c();
                """,
                Module()
            }
        };
    }
}