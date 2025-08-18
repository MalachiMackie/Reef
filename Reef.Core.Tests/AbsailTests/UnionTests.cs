using FluentAssertions;
using Reef.Core.LoweredExpressions;
using Reef.Core.Absail;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbsailTests;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable xUnit1026 // Remove unused parameter

public class UnionTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void UnionAbsailTest(string description, string source, LoweredProgram expectedProgram)
    {
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbsail.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    [Fact]
    public void SingleTest()
    { 
        const string source = "union MyUnion<T>{ A(T) }";
        var expectedProgram = LoweredProgram(types:
        [
            DataType("MyUnion",
                ["T"],
                [
                    Variant("A",
                    [
                        Field("_variantIdentifier", Int),
                        Field("_tupleMember_0", GenericPlaceholder("T"))
                    ])
                ],
                methods:
                [
                    DataTypeMethod(
                        "MyUnion_Create_A",
                        [],
                        [GenericPlaceholder("T")],
                        ConcreteTypeReference("MyUnion", [GenericPlaceholder("T")]),
                        CompilerImplementationType.UnionTupleVariantInit)
                ])
        ]);

        var program = CreateProgram(source);
        var loweredProgram = ProgramAbsail.Lower(program);
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
                        [],
                        [
                            DataTypeMethod(
                                "SomeFn",
                                [],
                                [ConcreteTypeReference("MyUnion", [GenericPlaceholder("T")])],
                                Unit,
                                [MethodReturn(UnitConstant(true))])
                        ])
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
                                Field("_tupleMember_0", StringType),
                                Field("_tupleMember_1", Int),
                            ])
                        ],
                        methods: [
                            DataTypeMethod(
                                "MyUnion_Create_A",
                                [],
                                [StringType, Int],
                                ConcreteTypeReference("MyUnion"),
                                CompilerImplementationType.UnionTupleVariantInit)
                        ])
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
                                    Field("_tupleMember_0", GenericPlaceholder("T"))
                                ])
                        ],
                        methods: [
                            DataTypeMethod(
                                "MyUnion_Create_A",
                                [],
                                [GenericPlaceholder("T")],
                                ConcreteTypeReference("MyUnion", [GenericPlaceholder("T")]),
                                CompilerImplementationType.UnionTupleVariantInit)
                        ])
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
                    DataType("MyUnion",
                        methods: [
                            DataTypeMethod(
                                "MyFn",
                                [],
                                [ConcreteTypeReference("MyUnion")],
                                Unit, 
                                [
                                    MethodReturn(UnitConstant(valueUseful: true)) 
                                ])
                        ])
                ])
            },
            {
                "union with method and tuple variants",
                "union MyUnion { A(string), pub static fn MyFn() {}, B(string) }",
                LoweredProgram(types: [
                    DataType("MyUnion",
                        methods: [
                            DataTypeMethod(
                                "MyFn",
                                [],
                                [],
                                Unit,
                                [
                                    MethodReturn(UnitConstant(true))
                                ]),
                            DataTypeMethod(
                                "MyUnion_Create_A",
                                [],
                                [StringType],
                                ConcreteTypeReference("MyUnion"),
                                CompilerImplementationType.UnionTupleVariantInit),
                            DataTypeMethod(
                                "MyUnion_Create_B",
                                [],
                                [StringType],
                                ConcreteTypeReference("MyUnion"),
                                CompilerImplementationType.UnionTupleVariantInit),
                        ],
                        variants: [
                            Variant(
                                "A",
                                [
                                    Field("_variantIdentifier", Int),
                                    Field("_tupleMember_0", StringType),
                                ]),
                            Variant(
                                "B",
                                [
                                    Field("_variantIdentifier", Int),
                                    Field("_tupleMember_0", StringType),
                                ]),
                        ])
                ])
            }
        };
    }
}
#pragma warning restore IDE0060
