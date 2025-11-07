using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;

using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class UnionTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void UnionAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "UnionTests";

    [Fact]
    public void SingleTest()
    {
        // const string source = 
        //         "class MyClass<T>{pub fn SomeFn(){}}";
        // var expectedProgram = NewLoweredProgram(
        //     types:
        //     [
        //         DataType(ModuleId, "MyClass",
        //             ["T"],
        //             [Variant("_classVariant")])
        //     ], methods:
        //     [
        //         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
        //             [MethodReturn(UnitConstant(true))],
        //             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [GenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])],
        //             typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
        //     ]);
        //
        // var program = CreateProgram(ModuleId, source);
        // var loweredProgram = ProgramAbseil.Lower(program);
        // loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
    {
        return new()
        {
            {
                "empty union",
                "union MyUnion{}",
                NewLoweredProgram(types: [
                    NewDataType(ModuleId, "MyUnion")
                ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                NewLoweredProgram(types: [
                        NewDataType(ModuleId, "MyUnion",
                            ["T"])
                ])
            },
            {
                "union with unit variants",
                "union MyUnion{A, B}",
                NewLoweredProgram(types: [
                    NewDataType(ModuleId, "MyUnion",
                        variants: [
                            NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                            NewVariant("B", [NewField("_variantIdentifier", UInt16T)]),
                        ])
                ])
            },
            {
                "generic union with instance function",
                "union MyUnion<T>{pub fn SomeFn(){}}",
                NewLoweredProgram(types: [
                    NewDataType(ModuleId, "MyUnion",
                        ["T"])
                ], methods: [
                    NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                        [
                            new BasicBlock(new BasicBlockId("bb0"), [])
                            {
                                Terminator = new Return()
                            }
                        ],
                        Unit,
                        parameters: [
                            (
                                "this",
                                new NewLoweredConcreteTypeReference(
                                    "MyUnion",
                                    new DefId(ModuleId, $"{ModuleId}.MyUnion"),
                                    [new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")])
                            )],
                        typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")])
                ])
            },
            {
                "union with tuple variant",
                "union MyUnion { A(string, i64) }",
                NewLoweredProgram(types: [
                    NewDataType(ModuleId, "MyUnion",
                        variants: [
                            NewVariant("A", [
                                NewField("_variantIdentifier", UInt16T),
                                NewField("Item0", StringT),
                                NewField("Item1", Int64T),
                            ])
                        ])
                ], methods: [
                    NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                        [
                            new BasicBlock(new BasicBlockId("bb0"), [
                                new Assign(
                                    new Local("_returnValue"),
                                    new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new Assign(
                                    new Field("_returnValue", "_variantIdentifier", "A"),
                                    new Use(new UIntConstant(0, 2))),
                                new Assign(
                                    new Field("_returnValue", "Item0", "A"),
                                    new Use(new Copy(new Local("_param0")))),
                                new Assign(
                                    new Field("_returnValue", "Item1", "A"),
                                    new Use(new Copy(new Local("_param1")))),
                            ])
                            {
                                Terminator = new Return()
                            }
                        ],
                        new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
                        parameters: [("Item0", StringT), ("Item1", Int64T)])
                ])
            },
            {
                "generic union with tuple variant",
                "union MyUnion<T>{ A(T) }",
                NewLoweredProgram(types: [
                    NewDataType(ModuleId, "MyUnion",
                        ["T"],
                        [
                            NewVariant("A",
                                [
                                    NewField("_variantIdentifier", UInt16T),
                                    NewField("Item0", new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T"))
                                ])
                        ])
                ], methods: [
                    NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                        [
                            new BasicBlock(new BasicBlockId("bb0"), [
                                new Assign(
                                    new Local("_returnValue"),
                                    new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")]))),
                                new Assign(
                                    new Field("_returnValue", "_variantIdentifier", "A"),
                                    new Use(new UIntConstant(0, 2))),
                                new Assign(
                                    new Field("_returnValue", "Item0", "A"),
                                    new Use(new Copy(new Local("_param0"))))
                            ])
                            {
                                Terminator = new Return()
                            }
                        ],
                        new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")]),
                        typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")],
                        parameters: [("Item0", new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T"))])
                ])
            },
            // {
            //     "union with class variant",
            //     "union MyUnion { A { field MyField: string, field OtherField: i64 } }",
            //     LoweredProgram(types: [
            //         DataType(ModuleId, "MyUnion",
            //             variants: [
            //                 Variant("A",
            //                     fields: [
            //                         Field("_variantIdentifier", UInt16_t),
            //                         Field("MyField", StringType),
            //                         Field("OtherField", Int64_t),
            //                     ])
            //             ])
            //     ])
            // },
            // {
            //     "union with method",
            //     "union MyUnion { pub fn MyFn(){} }",
            //     LoweredProgram(types: [
            //         DataType(ModuleId, "MyUnion")
            //     ], [
            //                 Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__MyFn"), "MyUnion__MyFn",
            //                     [MethodReturn(UnitConstant(valueUseful: true))],
            //                     parameters: [ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))])
            //             ])
            // },
            // {
            //     "union with method and tuple variants",
            //     "union MyUnion { A(string), pub static fn MyFn() {}, B(string) }",
            //     LoweredProgram(types: [
            //         DataType(ModuleId, "MyUnion",
            //             variants: [
            //                 Variant(
            //                     "A",
            //                     [
            //                         Field("_variantIdentifier", UInt16_t),
            //                         Field("Item0", StringType),
            //                     ]),
            //                 Variant(
            //                     "B",
            //                     [
            //                         Field("_variantIdentifier", UInt16_t),
            //                         Field("Item0", StringType),
            //                     ]),
            //             ])
            //     ], methods: [
            //                 Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__MyFn"), "MyUnion__MyFn",
            //                     [MethodReturn(UnitConstant(true))]),
            //                 Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
            //                     [
            //                         MethodReturn(CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "A",
            //                             true,
            //                             new()
            //                             {
            //                                 {"_variantIdentifier", UInt16Constant(0, true)},
            //                                 {"Item0", LoadArgument(0, true, StringType)},
            //                             }))
            //                     ],
            //                     parameters: [StringType],
            //                     returnType: ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //                 Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__B"), "MyUnion__Create__B",
            //                     [
            //                         MethodReturn(CreateObject(
            //                             ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                             "B",
            //                             true,
            //                             new()
            //                             {
            //                                 {"_variantIdentifier", UInt16Constant(1, true)},
            //                                 {"Item0", LoadArgument(0, true, StringType)},
            //                             }))
            //                     ],
            //                     parameters: [StringType],
            //                     returnType: ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"))),
            //             ])
            // }
        };
    }
}
