using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable xUnit1026 // Remove unused parameter

public class ClassTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClassAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "empty class",
                "class MyClass{}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                variants: [Variant("_classVariant")])
                        ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                ["T"],
                                variants: [Variant("_classVariant")])
                        ])
            },
            {
                "generic class with instance function",
                "class MyClass<T>{pub fn SomeFn(){}}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                ["T"],
                                [Variant("_classVariant")],
                                [
                                    DataTypeMethod(
                                        "SomeFn",
                                        [],
                                        parameters: [ConcreteTypeReference("MyClass", [GenericPlaceholder("T")])],
                                        returnType: Unit,
                                        expressions: [MethodReturn(UnitConstant(true))])
                                ])
                        ])
            },
            {
                "class with instance fields",
                "class MyClass { pub field MyField: string, pub field OtherField: int}",
                LoweredProgram(types: [
                    DataType("MyClass",
                        variants: [
                            Variant(
                                "_classVariant",
                                [
                                    Field("MyField", StringType),
                                    Field("OtherField", Int),
                                ])
                        ])
                ])
            },
            {
                "class with static fields",
                """class MyClass { pub static field MyField: string = ""}""",
                LoweredProgram(types: [
                    DataType("MyClass",
                        variants: [Variant("_classVariant")],
                        staticFields: [
                            StaticField("MyField", StringType, [StringConstant("", true)])
                        ])
                ])
            }
        };
    }
}
