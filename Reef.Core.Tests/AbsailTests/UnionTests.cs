using FluentAssertions;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbsailTests;

#pragma warning disable IDE0060 // Remove unused parameter

public class UnionTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void UnionAbsailTest(string description, string source, LoweredProgram expectedProgram)
    {
        var program = CreateProgram(source);
        var loweredProgram = Absail.Lower(program);
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
                                [],
                                Unit, [
                                    MethodReturn(UnitConstant(valueUseful: true)) 
                                ])
                        ])
                ])
            }
        };
    }
}
#pragma warning restore IDE0060
