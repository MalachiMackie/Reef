using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ObjectInitializationTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ObjectInitializationAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ObjectInitializationTests";

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
            {
                "create empty class",
                "class MyClass{} var a = new MyClass{};",
                LoweredProgram(
                        types: [
                            DataType(ModuleId, "MyClass",
                                variants: [
                                    Variant("_classVariant")
                                ])
                        ],
                        methods: [
                            Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                                [
                                    new BasicBlock(new BasicBlockId("bb0"), [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new LoweredConcreteTypeReference(
                                                "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                    ], new GoTo(new BasicBlockId("bb1"))),
                                    new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                ],
                                Unit,
                                locals: [
                                    new MethodLocal(
                                        "_local0",
                                        "a",
                                        new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                                ])
                        ])
            },
            {
                "create class with fields",
                "class MyClass{pub field MyField: string} var a = new MyClass{MyField = \"hi\"};",
                LoweredProgram(
                        types: [
                            DataType(ModuleId, "MyClass",
                                variants: [
                                    Variant("_classVariant", [Field("MyField", StringT)])
                                ])
                        ],
                        methods: [
                            Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                                [
                                    new BasicBlock(
                                        new BasicBlockId("bb0"),
                                        [
                                            new Assign(
                                                new Local("_local0"),
                                                new CreateObject(
                                                    new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
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
                                    new MethodLocal(
                                        "_local0",
                                        "a",
                                        new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                                ])
                        ])
            },
            {
                "Create unit union variant",
                "union MyUnion{A} var a = MyUnion::A;",
                LoweredProgram(
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [Variant("A", [Field("_variantIdentifier", UInt16T)])])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
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
                            locals: [new MethodLocal(
                                "_local0",
                                "a",
                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))])
                    ])
            },
            {
                "Create class union variant",
                "union MyUnion{A, B {field a: string}} var a = new MyUnion::B { a = \"hi\"};",
                LoweredProgram(
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [
                                Variant("A", [Field("_variantIdentifier", UInt16T)]),
                                Variant("B", [
                                    Field("_variantIdentifier", UInt16T),
                                    Field("a", StringT),
                                ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    new BasicBlockId("bb0"),
                                    [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
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
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))
                            ])
                    ])
            }
        };
    }
}
