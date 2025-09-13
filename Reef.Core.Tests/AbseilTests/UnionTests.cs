using FluentAssertions;
using Reef.Core.LoweredExpressions;
using Reef.Core.Abseil;

using static Reef.Core.Tests.LoweredProgramHelpers;
using Xunit.Abstractions;

namespace Reef.Core.Tests.AbseilTests;

public class UnionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void UnionAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    [Fact]
    public void SingleTest()
    {
        const string source = 
                "class MyClass<T>{pub fn SomeFn(){}}";
        var expectedProgram = LoweredProgram(
            types:
            [
                DataType("MyClass",
                    ["T"],
                    [Variant("_classVariant")])
            ], methods:
            [
                Method(
                    "MyClass__SomeFn",
                    [MethodReturn(UnitConstant(true))],
                    parameters: [ConcreteTypeReference("MyClass", [GenericPlaceholder("T")])],
                    typeParameters: ["T"])
            ]);

        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "empty union",
                "union MyUnion{}",
                LoweredProgram(types: [
                        DataType(
                            "MyUnion")
                ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                LoweredProgram(types: [
                        DataType(
                            "MyUnion",
                            ["T"])
                ])
            },
            {
                "union with unit variants",
                "union MyUnion{A, B}",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        variants: [
                            Variant("A", [Field("_variantIdentifier", Int)]),
                            Variant("B", [Field("_variantIdentifier", Int)]),
                        ])
                ])
            },
            {
                "generic union with instance function",
                "union MyUnion<T>{pub fn SomeFn(){}}",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        ["T"],
                        [])
                ], methods: [
                            Method(
                                "MyUnion__SomeFn",
                                [MethodReturn(UnitConstant(true))],
                                parameters: [ConcreteTypeReference("MyUnion", [GenericPlaceholder("T")])],
                                typeParameters: ["T"])
                        ])
            },
            {
                "union with tuple variant",
                "union MyUnion { A(string, int) }",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        variants: [
                            Variant("A", [
                                Field("_variantIdentifier", Int),
                                Field("Item0", StringType),
                                Field("Item1", Int),
                            ])
                        ])
                ], methods: [
                            Method(
                                "MyUnion_Create_A",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion"),
                                        "A",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", IntConstant(0, true)},
                                            {"Item0", LoadArgument(0, true, StringType)},
                                            {"Item1", LoadArgument(1, true, Int)},
                                        }))
                                ],
                                parameters: [StringType, Int],
                                returnType: ConcreteTypeReference("MyUnion"))
                        ])
            },
            {
                "generic union with tuple variant",
                "union MyUnion<T>{ A(T) }",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        ["T"],
                        [
                            Variant("A",
                                [
                                    Field("_variantIdentifier", Int),
                                    Field("Item0", GenericPlaceholder("T"))
                                ])
                        ])
                ], methods: [
                            Method(
                                "MyUnion_Create_A",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion", [GenericPlaceholder("T")]),
                                        "A",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", IntConstant(0, true)},
                                            {"Item0", LoadArgument(0, true, GenericPlaceholder("T"))},
                                        }))
                                ],
                                typeParameters: ["T"],
                                parameters: [GenericPlaceholder("T")],
                                returnType: ConcreteTypeReference("MyUnion", [GenericPlaceholder("T")]))
                        ])
            },
            {
                "union with class variant",
                "union MyUnion { A { field MyField: string, field OtherField: int } }",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        variants: [
                            Variant("A",
                                fields: [
                                    Field("_variantIdentifier", Int),
                                    Field("MyField", StringType),
                                    Field("OtherField", Int),
                                ])
                        ])
                ])
            },
            {
                "union with method",
                "union MyUnion { pub fn MyFn(){} }",
                LoweredProgram(types: [
                    DataType("MyUnion")
                ], [
                            Method(
                                "MyUnion__MyFn",
                                [MethodReturn(UnitConstant(valueUseful: true))],
                                parameters: [ConcreteTypeReference("MyUnion")])
                        ])
            },
            {
                "union with method and tuple variants",
                "union MyUnion { A(string), pub static fn MyFn() {}, B(string) }",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        variants: [
                            Variant(
                                "A",
                                [
                                    Field("_variantIdentifier", Int),
                                    Field("Item0", StringType),
                                ]),
                            Variant(
                                "B",
                                [
                                    Field("_variantIdentifier", Int),
                                    Field("Item0", StringType),
                                ]),
                        ])
                ], methods: [
                            Method(
                                "MyUnion__MyFn",
                                [MethodReturn(UnitConstant(true))]),
                            Method(
                                "MyUnion_Create_A",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion"),
                                        "A",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", IntConstant(0, true)},
                                            {"Item0", LoadArgument(0, true, StringType)},
                                        }))
                                ],
                                parameters: [StringType],
                                returnType: ConcreteTypeReference("MyUnion")),
                            Method(
                                "MyUnion_Create_B",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion"),
                                        "B",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", IntConstant(1, true)},
                                            {"Item0", LoadArgument(0, true, StringType)},
                                        }))
                                ],
                                parameters: [StringType],
                                returnType: ConcreteTypeReference("MyUnion")),
                        ])
            }
        };
    }
}
