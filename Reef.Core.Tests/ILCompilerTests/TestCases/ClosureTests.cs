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
                        Class("InnerFn!Closure", variantName: "ClosureVariant", instanceFields:
                        [
                            Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            Field("Field_1", ConcreteTypeReference("int"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("SomeFn", isStatic: true, parameters:
                        [
                            Parameter("param", ConcreteTypeReference("int")),
                            Parameter("param2", ConcreteTypeReference("string")),
                        ], instructions: [LoadUnit(0), Return(1)]),
                        Method("InnerFn",
                            isStatic: false,
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("InnerFn!Closure"))
                            ],
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("string") }
                            ],
                            returnType: ConcreteTypeReference("int"),
                            instructions:
                            [
                                new LoadArgument(new InstructionAddress(0), 0),
                                new LoadField(new InstructionAddress(1), VariantIndex: 0, FieldIndex: 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(new InstructionAddress(3), 0),
                                new LoadField(new InstructionAddress(4), VariantIndex: 0, FieldIndex: 1),
                                new Return(new InstructionAddress(5))
                            ])
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
                        Class("SomeMethod!Closure", variantName: "ClosureVariant", instanceFields:
                        [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            locals: [new ReefMethod.Local() { DisplayName = "a", Type = ConcreteTypeReference("int") }],
                            isStatic: true,
                            instructions: [LoadUnit(0), Return(1)]),
                        Method("SomeMethod", parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeMethod!Closure")),
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
                            ])
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
                        Class("SomeMethod!Closure", variantName: "ClosureVariant", instanceFields:
                        [
                            Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("OuterFn",
                            isStatic: true,
                            parameters:
                            [
                                Parameter("outerParam", ConcreteTypeReference("string"))
                            ],
                            instructions: [LoadUnit(0), Return(1)]),
                        Method("SomeMethod", parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeMethod!Closure")),
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
                            ])
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
                        Class("SomeFn!Closure",
                            variantName: "ClosureVariant",
                            instanceFields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
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
                                Local("a", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadStringConstant(Addr(0), ""),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("SomeFn!Closure")),
                                new CopyStack(Addr(3)),
                                new LoadLocal(Addr(4), 0),
                                new StoreField(Addr(5), 0, 0),
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
                        Class("SomeFn!Closure",
                            variantName: "ClosureVariant",
                            instanceFields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
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
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("SomeFn!Closure")),
                                new CopyStack(Addr(1)),
                                new LoadArgument(Addr(2), 0),
                                new StoreField(Addr(3), 0, 0),
                                new LoadGlobalFunction(Addr(4), FunctionReference("SomeFn")),
                                new Call(Addr(5)),
                                Drop(6),
                                LoadUnit(7),
                                Return(8)
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
                        Class("SomeFn!Closure",
                            variantName: "ClosureVariant",
                            instanceFields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods:
                    [
                        Method("SomeFn",
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
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
                                Local("a", ConcreteTypeReference("string"))
                            ],
                            instructions:
                            [
                                new LoadStringConstant(Addr(0), ""),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("SomeFn!Closure")),
                                new CopyStack(Addr(3)),
                                new LoadLocal(Addr(4), 0),
                                new StoreField(Addr(5), 0, 0),
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
                        Class("Fourth!Closure",
                            variantName: "ClosureVariant",
                            instanceFields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                                Field("Field_1", ConcreteTypeReference("int"), isPublic: true)
                            ]),
                        Class("Third!Closure",
                            variantName: "ClosureVariant",
                            instanceFields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                            ]),
                        Class("Second!Closure",
                            variantName: "ClosureVariant",
                            instanceFields:
                            [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                            ]),
                    ],
                    methods:
                    [
                        Method("Fourth",
                            locals:
                            [
                                Local("b", ConcreteTypeReference("string")),
                                Local("d", ConcreteTypeReference("int")),
                            ],
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("Fourth!Closure"))
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 0),
                                new LoadField(Addr(4), 0, 1),
                                new StoreLocal(Addr(5), 1),
                                LoadUnit(6),
                                Return(7)
                            ]),
                        Method("Third",
                            locals:
                            [
                                Local("c", ConcreteTypeReference("int"))
                            ],
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("Third!Closure"))
                            ],
                            instructions:
                            [
                                new LoadIntConstant(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("Fourth!Closure")),
                                new CopyStack(Addr(3)),
                                new LoadArgument(Addr(4), 0),
                                new LoadField(Addr(5), 0, 0),
                                new StoreField(Addr(6), 0, 0),
                                new CopyStack(Addr(7)),
                                new LoadLocal(Addr(8), 0),
                                new StoreField(Addr(9), 0, 1),
                                new LoadGlobalFunction(Addr(10), FunctionReference("Fourth")),
                                new Call(Addr(11)),
                                Drop(12),
                                LoadUnit(13),
                                Return(14)
                            ]),
                        Method("Second",
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("Second!Closure"))
                            ],
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("Third!Closure")),
                                new CopyStack(Addr(1)),
                                new LoadArgument(Addr(2), 0),
                                new LoadField(Addr(3), 0, 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadGlobalFunction(Addr(5), FunctionReference("Third")),
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
                            instructions:
                            [
                                new CreateObject(Addr(0), ConcreteTypeReference("Second!Closure")),
                                new CopyStack(Addr(1)),
                                new LoadArgument(Addr(2), 0),
                                new StoreField(Addr(3), 0, 0),
                                new LoadGlobalFunction(Addr(4), FunctionReference("Second")),
                                new Call(Addr(5)),
                                Drop(6),
                                LoadUnit(7),
                                Return(8)
                            ]),
                    ])
            },
        };
    }
}