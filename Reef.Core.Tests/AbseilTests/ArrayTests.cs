using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;
using Index = Reef.Core.LoweredExpressions.Index;

namespace Reef.Core.Tests.AbseilTests;

public class ArrayTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ArrayAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ArrayTests";

    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
            {
                "empty array",
                "var a: [string; 0] = []",
                LoweredProgram(ModuleId,
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        new LoweredArray(StringT, 0),
                                        new Local("_local0"),
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(new Local("_local0")),
                                            new CreateArray(new LoweredArray(StringT, 0)))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(
                                    BB2,
                                    [],
                                    new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(new LoweredArray(
                                        StringT,
                                        0)))
                            ])
                    ])
            },
            {
                "collection expression with literals",
                "var a = [1, 4, 8]",
                LoweredProgram(
                    ModuleId,
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        new LoweredArray(Int32T, 3),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateArray(
                                                new LoweredArray(Int32T, 3))),
                                        new Assign(
                                            new Index(new Deref(Local0), new UIntConstant(0, 8)),
                                            new Use(new IntConstant(1, 4))),
                                        new Assign(
                                            new Index(new Deref(Local0), new UIntConstant(1, 8)),
                                            new Use(new IntConstant(4, 4))),
                                        new Assign(
                                            new Index(new Deref(Local0), new UIntConstant(2, 8)),
                                            new Use(new IntConstant(8, 4))),
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(
                                        new LoweredArray(Int32T, 3)))
                            ])
                    ])
            },
            {
                "unboxed collection expression with literals",
                "var a = [unboxed; 1, 4, 8]",
                LoweredProgram(
                    ModuleId,
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new CreateArray(new LoweredArray(Int32T, 3))),
                                        new Assign(
                                            new Index(Local0, new UIntConstant(0, 8)),
                                            new Use(new IntConstant(1, 4))),
                                        new Assign(
                                            new Index(Local0, new UIntConstant(1, 8)),
                                            new Use(new IntConstant(4, 4))),
                                        new Assign(
                                            new Index(Local0, new UIntConstant(2, 8)),
                                            new Use(new IntConstant(8, 4))),
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredArray(Int32T, 3))
                            ])
                    ])
            },
            {
                "collection expression with boxed values",
                """
                class MyClass{}
                var a = [new MyClass{}, new MyClass{}]; 
                """,
                LoweredProgram(
                    ModuleId,
                    types: [
                        DataType(ModuleId,
                            "MyClass",
                            variants: [
                                Variant("_classVariant")
                            ])
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        new LoweredArray(
                                            new LoweredPointer(
                                                ConcreteTypeReference("MyClass", ModuleId)), 2),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateArray(new LoweredArray(
                                                new LoweredPointer(ConcreteTypeReference("MyClass", ModuleId)), 2)))
                                    ],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyClass", ModuleId),
                                        new Index(new Deref(Local0), new UIntConstant(0, 8)),
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(
                                            new Deref(new Index(new Deref(Local0), new UIntConstant(0, 8))),
                                            new CreateObject(
                                                ConcreteTypeReference("MyClass", ModuleId))),
                                    ],
                                    AllocateMethodCall(
                                        ConcreteTypeReference("MyClass", ModuleId),
                                        new Index(new Deref(Local0), new UIntConstant(1, 8)),
                                        BB3)),
                                new BasicBlock(
                                    BB3,
                                    [
                                        new Assign(
                                            new Deref(new Index(new Deref(Local0), new UIntConstant(1, 8))),
                                            new CreateObject(
                                                ConcreteTypeReference("MyClass", ModuleId))),
                                    ],
                                    new GoTo(BB4)),
                                new BasicBlock(BB4, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(
                                        new LoweredArray(
                                            new LoweredPointer(
                                                ConcreteTypeReference("MyClass", ModuleId)), 2))),
                            ])
                    ])
            },
            {
                "fill collection expression",
                """
                var a = [4; 3]; 
                """,
                LoweredProgram(
                    ModuleId,
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        new LoweredArray(
                                                Int32T, 3),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateArray(
                                                new LoweredArray(Int32T, 3))),
                                        new Assign(
                                            new Deref(Local0),
                                            new Fill(new IntConstant(4, 4), 3))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(
                                    BB2,
                                    [],
                                    new Return()),
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(
                                        new LoweredArray(Int32T, 3))),
                            ])
                    ])
            },
            {
                "unboxed fill collection expression",
                """
                var a = [4; 3]; 
                """,
                LoweredProgram(
                    ModuleId,
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        new LoweredArray(
                                                Int32T, 3),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateArray(new LoweredArray(Int32T, 3))),
                                        new Assign(
                                            new Deref(Local0),
                                            new Fill(new IntConstant(4, 4), 3))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(
                                    BB2,
                                    [],
                                    new Return()),
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(
                                        new LoweredArray(Int32T, 3))),
                            ])
                    ])
            },
            {
                "index into boxed array",
                """
                var a = [1; 4];
                var b = a[2];
                """,
                LoweredProgram(
                    ModuleId,
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        new LoweredArray(Int32T, 4),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            new Deref(Local0),
                                            new CreateArray(
                                                new LoweredArray(Int32T, 4))),
                                        new Assign(
                                            new Deref(Local0),
                                            new Fill(new IntConstant(1, 4), 4)),
                                        new Assign(
                                            Local2,
                                            new BinaryOperation(
                                                new UIntConstant(2, 8),
                                                new UIntConstant(4, 8),
                                                BinaryOperationKind.LessThan))
                                    ],
                                    new LoweredExpressions.Assert(
                                        new Copy(Local2),
                                        BB2)),
                                new BasicBlock(
                                    BB2,
                                    [
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(
                                                new Index(new Deref(Local0), new UIntConstant(2, 8)))))
                                    ],
                                    new GoTo(BB3)),
                                new BasicBlock(
                                    BB3, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredPointer(new LoweredArray(Int32T, 4))) ,
                                new MethodLocal(
                                    "_local1",
                                    "b",
                                    Int32T),
                                new MethodLocal(
                                    "_local2",
                                    null,
                                    BooleanT)
                            ])
                    ])
            },
            {
                "index into unboxed array",
                """
                var a = [unboxed; 1; 4];
                var b = a[2];
                """,
                LoweredProgram(
                    ModuleId,
                    [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}._Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new CreateArray(
                                                new LoweredArray(Int32T, 4))),
                                        new Assign(
                                            Local0,
                                            new Fill(new IntConstant(1, 4), 4)),
                                        new Assign(
                                            Local2,
                                            new BinaryOperation(
                                                new UIntConstant(2, 8),
                                                new UIntConstant(4, 8),
                                                BinaryOperationKind.LessThan))
                                    ],
                                    new LoweredExpressions.Assert(
                                        new Copy(Local2),
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        new Assign(
                                            Local1,
                                            new Use(new Copy(
                                                new Index(Local0, new UIntConstant(2, 8)))))
                                    ],
                                    new GoTo(BB2)),
                                new BasicBlock(
                                    BB2, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal(
                                    "_local0",
                                    "a",
                                    new LoweredArray(Int32T, 4)),
                                new MethodLocal(
                                    "_local1",
                                    "b",
                                    Int32T),
                                new MethodLocal(
                                    "_local2",
                                    null,
                                    BooleanT)
                            ])
                    ])
            }
        };
    }
}