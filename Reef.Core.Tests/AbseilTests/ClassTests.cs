using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ClassTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClassAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
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
                                [Variant("_classVariant")])
                        ], methods: [
                                    Method(
                                        "MyClass__SomeFn",
                                        [MethodReturn(UnitConstant(true))],
                                        parameters: [ConcreteTypeReference("MyClass", [GenericPlaceholder("T")])])
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
                            StaticField("MyField", StringType, StringConstant("", true))
                        ])
                ])
            },
            {
                "access class field",
                """
                class MyClass {pub field A: string}
                var a = new MyClass{A = ""};
                var b = a.A;
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant", [Field("A", StringType)])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        valueUseful: true,
                                        new(){{"A", StringConstant("", valueUseful: true)}}),
                                    valueUseful: false),
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        LocalAccess("a", true, ConcreteTypeReference("MyClass")),
                                        "A",
                                        valueUseful: true,
                                        resolvedType: StringType),
                                    valueUseful: false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", StringType),
                            ])
                    ])
            }
        };
    }
}
