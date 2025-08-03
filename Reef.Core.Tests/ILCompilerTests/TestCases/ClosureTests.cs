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
                        ]),
                        Class("InnerFn_Closure", fields: [
                            Field("Field_0", ConcreteTypeReference("SomeFn_Locals"), isPublic: true)
                        ])
                    ],
                    methods:
                    [
                        Method("InnerFn",
                            isStatic: false,
                            parameters:
                            [
                                Parameter("closureParameter", ConcreteTypeReference("InnerFn_Closure"))
                            ],
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("string") }
                            ],
                            returnType: ConcreteTypeReference("int"),
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), VariantIndex: 0, FieldIndex: 1),
                                new StoreLocal(Addr(3), 0),
                                new LoadArgument(Addr(4), 0),
                                new LoadField(Addr(5), 0, 0),
                                new LoadField(Addr(6), VariantIndex: 0, FieldIndex: 0),
                                Return(7)
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
                        ]),
                        Class("SomeMethod_Closure", fields: [
                            Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                        ])
                    ],
                    methods:
                    [
                        
                        Method("SomeMethod", parameters:
                            [
                                Parameter("closureParameter", ConcreteTypeReference("SomeMethod_Closure")),
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
                                new LoadField(Addr(3), 0, 0),
                                new LoadField(Addr(4), VariantIndex: 0, FieldIndex: 0),
                                new StoreLocal(Addr(5), LocalIndex: 1),
                                LoadUnit(6),
                                Return(7)
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
                        ]),
                        Class("SomeMethod_Closure", fields: [
                            Field("Field_0", ConcreteTypeReference("OuterFn_Locals"), isPublic: true)
                        ])
                    ],
                    methods:
                    [
                        Method("SomeMethod", parameters:
                            [
                                Parameter("closureParameter", ConcreteTypeReference("SomeMethod_Closure")),
                            ], locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("string") },
                            ], instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
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
                            ]),
                        Class("SomeFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("closureParameter", ConcreteTypeReference("SomeFn_Closure")),
                            ],
                            locals:
                            [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
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
                                new LoadLocal(Addr(2), 0),
                                new LoadStringConstant(Addr(3), ""),
                                new StoreField(Addr(4), 0, 0),
                                new CreateObject(Addr(5), ConcreteTypeReference("SomeFn_Closure")),
                                new CopyStack(Addr(6)),
                                new LoadLocal(Addr(7), 0),
                                new StoreField(Addr(8), 0, 0),
                                new LoadGlobalFunction(Addr(9), FunctionDefinitionReference("SomeFn")),
                                new Call(Addr(10)),
                                Drop(11),
                                LoadUnit(12),
                                Return(13)
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
                            ]),
                        Class("SomeFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("Outer_Locals"), isPublic: true)
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("closureParameter", ConcreteTypeReference("SomeFn_Closure")),
                            ],
                            locals:
                            [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
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
                                new LoadLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new CreateObject(Addr(5), ConcreteTypeReference("SomeFn_Closure")),
                                new CopyStack(Addr(6)),
                                new LoadLocal(Addr(7), 0),
                                new StoreField(Addr(8), 0, 0),
                                new LoadGlobalFunction(Addr(9), FunctionDefinitionReference("SomeFn")),
                                new Call(Addr(10)),
                                Drop(11),
                                LoadUnit(12),
                                Return(13)
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
                            ]),
                        Class("SomeFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("closureParameter", ConcreteTypeReference("SomeFn_Closure")),
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
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
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
                                new LoadLocal(Addr(2), 0),
                                new LoadStringConstant(Addr(3), ""),
                                new StoreField(Addr(4), 0, 0),
                                new CreateObject(Addr(5), ConcreteTypeReference("SomeFn_Closure")),
                                new CopyStack(Addr(6)),
                                new LoadLocal(Addr(7), 0),
                                new StoreField(Addr(8), 0, 0),
                                new LoadIntConstant(Addr(9), 1),
                                new LoadGlobalFunction(Addr(10), FunctionDefinitionReference("SomeFn")),
                                new Call(Addr(11)),
                                Drop(12),
                                LoadUnit(13),
                                Return(14)
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
                    Class("Fourth_Closure",
                        fields:
                        [
                            Field("Field_0", ConcreteTypeReference("First_Locals"), isPublic: true),
                            Field("Field_1", ConcreteTypeReference("Third_Locals"), isPublic: true)
                        ]),
                    Class("Third_Locals",
                        fields:
                        [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                        ]),
                    Class("Third_Closure",
                        fields:
                        [
                            Field("Field_0", ConcreteTypeReference("First_Locals"), isPublic: true)
                        ]),
                    Class("Second_Closure",
                        fields:
                        [
                            Field("Field_0", ConcreteTypeReference("First_Locals"), isPublic: true)
                        ]),
                    Class("First_Locals",
                        fields:
                        [
                            Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                        ]),
                ],
                methods:
                [
                    Method("Fourth",
                        parameters:
                        [
                            Parameter("closureParameter", ConcreteTypeReference("Fourth_Closure")),
                        ],
                        locals:
                        [
                            Local("b", ConcreteTypeReference("string")),
                            Local("d", ConcreteTypeReference("int"))
                        ],
                        instructions:
                        [
                            new LoadArgument(Addr(0), 0),
                            new LoadField(Addr(1), 0, 0),
                            new LoadField(Addr(2), 0, 0),
                            new StoreLocal(Addr(3), 0),
                            new LoadArgument(Addr(4), 0),
                            new LoadField(Addr(5), 0, 1),
                            new LoadField(Addr(6), 0, 0),
                            new StoreLocal(Addr(7), 1),
                            LoadUnit(8),
                            Return(9)
                        ]),
                    Method("Third",
                        parameters:
                        [
                            Parameter("closureParameter", ConcreteTypeReference("Third_Closure"))
                        ],
                        locals:
                        [
                            Local("locals", ConcreteTypeReference("Third_Locals"))
                        ],
                        instructions:
                        [
                            new CreateObject(Addr(0), ConcreteTypeReference("Third_Locals")),
                            new StoreLocal(Addr(1), 0),
                            new LoadLocal(Addr(2), 0),
                            new LoadIntConstant(Addr(3), 1),
                            new StoreField(Addr(4), 0, 0),
                            new CreateObject(Addr(5), ConcreteTypeReference("Fourth_Closure")),
                            new CopyStack(Addr(6)),
                            new LoadArgument(Addr(7), 0),
                            new LoadField(Addr(8), 0, 0),
                            new StoreField(Addr(9), 0, 0),
                            new CopyStack(Addr(10)),
                            new LoadLocal(Addr(11), 0),
                            new StoreField(Addr(12), 0, 1),
                            new LoadGlobalFunction(Addr(13), FunctionDefinitionReference("Fourth")),
                            new Call(Addr(14)),
                            Drop(15),
                            LoadUnit(16),
                            Return(17)
                        ]),
                    Method("Second",
                        parameters:
                        [
                            Parameter("closureParameter", ConcreteTypeReference("Second_Closure"))
                        ],
                        instructions:
                        [
                            new CreateObject(Addr(0), ConcreteTypeReference("Third_Closure")),
                            new CopyStack(Addr(1)),
                            new LoadArgument(Addr(2), 0),
                            new LoadField(Addr(3), 0, 0),
                            new StoreField(Addr(4), 0, 0),
                            new LoadGlobalFunction(Addr(5), FunctionDefinitionReference("Third")),
                            new Call(Addr(6)),
                            Drop(7),
                            LoadUnit(8),
                            Return(9)
                        ]),
                    Method("First",
                        isStatic: true,
                        parameters:
                        [
                            Parameter("a", ConcreteTypeReference("string"))
                        ],
                        locals:
                        [
                            Local("locals", ConcreteTypeReference("First_Locals"))
                        ],
                        instructions:
                        [
                            new CreateObject(Addr(0), ConcreteTypeReference("First_Locals")),
                            new StoreLocal(Addr(1), 0),
                            new LoadLocal(Addr(2), 0),
                            new LoadArgument(Addr(3), 0),
                            new StoreField(Addr(4), 0, 0),
                            new CreateObject(Addr(5), ConcreteTypeReference("Second_Closure")),
                            new CopyStack(Addr(6)),
                            new LoadLocal(Addr(7), 0),
                            new StoreField(Addr(8), 0, 0),
                            new LoadGlobalFunction(Addr(9), FunctionDefinitionReference("Second")),
                            new Call(Addr(10)),
                            Drop(11),
                            LoadUnit(12),
                            Return(13)
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
                        ]),
                    Class("InnerFn_Closure", fields: [
                        Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                    ])
                ], methods: [
                    Method("InnerFn",
                        parameters: [
                            Parameter("closureParameter", ConcreteTypeReference("InnerFn_Closure"))
                        ],
                        locals: [
                            Local("b", ConcreteTypeReference("int"))
                        ],
                        instructions: [
                            new LoadArgument(Addr(0), 0),
                            new LoadField(Addr(1), 0, 0),
                            new LoadField(Addr(2), 0, 0),
                            new StoreLocal(Addr(3), 0),
                            LoadUnit(4),
                            Return(5)
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
                            new LoadLocal(Addr(2), 0),
                            new LoadIntConstant(Addr(3), 1),
                            new StoreField(Addr(4), 0, 0),
                            new LoadLocal(Addr(5), 0),
                            new LoadField(Addr(6), 0, 0),
                            new StoreLocal(Addr(7), 1),
                            new CreateObject(Addr(8), ConcreteTypeReference("InnerFn_Closure")),
                            new CopyStack(Addr(9)),
                            new LoadLocal(Addr(10), 0),
                            new StoreField(Addr(11), 0, 0),
                            new LoadGlobalFunction(Addr(12), FunctionDefinitionReference("InnerFn")),
                            new Call(Addr(13)),
                            Drop(14),
                            LoadUnit(15),
                            Return(16)
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
                Module(
                    types: [
                        Class("!Main_Locals",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("int"), isPublic: true),
                                Field("Field_1", ConcreteTypeReference("int"), isPublic: true),
                            ]),
                        Class("InnerFn1_Closure", fields: [
                            Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                        ]),
                        Class("InnerFn2_Closure", fields: [
                            Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                        ])
                    ],
                    methods: [
                        Method("InnerFn1",
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("InnerFn1_Closure"))
                            ],
                            locals: [
                                Local("d", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ]),
                        Method("InnerFn2",
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("InnerFn2_Closure"))
                            ],
                            locals: [
                                Local("e", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 1),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
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
                                new LoadLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 1),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadIntConstant(Addr(6), 2),
                                new StoreField(Addr(7), 0, 1),
                                new LoadIntConstant(Addr(8), 3),
                                new StoreLocal(Addr(9), 1),
                                new CreateObject(Addr(10), ConcreteTypeReference("InnerFn1_Closure")),
                                new CopyStack(Addr(11)),
                                new LoadLocal(Addr(12), 0),
                                new StoreField(Addr(13), 0, 0),
                                new LoadGlobalFunction(Addr(14), FunctionDefinitionReference("InnerFn1")),
                                new Call(Addr(15)),
                                Drop(16),
                                new CreateObject(Addr(17), ConcreteTypeReference("InnerFn1_Closure")),
                                new CopyStack(Addr(18)),
                                new LoadLocal(Addr(19), 0),
                                new StoreField(Addr(20), 0, 0),
                                new LoadGlobalFunction(Addr(21), FunctionDefinitionReference("InnerFn1")),
                                new Call(Addr(22)),
                                Drop(23),
                                new CreateObject(Addr(24), ConcreteTypeReference("InnerFn2_Closure")),
                                new CopyStack(Addr(25)),
                                new LoadLocal(Addr(26), 0),
                                new StoreField(Addr(27), 0, 0),
                                new LoadGlobalFunction(Addr(28), FunctionDefinitionReference("InnerFn2")),
                                new Call(Addr(29)),
                                Drop(30),
                                LoadUnit(31),
                                Return(32)
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
                            ]),
                        Class("InnerFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("SomeFn_Locals"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("InnerFn",
                            locals: [
                                Local("b", ConcreteTypeReference("int"))
                            ],
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("InnerFn_Closure"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
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
                                new LoadLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadIntConstant(Addr(5), 2),
                                new StoreLocal(Addr(6), 1),
                                new CreateObject(Addr(7), ConcreteTypeReference("InnerFn_Closure")),
                                new CopyStack(Addr(8)),
                                new LoadLocal(Addr(9), 0),
                                new StoreField(Addr(10), 0, 0),
                                new LoadGlobalFunction(Addr(11), FunctionDefinitionReference("InnerFn")),
                                new Call(Addr(12)),
                                Drop(13),
                                new LoadLocal(Addr(14), 0),
                                new LoadField(Addr(15), 0, 0),
                                new StoreLocal(Addr(16), 2),
                                LoadUnit(17),
                                Return(18)
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
                            ]),
                        Class("Inner1_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("Outer_Locals"), isPublic: true)
                            ]),
                        Class("Inner2_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("Outer_Locals"), isPublic: true),
                                Field("Field_1", ConcreteTypeReference("Inner1_Locals"), isPublic: true),
                            ])
                    ],
                    methods: [
                        Method("Inner2",
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("Inner2_Closure")),
                            ],
                            locals: [
                                Local("aa", ConcreteTypeReference("int")),
                                Local("bb", ConcreteTypeReference("int")),
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                new LoadArgument(Addr(4), 0),
                                new LoadField(Addr(5), 0, 1),
                                new LoadField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 1),
                                LoadUnit(8),
                                Return(9)
                            ]),
                        Method("Inner1",
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("Inner1_Closure")),
                            ],
                            locals: [
                                Local("locals", ConcreteTypeReference("Inner1_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Inner1_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 2),
                                new StoreField(Addr(4), 0, 0),
                                new CreateObject(Addr(5), ConcreteTypeReference("Inner2_Closure")),
                                new CopyStack(Addr(6)),
                                new LoadArgument(Addr(7), 0),
                                new LoadField(Addr(8), 0, 0),
                                new StoreField(Addr(9), 0, 0),
                                new CopyStack(Addr(10)),
                                new LoadLocal(Addr(11), 0),
                                new StoreField(Addr(12), 0, 1),
                                new LoadGlobalFunction(Addr(13), FunctionDefinitionReference("Inner2")),
                                new Call(Addr(14)),
                                Drop(15),
                                LoadUnit(16),
                                Return(17)
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
                                new LoadLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 1),
                                new CreateObject(Addr(8), ConcreteTypeReference("Inner1_Closure")),
                                new CopyStack(Addr(9)),
                                new LoadLocal(Addr(10), 0),
                                new StoreField(Addr(11), 0, 0),
                                new LoadGlobalFunction(Addr(12), FunctionDefinitionReference("Inner1")),
                                new Call(Addr(13)),
                                Drop(14),
                                LoadUnit(15),
                                Return(16)
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
                            ]),
                        Class("InnerFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("InnerFn",
                            locals: [
                                Local("b", ConcreteTypeReference("int"))
                            ],
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("InnerFn_Closure"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                new LoadArgument(Addr(4), 0),
                                new LoadField(Addr(5), 0, 0),
                                new LoadIntConstant(Addr(6), 2),
                                new StoreField(Addr(7), 0, 0),
                                LoadUnit(8),
                                Return(9)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals")),
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 1),
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
                Module(
                    types: [
                        Class("!Main_Locals",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                            ]),
                        Class("Inner_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ]),
                    ],
                    methods: [
                        Method("Inner",
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("Inner_Closure"))
                            ],
                            locals: [
                                Local("b", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals")),
                                Local("c", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 1),
                                new StoreField(Addr(4), 0, 0),
                                new CreateObject(Addr(5), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(6)),
                                new LoadGlobalFunction(Addr(7), FunctionDefinitionReference("Inner")),
                                new StoreField(Addr(8), 0, 0),
                                new CopyStack(Addr(9)),
                                new CreateObject(Addr(10), ConcreteTypeReference("Inner_Closure")),
                                new CopyStack(Addr(11)),
                                new LoadLocal(Addr(12), 0),
                                new StoreField(Addr(13), 0, 0),
                                new StoreField(Addr(14), 0, 1),
                                new StoreLocal(Addr(15), 1),
                                new LoadLocal(Addr(16), 1),
                                new LoadTypeFunction(Addr(17), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0),
                                new Call(Addr(18)),
                                Drop(19),
                                LoadUnit(20),
                                Return(21)
                            ])
                    ])
            },
            {
                "call function variable from closure",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    a();
                }
                OtherFn();
                """,
                Module(
                    types: [
                        Class("!Main_Locals", fields: [
                            Field("Field_0", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), isPublic: true),
                        ]),
                        Class("OtherFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("MyFn", instructions: [
                            LoadUnit(0),
                            Return(1)
                        ]),
                        Method("OtherFn",
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("OtherFn_Closure")),
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new LoadTypeFunction(Addr(3), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0),
                                new Call(Addr(4)),
                                Drop(5),
                                LoadUnit(6),
                                Return(7)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new CreateObject(Addr(3), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(4)),
                                new LoadGlobalFunction(Addr(5), FunctionDefinitionReference("MyFn")),
                                new StoreField(Addr(6), 0, 0),
                                new StoreField(Addr(7), 0, 0),
                                new CreateObject(Addr(8), ConcreteTypeReference("OtherFn_Closure")),
                                new CopyStack(Addr(9)),
                                new LoadLocal(Addr(10), 0),
                                new StoreField(Addr(11), 0, 0),
                                new LoadGlobalFunction(Addr(12), FunctionDefinitionReference("OtherFn")),
                                new Call(Addr(13)),
                                Drop(14),
                                LoadUnit(15),
                                Return(16)
                            ])
                    ])
            },
            {
                "reference function variable from closure",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    var b = a;
                }
                OtherFn();
                """,
                Module(
                    types: [
                        Class("!Main_Locals", fields: [
                            Field("Field_0", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), isPublic: true),
                        ]),
                        Class("OtherFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("MyFn", instructions: [
                            LoadUnit(0),
                            Return(1)
                        ]),
                        Method("OtherFn",
                            locals: [
                                Local("b", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                            ],
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("OtherFn_Closure")),
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new CreateObject(Addr(3), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(4)),
                                new LoadGlobalFunction(Addr(5), FunctionDefinitionReference("MyFn")),
                                new StoreField(Addr(6), 0, 0),
                                new StoreField(Addr(7), 0, 0),
                                new CreateObject(Addr(8), ConcreteTypeReference("OtherFn_Closure")),
                                new CopyStack(Addr(9)),
                                new LoadLocal(Addr(10), 0),
                                new StoreField(Addr(11), 0, 0),
                                new LoadGlobalFunction(Addr(12), FunctionDefinitionReference("OtherFn")),
                                new Call(Addr(13)),
                                Drop(14),
                                LoadUnit(15),
                                Return(16)
                            ])
                    ])
            },
            {
                "reference function variable from closure and call from locals",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    var b = a;
                }
                OtherFn();
                a();
                """,
                Module(
                    types: [
                        Class("!Main_Locals", fields: [
                            Field("Field_0", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), isPublic: true),
                        ]),
                        Class("OtherFn_Closure",
                            fields: [
                                Field("Field_0", ConcreteTypeReference("!Main_Locals"), isPublic: true)
                            ])
                    ],
                    methods: [
                        Method("MyFn", instructions: [
                            LoadUnit(0),
                            Return(1)
                        ]),
                        Method("OtherFn",
                            locals: [
                                Local("b", ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]))
                            ],
                            parameters: [
                                Parameter("closureParameter", ConcreteTypeReference("OtherFn_Closure")),
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new LoadField(Addr(2), 0, 0),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("locals", ConcreteTypeReference("!Main_Locals"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("!Main_Locals")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new CreateObject(Addr(3), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")])),
                                new CopyStack(Addr(4)),
                                new LoadGlobalFunction(Addr(5), FunctionDefinitionReference("MyFn")),
                                new StoreField(Addr(6), 0, 0),
                                new StoreField(Addr(7), 0, 0),
                                new CreateObject(Addr(8), ConcreteTypeReference("OtherFn_Closure")),
                                new CopyStack(Addr(9)),
                                new LoadLocal(Addr(10), 0),
                                new StoreField(Addr(11), 0, 0),
                                new LoadGlobalFunction(Addr(12), FunctionDefinitionReference("OtherFn")),
                                new Call(Addr(13)),
                                Drop(14),
                                new LoadLocal(Addr(15), 0),
                                new LoadField(Addr(16), 0, 0),
                                new LoadTypeFunction(Addr(17), ConcreteTypeReference("Function`1", [ConcreteTypeReference("Unit")]), 0),
                                new Call(Addr(18)),
                                Drop(19),
                                LoadUnit(20),
                                Return(21)
                            ])
                    ])
            }
        };
    }
}