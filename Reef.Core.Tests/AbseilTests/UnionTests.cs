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
        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string _moduleId = "UnionTests";

    [Fact]
    public void SingleTest()
    {
        const string source = 
                "class MyClass<T>{pub fn SomeFn(){}}";
        var expectedProgram = LoweredProgram(
            types:
            [
                DataType(_moduleId, "MyClass",
                    ["T"],
                    [Variant("_classVariant")])
            ], methods:
            [
                Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                    [MethodReturn(UnitConstant(true))],
                    parameters: [ConcreteTypeReference("MyClass", new DefId(_moduleId, $"{_moduleId}.MyClass"), [GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T")])],
                    typeParameters: [(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T")])
            ]);

        var program = CreateProgram(_moduleId, source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "empty union",
                "union MyUnion{}",
                LoweredProgram(types: [
                        DataType(_moduleId, "MyUnion")
                ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                LoweredProgram(types: [
                        DataType(_moduleId, "MyUnion",
                            ["T"])
                ])
            },
            {
                "union with unit variants",
                "union MyUnion{A, B}",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion",
                        variants: [
                            Variant("A", [Field("_variantIdentifier", UInt16_t)]),
                            Variant("B", [Field("_variantIdentifier", UInt16_t)]),
                        ])
                ])
            },
            {
                "generic union with instance function",
                "union MyUnion<T>{pub fn SomeFn(){}}",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion",
                        ["T"],
                        [])
                ], methods: [
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                                [MethodReturn(UnitConstant(true))],
                                parameters: [ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"), [GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")])],
                                typeParameters: [(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")])
                        ])
            },
            {
                "union with tuple variant",
                "union MyUnion { A(string, i64) }",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion",
                        variants: [
                            Variant("A", [
                                Field("_variantIdentifier", UInt16_t),
                                Field("Item0", StringType),
                                Field("Item1", Int64_t),
                            ])
                        ])
                ], methods: [
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                            {"Item0", LoadArgument(0, true, StringType)},
                                            {"Item1", LoadArgument(1, true, Int64_t)},
                                        }))
                                ],
                                parameters: [StringType, Int64_t],
                                returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")))
                        ])
            },
            {
                "generic union with tuple variant",
                "union MyUnion<T>{ A(T) }",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion",
                        ["T"],
                        [
                            Variant("A",
                                [
                                    Field("_variantIdentifier", UInt16_t),
                                    Field("Item0", GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T"))
                                ])
                        ])
                ], methods: [
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"), [GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")]),
                                        "A",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                            {"Item0", LoadArgument(0, true, GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T"))},
                                        }))
                                ],
                                typeParameters: [(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")],
                                parameters: [GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")],
                                returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"), [GenericPlaceholder(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")]))
                        ])
            },
            {
                "union with class variant",
                "union MyUnion { A { field MyField: string, field OtherField: i64 } }",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion",
                        variants: [
                            Variant("A",
                                fields: [
                                    Field("_variantIdentifier", UInt16_t),
                                    Field("MyField", StringType),
                                    Field("OtherField", Int64_t),
                                ])
                        ])
                ])
            },
            {
                "union with method",
                "union MyUnion { pub fn MyFn(){} }",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion")
                ], [
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__MyFn"), "MyUnion__MyFn",
                                [MethodReturn(UnitConstant(valueUseful: true))],
                                parameters: [ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))])
                        ])
            },
            {
                "union with method and tuple variants",
                "union MyUnion { A(string), pub static fn MyFn() {}, B(string) }",
                LoweredProgram(types: [
                    DataType(_moduleId, "MyUnion",
                        variants: [
                            Variant(
                                "A",
                                [
                                    Field("_variantIdentifier", UInt16_t),
                                    Field("Item0", StringType),
                                ]),
                            Variant(
                                "B",
                                [
                                    Field("_variantIdentifier", UInt16_t),
                                    Field("Item0", StringType),
                                ]),
                        ])
                ], methods: [
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__MyFn"), "MyUnion__MyFn",
                                [MethodReturn(UnitConstant(true))]),
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "A",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", UInt16Constant(0, true)},
                                            {"Item0", LoadArgument(0, true, StringType)},
                                        }))
                                ],
                                parameters: [StringType],
                                returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                            Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__B"), "MyUnion__Create__B",
                                [
                                    MethodReturn(CreateObject(
                                        ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion")),
                                        "B",
                                        true,
                                        new()
                                        {
                                            {"_variantIdentifier", UInt16Constant(1, true)},
                                            {"Item0", LoadArgument(0, true, StringType)},
                                        }))
                                ],
                                parameters: [StringType],
                                returnType: ConcreteTypeReference("MyUnion", new DefId(_moduleId, $"{_moduleId}.MyUnion"))),
                        ])
            }
        };
    }
}
