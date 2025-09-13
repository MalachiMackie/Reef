namespace Reef.Core.Tests.AbseilTests;

using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Xunit.Abstractions;
using static Reef.Core.Tests.LoweredProgramHelpers;

public class ObjectInitializationTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{

    [Theory]
    [MemberData(nameof(TestCases))]
    public void ObjectInitializationAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "create empty class",
                "class MyClass{} new MyClass{}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                variants: [
                                    Variant("_classVariant")
                                ])
                        ],
                        methods: [
                            Method("_Main",
                                [
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        false),
                                    MethodReturnUnit()
                                ])
                        ])
            },
            {
                "create class with fields",
                "class MyClass{pub field MyField: string} new MyClass{MyField = \"\"}",
                LoweredProgram(
                        types: [
                            DataType("MyClass",
                                variants: [
                                    Variant("_classVariant", [Field("MyField", StringType)])
                                ])
                        ],
                        methods: [
                            Method("_Main",
                                [
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        false,
                                        new(){{"MyField", StringConstant("", true)}}),
                                    MethodReturnUnit()
                                ])
                        ])
            },
            {
                "Create unit union variant",
                "union MyUnion{A} MyUnion::A",
                LoweredProgram(
                    types: [
                        DataType("MyUnion",
                            variants: [Variant("A", [Field("_variantIdentifier", Int)])])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                CreateObject(
                                    ConcreteTypeReference("MyUnion"),
                                    "A",
                                    false,
                                    new(){{"_variantIdentifier", IntConstant(0, true)}}),
                                MethodReturnUnit()
                            ])
                    ])
            },
            {
                "Create class union variant",
                "union MyUnion{A {field a: string}} new MyUnion::A { a = \"hi\"};",
                LoweredProgram(
                    types: [
                        DataType("MyUnion",
                            variants: [
                                Variant("A", [
                                    Field("_variantIdentifier", Int),
                                    Field("a", StringType),
                                ])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                CreateObject(
                                    ConcreteTypeReference("MyUnion"),
                                    "A",
                                    false,
                                    new()
                                    {
                                        {"_variantIdentifier", IntConstant(0, true)},
                                        {"a", StringConstant("hi", true)},
                                    }),
                                MethodReturnUnit()
                            ])
                    ])
            }
        };
    }
}
