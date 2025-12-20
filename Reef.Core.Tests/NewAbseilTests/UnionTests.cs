using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;

using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class UnionTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void UnionAbseilTest(string description, string source, NewLoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "UnionTests";

    [Fact]
    public void SingleTest()
    {
        const string source = 
                "union MyUnion{}";
        var expectedProgram = NewLoweredProgram(types: [
            NewDataType(ModuleId, "MyUnion")
        ]);
        
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    public static TheoryData<string, string, NewLoweredModule> TestCases()
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
                                    new Field(new Local("_returnValue"), "_variantIdentifier", "A"),
                                    new Use(new UIntConstant(0, 2))),
                                new Assign(
                                    new Field(new Local("_returnValue"), "Item0", "A"),
                                    new Use(new Copy(new Local("_param0")))),
                                new Assign(
                                    new Field(new Local("_returnValue"), "Item1", "A"),
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
                                    new Field(new Local("_returnValue"), "_variantIdentifier", "A"),
                                    new Use(new UIntConstant(0, 2))),
                                new Assign(
                                    new Field(new Local("_returnValue"), "Item0", "A"),
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
            {
                "union with class variant",
                "union MyUnion { A { field MyField: string, field OtherField: i64 } }",
                NewLoweredProgram(types: [
                    NewDataType(ModuleId, "MyUnion",
                        variants: [
                            NewVariant("A",
                                fields: [
                                    NewField("_variantIdentifier", UInt16T),
                                    NewField("MyField", StringT),
                                    NewField("OtherField", Int64T),
                                ])
                        ])
                ])
            },
            {
                "union with method",
                "union MyUnion { pub fn MyFn(){} }",
                NewLoweredProgram(
                [
                            NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__MyFn"), "MyUnion__MyFn",
                                [new BasicBlock(new BasicBlockId("bb0"), []) {Terminator = new Return()}],
                                Unit,
                                parameters: [("this", new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
                        ], types: [
                    NewDataType(ModuleId, "MyUnion")
                ])
            },
            {
                "union with method and tuple variants",
                "union MyUnion { A(string), pub static fn MyFn() {}, B(string) }",
                NewLoweredProgram(
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__MyFn"), "MyUnion__MyFn",
                            [new BasicBlock(new BasicBlockId("bb0"), []){Terminator = new Return()}],
                            Unit),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_returnValue"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item0", "A"),
                                            new Use(new Copy(new Local("_param0"))))
                                    ])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
                            parameters: [("Item0", StringT)]),
                        NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__B"), "MyUnion__Create__B",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_returnValue"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Field(new Local("_returnValue"), "Item0", "B"),
                                            new Use(new Copy(new Local("_param0"))))
                                    ])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []),
                            parameters: [("Item0", StringT)]),
                    ],
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant(
                                    "A",
                                    [
                                        NewField("_variantIdentifier", UInt16T),
                                        NewField("Item0", StringT),
                                    ]),
                                NewVariant(
                                    "B",
                                    [
                                        NewField("_variantIdentifier", UInt16T),
                                        NewField("Item0", StringT),
                                    ]),
                            ])
                    ])
            }
        };
    }
}
