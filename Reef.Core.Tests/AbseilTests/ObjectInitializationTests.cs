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
                LoweredProgram(ModuleId, 
                        types: [
                            DataType(ModuleId, "MyClass",
                                variants: [
                                    Variant("_classVariant")
                                ])
                        ],
                        methods: [
                            Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                                [
                                    new BasicBlock(
                                        BB0,
                                        [],
                                        AllocateMethodCall(
                                            ConcreteTypeReference("MyClass", ModuleId),
                                            Local0,
                                            BB1)),
                                    new BasicBlock(BB1, [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference(
                                                "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                    ], new GoTo(BB2)),
                                    new BasicBlock(BB2, [], new Return())
                                ],
                                Unit,
                                locals: [
                                    new MethodLocal(
                                        "_local0",
                                        "a",
                                        new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                ])
                        ])
            },
            {
                "create unboxed empty class",
                "class MyClass{} var a = new unboxed MyClass{};",
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyClass",
                            variants: [
                                Variant("_classVariant")
                            ])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(BB0, [
                                    new Assign(
                                        Local0,
                                        new CreateObject(new LoweredConcreteTypeReference(
                                            "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                ], new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
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
                LoweredProgram(ModuleId, 
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
                                        BB0,
                                        [],
                                        AllocateMethodCall(
                                            ConcreteTypeReference("MyClass", ModuleId),
                                            Local0,
                                            BB1)),
                                    new BasicBlock(
                                        BB1,
                                        [
                                            new Assign(
                                                new Deref(Local0),
                                                new CreateObject(
                                                    new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                            new Assign(
                                                new Field(
                                                    new Deref(Local0),
                                                    "MyField",
                                                    "_classVariant"),
                                                new Use(new StringConstant("hi")))
                                        ],
                                        new GoTo(BB2)),
                                    new BasicBlock(BB2, [], new Return())
                                ],
                                Unit,
                                locals: [
                                    new MethodLocal(
                                        "_local0",
                                        "a",
                                        new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                ])
                        ])
            },
            {
                "Create unit union variant",
                "union MyUnion{A} var a = MyUnion::A;",
                LoweredProgram(ModuleId, 
                    types: [
                        DataType(ModuleId, "MyUnion",
                            variants: [Variant("A", [Field("_variantIdentifier", UInt16T)])])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(
                                                new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(new Deref(Local0), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2)))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(
                                    BB2,
                                    [],
                                    new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal(
                                "_local0",
                                "a",
                                new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])))])
                    ])
            },
            {
                "Create class union variant",
                "union MyUnion{A, B {field a: string}} var a = new MyUnion::B { a = \"hi\"};",
                LoweredProgram(ModuleId, 
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
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyUnion", ModuleId),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateObject(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                        new Assign(
                                            new Field(
                                                new Deref(Local0),
                                                "_variantIdentifier",
                                                "B"),
                                            new Use(new UIntConstant(1, 2))),
                                        new Assign(
                                            new Field(
                                                new Deref(Local0),
                                                "a",
                                                "B"),
                                            new Use(new StringConstant("hi")))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])))
                            ])
                    ])
            }
        };
    }
}
