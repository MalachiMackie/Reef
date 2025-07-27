using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class SimpleExpressions
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "push int",
                "1",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            Drop(1),
                            LoadUnit(2),
                            Return(3)
                        ])
                ])
            },
            {
                "push constant string",
                "\"someString\"",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadStringConstant(new InstructionAddress(0), "someString"),
                            Drop(1),
                            LoadUnit(2),
                            Return(3)
                        ])
                ])
            },
            {
                "push constant bool true",
                "true",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadBoolConstant(new InstructionAddress(0), true),
                            Drop(1),
                            LoadUnit(2),
                            Return(3)
                        ])
                ])
            },
            {
                "push constant bool false",
                "false",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadBoolConstant(new InstructionAddress(0), false),
                            Drop(1),
                            LoadUnit(2),
                            Return(3)
                        ])
                ])
            },
            {
                "variable declaration without initializer",
                "var a: int",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            LoadUnit(0),
                            Return(1)
                        ],
                        locals:
                        [
                            new ReefMethod.Local { Type = ConcreteTypeReference("int"), DisplayName = "a" }
                        ])
                ])
            },
            {
                "two variable declarations without initializers",
                "var a: int;var b: string",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            LoadUnit(0),
                            Return(1)
                        ],
                        locals:
                        [
                            new ReefMethod.Local { Type = ConcreteTypeReference("int"), DisplayName = "a" },
                            new ReefMethod.Local { Type = ConcreteTypeReference("string"), DisplayName = "b" },
                        ])
                ])
            },
            {
                "variable declaration with value initializer",
                "var a = 1",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new StoreLocal(new InstructionAddress(1), 0),
                            LoadUnit(2),
                            Return(3)
                        ],
                        locals:
                        [
                            new ReefMethod.Local { Type = ConcreteTypeReference("int"), DisplayName = "a" },
                        ])
                ])
            },
            {
                "two variable declarations with value initializers",
                "var a = 1;var b = \"hello\"",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new StoreLocal(new InstructionAddress(1), 0),
                            new LoadStringConstant(new InstructionAddress(2), "hello"),
                            new StoreLocal(new InstructionAddress(3), 1),
                            LoadUnit(4),
                            Return(5)
                        ],
                        locals:
                        [
                            new ReefMethod.Local { Type = ConcreteTypeReference("int"), DisplayName = "a" },
                            new ReefMethod.Local { Type = ConcreteTypeReference("string"), DisplayName = "b" },
                        ])
                ])
            },
            {
                "less than",
                "var a = 1 < 2",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new LoadIntConstant(new InstructionAddress(1), 2),
                            new CompareIntLessThan(new InstructionAddress(2)),
                            new StoreLocal(new InstructionAddress(3), 0),
                            LoadUnit(4),
                            Return(5)
                        ],
                        locals:
                        [
                            new ReefMethod.Local { Type = ConcreteTypeReference("bool"), DisplayName = "a" },
                        ])
                ])
            },
            {
                "greater than",
                "var a = 1 > 2",
                Module(methods:
                [
                    Method("!Main",
                        isStatic: true,
                        instructions:
                        [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new LoadIntConstant(new InstructionAddress(1), 2),
                            new CompareIntGreaterThan(new InstructionAddress(2)),
                            new StoreLocal(new InstructionAddress(3), 0),
                            LoadUnit(4),
                            Return(5)
                        ],
                        locals:
                        [
                            new ReefMethod.Local { Type = ConcreteTypeReference("bool"), DisplayName = "a" },
                        ])
                ])
            },
            {
                "access local variable",
                """
                var a = 1;
                var b = a;
                var c = b;
                """,
                Module(
                    methods:
                    [
                        Method("!Main",
                            isStatic: true,
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("int") },
                                new ReefMethod.Local { DisplayName = "b", Type = ConcreteTypeReference("int") },
                                new ReefMethod.Local { DisplayName = "c", Type = ConcreteTypeReference("int") },
                            ],
                            instructions:
                            [
                                new LoadIntConstant(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new StoreLocal(Addr(3), 1),
                                new LoadLocal(Addr(4), 1),
                                new StoreLocal(Addr(5), 2),
                                LoadUnit(6),
                                Return(7)
                            ]),
                    ])
            },
        };
    }
}