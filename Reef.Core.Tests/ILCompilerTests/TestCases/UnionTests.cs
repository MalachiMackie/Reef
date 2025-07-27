using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class UnionTests
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "Create union unit variant",
                """
                union MyUnion {
                    A
                }
                var a = MyUnion::A
                """,
                Module(
                    types: [
                        Union(
                        "MyUnion",
                        variants: [
                            Variant(
                                "A",
                                fields: [
                                    Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true)
                                ])
                        ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyUnion")),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 0),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                LoadUnit(5),
                                Return(6)
                            ])
                    ])
            },
            {
                "Create union second unit variant",
                """
                union MyUnion {
                    A,
                    B
                }
                var a = MyUnion::B
                """,
                Module(
                    types: [
                        Union(
                            "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    fields: [
                                        Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true)
                                    ]),
                                Variant(
                                    "B",
                                    fields: [
                                        Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true)
                                    ]),
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyUnion")),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreField(Addr(3), 1, 0),
                                new StoreLocal(Addr(4), 0),
                                LoadUnit(5),
                                Return(6)
                            ])
                    ])
            },
            {
                "create union tuple variant",
                """
                union MyUnion {
                    A,
                    B(string),
                    C(int, string),
                    
                    pub static fn SomeFn(){}
                }
                
                var a = MyUnion::C(1, "");
                """,
                Module(
                    types: [
                        Union("MyUnion",
                            variants: [
                                Variant("A", fields: [Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true)]),
                                Variant("B", fields: [
                                    Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                                    Field("First", ConcreteTypeReference("string"), isPublic: true),
                                ]),
                                Variant("C", fields: [
                                    Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                                    Field("First", ConcreteTypeReference("int"), isPublic: true),
                                    Field("Second", ConcreteTypeReference("string"), isPublic: true),
                                ]),
                            ],
                            methods: [
                                Method("SomeFn",
                                    isStatic: true,
                                    instructions: [LoadUnit(0), Return(1)]),
                                Method("MyUnion_B_Create",
                                    isStatic: true,
                                    parameters: [
                                        Parameter("First", ConcreteTypeReference("string")),
                                    ],
                                    returnType: ConcreteTypeReference("MyUnion"),
                                    instructions: [
                                        new CreateObject(Addr(0), ConcreteTypeReference("MyUnion")),
                                        new CopyStack(Addr(1)),
                                        new LoadIntConstant(Addr(2), 1),
                                        new StoreField(Addr(3), 1, 0),
                                        new CopyStack(Addr(4)),
                                        new LoadArgument(Addr(5), 0),
                                        new StoreField(Addr(6), 1, 1),
                                        Return(7)
                                    ]),
                                Method("MyUnion_C_Create",
                                    isStatic: true,
                                    parameters: [
                                        Parameter("First", ConcreteTypeReference("int")),
                                        Parameter("Second", ConcreteTypeReference("string")),
                                    ],
                                    returnType: ConcreteTypeReference("MyUnion"),
                                    instructions: [
                                        new CreateObject(Addr(0), ConcreteTypeReference("MyUnion")),
                                        new CopyStack(Addr(1)),
                                        new LoadIntConstant(Addr(2), 2),
                                        new StoreField(Addr(3), 2, 0),
                                        new CopyStack(Addr(4)),
                                        new LoadArgument(Addr(5), 0),
                                        new StoreField(Addr(6), 2, 1),
                                        new CopyStack(Addr(7)),
                                        new LoadArgument(Addr(8), 1),
                                        new StoreField(Addr(9), 2, 2),
                                        Return(10)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyUnion"))
                            ],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadStringConstant(Addr(1), ""),
                                new LoadTypeFunction(Addr(2), ConcreteTypeReference("MyUnion"), 2),
                                new Call(Addr(3)),
                                new StoreLocal(Addr(4), 0),
                                LoadUnit(5),
                                Return(6)
                            ])
                    ])
            }
        };
    }
}