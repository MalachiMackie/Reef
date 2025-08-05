using Reef.IL;

using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class ControlFlow
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            {
                "Fallout operator with result",
                """
                static fn SomeFn(): result::<int, string> {
                    var a = ok(1)?;
                    return ok(1);
                }
                """,
                Module(
                    methods:
                    [
                        Method("SomeFn",
                            returnType: ConcreteTypeReference(
                                "result",
                                typeArguments:
                                [
                                    ConcreteTypeReference("int"),
                                    ConcreteTypeReference("string")
                                ]),
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions:
                            [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadTypeFunction(Addr(1), ConcreteTypeReference(
                                    "result",
                                    typeArguments:
                                    [
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("string")
                                    ]), 0, []),
                                new Call(Addr(2)),
                                new CopyStack(Addr(3)),
                                new LoadField(Addr(4), 0, 0),
                                new LoadIntConstant(Addr(5), 1),
                                new CompareIntEqual(Addr(6)),
                                new BranchIfFalse(Addr(7), Addr(9)),
                                new Return(Addr(8)),
                                new LoadField(Addr(9), 0, 1),
                                new StoreLocal(Addr(10), 0),
                                new LoadIntConstant(Addr(11), 1),
                                new LoadTypeFunction(Addr(12), ConcreteTypeReference(
                                    "result",
                                    typeArguments:
                                    [
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("string")
                                    ]), 0, []),
                                new Call(Addr(13)),
                                Return(14)
                            ])
                    ])
            },
            {
                "empty if is last instruction",
                "if (true) {}",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(4)),
                                LoadUnit(2),
                                new Branch(Addr(3), Addr(5)),
                                LoadUnit(4),
                                Drop(5),
                                LoadUnit(6),
                                Return(7)
                            ])
                    ])
            },
            {
                "populated if is last instruction",
                "if (true) {var a = 1}",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(6)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                new Branch(Addr(5), Addr(7)),
                                LoadUnit(6),
                                Drop(7),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "simple if",
                """
                var a;
                if (true) {
                    a = 1;
                }
                a = 2;
                """,
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(6)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                new Branch(Addr(5), Addr(7)),
                                LoadUnit(6),
                                Drop(7),
                                new LoadIntConstant(Addr(8), 2),
                                new StoreLocal(Addr(9), 0),
                                LoadUnit(10),
                                Return(11)
                            ])
                    ])
            },
            {
                "if with else",
                """
                var a;
                if (true) {
                    a = 1;
                } else {
                    a = 2;
                }
                """,
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(6)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                new Branch(Addr(5), Addr(9)),
                                new LoadIntConstant(Addr(6), 2),
                                new StoreLocal(Addr(7), 0),
                                LoadUnit(8),
                                Drop(9),
                                LoadUnit(10),
                                Return(11)
                            ])
                    ])
            },
            {
                "if else if chain",
                """
                var a;
                if (true) {
                    a = 1;
                } else if (true) {
                    a = 2;
                } else if (true) {
                    a = 3;
                }
                """,
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(6)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                new Branch(Addr(5), Addr(19)),
                                new LoadBoolConstant(Addr(6), true),
                                new BranchIfFalse(Addr(7), Addr(12)),
                                new LoadIntConstant(Addr(8), 2),
                                new StoreLocal(Addr(9), 0),
                                LoadUnit(10),
                                new Branch(Addr(11), Addr(19)),
                                new LoadBoolConstant(Addr(12), true),
                                new BranchIfFalse(Addr(13), Addr(18)),
                                new LoadIntConstant(Addr(14), 3),
                                new StoreLocal(Addr(15), 0),
                                LoadUnit(16),
                                new Branch(Addr(17), Addr(19)),
                                LoadUnit(18),
                                Drop(19),
                                LoadUnit(20),
                                Return(21)
                            ])
                    ])
            },
            {
                "if else if chain with else",
                """
                var a;
                if (true) {
                    a = 1;
                } else if (true) {
                    a = 2;
                } else if (true) {
                    a = 3;
                } else {
                    a = 4;
                }
                """,
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(6)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                new Branch(Addr(5), Addr(21)),
                                new LoadBoolConstant(Addr(6), true),
                                new BranchIfFalse(Addr(7), Addr(12)),
                                new LoadIntConstant(Addr(8), 2),
                                new StoreLocal(Addr(9), 0),
                                LoadUnit(10),
                                new Branch(Addr(11), Addr(21)),
                                new LoadBoolConstant(Addr(12), true),
                                new BranchIfFalse(Addr(13), Addr(18)),
                                new LoadIntConstant(Addr(14), 3),
                                new StoreLocal(Addr(15), 0),
                                LoadUnit(16),
                                new Branch(Addr(17), Addr(21)),
                                new LoadIntConstant(Addr(18), 4),
                                new StoreLocal(Addr(19), 0),
                                LoadUnit(20),
                                Drop(21),
                                LoadUnit(22),
                                Return(23)
                            ])
                    ])
            },
        };
    }
}