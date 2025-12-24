using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class UnionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void UnionAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "UnionTests";

    [Fact]
    public void SingleTest()
    {
        const string source = 
                "union MyUnion{}";
        var expectedProgram = LoweredProgram(types: [
            DataType(ModuleId, "MyUnion")
        ]);
        
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
            {
                "empty union",
                "union MyUnion{}",
                LoweredProgram(types: [
                    DataType(ModuleId, "MyUnion")
                ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                LoweredProgram(types: [
                        DataType(ModuleId, "MyUnion",
                            ["T"])
                ])
            },
            {
                "union with unit variants",
                "union MyUnion{A, B}",
                LoweredProgram(types: [
                    DataType(ModuleId, "MyUnion",
                        variants: [
                            Variant("A", [Field("_variantIdentifier", UInt16T)]),
                            Variant("B", [Field("_variantIdentifier", UInt16T)]),
                        ])
                ])
            },
            {
                "generic union with instance function",
                "union MyUnion<T>{pub fn SomeFn(){}}",
                LoweredProgram(types: [
                    DataType(ModuleId, "MyUnion",
                        ["T"])
                ], methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                        [
                            new BasicBlock(BB0, [])
                            {
                                Terminator = new Return()
                            }
                        ],
                        Unit,
                        parameters: [
                            (
                                "this",
                                new LoweredPointer(new LoweredConcreteTypeReference(
                                    "MyUnion",
                                    new DefId(ModuleId, $"{ModuleId}.MyUnion"),
                                    [new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")]))
                            )],
                        typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")])
                ])
            },
            {
                "union with tuple variant",
                "union MyUnion { A(string, i64) }",
                LoweredProgram(types: [
                    DataType(ModuleId, "MyUnion",
                        variants: [
                            Variant("A", [
                                Field("_variantIdentifier", UInt16T),
                                Field("Item0", StringT),
                                Field("Item1", Int64T),
                            ])
                        ])
                ], methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                        [
                            new BasicBlock(
                                BB0,
                                [],
                                AllocateMethodCall(
                                    ConcreteTypeReference("MyUnion", ModuleId),
                                    ReturnValue,
                                    BB1)),
                            new BasicBlock(BB1, [
                                new Assign(
                                    new Deref(ReturnValue),
                                    new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                new Assign(
                                    new Field(new Deref(ReturnValue), "_variantIdentifier", "A"),
                                    new Use(new UIntConstant(0, 2))),
                                new Assign(
                                    new Field(new Deref(ReturnValue), "Item0", "A"),
                                    new Use(new Copy(Param0))),
                                new Assign(
                                    new Field(new Deref(ReturnValue), "Item1", "A"),
                                    new Use(new Copy(Param1))),
                            ])
                            {
                                Terminator = new Return()
                            }
                        ],
                        new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                        parameters: [("Item0", StringT), ("Item1", Int64T)])
                ])
            },
            {
                "generic union with tuple variant",
                "union MyUnion<T>{ A(T) }",
                LoweredProgram(types: [
                    DataType(ModuleId, "MyUnion",
                        ["T"],
                        [
                            Variant("A",
                                [
                                    Field("_variantIdentifier", UInt16T),
                                    Field("Item0", new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T"))
                                ])
                        ])
                ], methods: [
                    Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                        [
                            new BasicBlock(
                                BB0,
                                [],
                                AllocateMethodCall(
                                    ConcreteTypeReference(
                                        "MyUnion",
                                        ModuleId,
                                        [new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")]),
                                    ReturnValue,
                                    BB1)),
                            new BasicBlock(BB1, [
                                new Assign(
                                    new Deref(ReturnValue),
                                    new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")]))),
                                new Assign(
                                    new Field(new Deref(ReturnValue), "_variantIdentifier", "A"),
                                    new Use(new UIntConstant(0, 2))),
                                new Assign(
                                    new Field(new Deref(ReturnValue), "Item0", "A"),
                                    new Use(new Copy(Param0)))
                            ])
                            {
                                Terminator = new Return()
                            }
                        ],
                        new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")])),
                        typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T")],
                        parameters: [("Item0", new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyUnion"), "T"))])
                ])
            },
            {
                "union with class variant",
                "union MyUnion { A { field MyField: string, field OtherField: i64 } }",
                LoweredProgram(types: [
                    DataType(ModuleId, "MyUnion",
                        variants: [
                            Variant("A",
                                fields: [
                                    Field("_variantIdentifier", UInt16T),
                                    Field("MyField", StringT),
                                    Field("OtherField", Int64T),
                                ])
                        ])
                ])
            },
            {
                "union with method",
                "union MyUnion { pub fn MyFn(){} }",
                LoweredProgram(
                [
                            Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__MyFn"), "MyUnion__MyFn",
                                [new BasicBlock(BB0, []) {Terminator = new Return()}],
                                Unit,
                                parameters: [("this", new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])))])
                        ], types: [
                    DataType(ModuleId, "MyUnion")
                ])
            },
            {
                "union with method and tuple variants",
                "union MyUnion { A(string), pub static fn MyFn() {}, B(string) }",
                LoweredProgram(
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__MyFn"), "MyUnion__MyFn",
                            [new BasicBlock(BB0, []){Terminator = new Return()}],
                            Unit),
                        Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        ReturnValue,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(ReturnValue),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item0", "A"),
                                            new Use(new Copy(Param0)))
                                    ])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                            parameters: [("Item0", StringT)]),
                        Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__B"), "MyUnion__Create__B",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        ReturnValue,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(ReturnValue),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "_variantIdentifier", "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Field(new Deref(ReturnValue), "Item0", "B"),
                                            new Use(new Copy(Param0)))
                                    ])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                            parameters: [("Item0", StringT)]),
                    ],
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("Item0", StringT),
                                    ]),
                                Variant(
                                    "B",
                                    [
                                        Field("_variantIdentifier", UInt16T),
                                        Field("Item0", StringT),
                                    ]),
                            ])
                    ])
            }
        };
    }
}
