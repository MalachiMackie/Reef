using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;

using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ObjectInitializationTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ObjectInitializationAbseilTest(string description, string source, NewLoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ObjectInitializationTests";

    public static TheoryData<string, string, NewLoweredModule> TestCases()
    {
        return new()
        {
            {
                "create empty class",
                "class MyClass{} var a = new MyClass{};",
                NewLoweredProgram(
                        types: [
                            NewDataType(ModuleId, "MyClass",
                                variants: [
                                    NewVariant("_classVariant")
                                ])
                        ],
                        methods: [
                            NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                                [
                                    new BasicBlock(new BasicBlockId("bb0"), [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference(
                                                "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                    ], new GoTo(new BasicBlockId("bb1"))),
                                    new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                ],
                                Unit,
                                locals: [
                                    new NewMethodLocal(
                                        "_local0",
                                        "a",
                                        new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                                ])
                        ])
            },
            {
                "create class with fields",
                "class MyClass{pub field MyField: string} var a = new MyClass{MyField = \"hi\"};",
                NewLoweredProgram(
                        types: [
                            NewDataType(ModuleId, "MyClass",
                                variants: [
                                    NewVariant("_classVariant", [NewField("MyField", StringT)])
                                ])
                        ],
                        methods: [
                            NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                                [
                                    new BasicBlock(
                                        new BasicBlockId("bb0"),
                                        [
                                            new Assign(
                                                new Local("_local0"),
                                                new CreateObject(
                                                    new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                            new Assign(
                                                new Field(
                                                    new Local("_local0"),
                                                    "MyField",
                                                    "_classVariant"),
                                                new Use(new StringConstant("hi")))
                                        ],
                                        new GoTo(new BasicBlockId("bb1"))),
                                    new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                ],
                                Unit,
                                locals: [
                                    new NewMethodLocal(
                                        "_local0",
                                        "a",
                                        new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                                ])
                        ])
            },
            {
                "Create unit union variant",
                "union MyUnion{A} var a = MyUnion::A;",
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [NewVariant("A", [NewField("_variantIdentifier", UInt16T)])])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Local("_local0"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2)))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(
                                    new BasicBlockId("bb1"),
                                    [],
                                    new Return())
                            ],
                            Unit,
                            locals: [new NewMethodLocal(
                                "_local0",
                                "a",
                                new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
                    ])
            },
            {
                "Create class union variant",
                "union MyUnion{A, B {field a: string}} var a = new MyUnion::B { a = \"hi\"};",
                NewLoweredProgram(
                    types: [
                        NewDataType(ModuleId, "MyUnion",
                            variants: [
                                NewVariant("A", [NewField("_variantIdentifier", UInt16T)]),
                                NewVariant("B", [
                                    NewField("_variantIdentifier", UInt16T),
                                    NewField("a", StringT),
                                ])
                            ])
                    ],
                    methods: [
                        NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(
                                                new Local("_local0"),
                                                "_variantIdentifier",
                                                "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Field(
                                                new Local("_local0"),
                                                "a",
                                                "B"),
                                            new Use(new StringConstant("hi")))
                                    ],
                                    new GoTo(new BasicBlockId("bb1"))),
                                new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                            ],
                            Unit,
                            locals: [
                                new NewMethodLocal(
                                    "_local0",
                                    "a",
                                    new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))
                            ])
                    ])
            }
        };
    }
}
