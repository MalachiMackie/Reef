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
        };
    }
}