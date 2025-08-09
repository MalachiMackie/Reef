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
            {
                "plus",
                "var a = 1 + 2",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("int"))],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadIntConstant(Addr(1), 2),
                                new IntPlus(Addr(2)),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ])
                    ])
            },
            {
                "minus",
                "var a = 1 - 2",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("int"))],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadIntConstant(Addr(1), 2),
                                new IntMinus(Addr(2)),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ])
                    ])
            },
            {
                "multiply",
                "var a = 1 * 2",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("int"))],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadIntConstant(Addr(1), 2),
                                new IntMultiply(Addr(2)),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ])
                    ])
            },
            {
                "divide",
                "var a = 1 / 2",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("int"))],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadIntConstant(Addr(1), 2),
                                new IntDivide(Addr(2)),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ])
                    ])
            },
            {
                "equals",
                "var a = 1 == 2",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("bool"))],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadIntConstant(Addr(1), 2),
                                new CompareIntEqual(Addr(2)),
                                new StoreLocal(Addr(3), 0),
                                LoadUnit(4),
                                Return(5)
                            ])
                    ])
            },
            {
                "local assignment",
                """
                var a;
                a = 1;
                """,
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("int"))],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
                            ])
                    ])
            },
            {
                "field assignment",
                """
                class MyClass{pub mut field MyField: int}
                var mut a = new MyClass{MyField = 1};
                a.MyField = 2;
                """,
                Module(
                    types: [
                        Class("MyClass",
                            fields: [Field("MyField", isPublic: true, type: ConcreteTypeReference("int"))])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [Local("a", ConcreteTypeReference("MyClass"))],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadIntConstant(Addr(6), 2),
                                new StoreField(Addr(7), 0, 0),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "static field assignment",
                """
                class MyClass{pub static mut field MyField: int = 1}
                MyClass::MyField = 2;
                """,
                Module(
                    types: [
                        Class("MyClass",
                            fields: [
                                Field(
                                    "MyField",
                                    isPublic: true,
                                    isStatic: true,
                                    type: ConcreteTypeReference("int"),
                                    staticInitializer: [new LoadIntConstant(Addr(0), 1)])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new LoadIntConstant(Addr(0), 2),
                                new StoreStaticField(Addr(1), ConcreteTypeReference("MyClass"), 0),
                                LoadUnit(2),
                                Return(3)
                            ])
                    ])
            },
            {
                "single element tuple",
                "var a = (1);",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a",  ConcreteTypeReference("int")),
                            ],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
                            ])
                    ])
            },
            {
                "tuple with multiple elements",
                "var a = (1, true)",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("Tuple`2", typeArguments: [
                                    ConcreteTypeReference("int"),
                                    ConcreteTypeReference("bool")
                                ]))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Tuple`2", typeArguments: [
                                    ConcreteTypeReference("int"),
                                    ConcreteTypeReference("bool")
                                ])),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreField(Addr(3), 0, 0),
                                new CopyStack(Addr(4)),
                                new LoadBoolConstant(Addr(5), true),
                                new StoreField(Addr(6), 0, 1),
                                new StoreLocal(Addr(7), 0),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "bool not",
                "var a = !true;",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("bool"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BoolNot(Addr(1)),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ])
                    ])
            },
            {
                "and",
                "var a = true && true",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("bool"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfFalse(Addr(1), Addr(6)),
                                new LoadBoolConstant(Addr(2), true),
                                new BranchIfFalse(Addr(3), Addr(6)),
                                new LoadBoolConstant(Addr(4), true),
                                new Branch(Addr(5), Addr(7)),
                                new LoadBoolConstant(Addr(6), false),
                                new StoreLocal(Addr(7), 0),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            },
            {
                "or",
                "var a = true || true",
                Module(
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("bool"))
                            ],
                            instructions: [
                                new LoadBoolConstant(Addr(0), true),
                                new BranchIfTrue(Addr(1), Addr(6)),
                                new LoadBoolConstant(Addr(2), true),
                                new BranchIfTrue(Addr(3), Addr(6)),
                                new LoadBoolConstant(Addr(4), false),
                                new Branch(Addr(5), Addr(7)),
                                new LoadBoolConstant(Addr(6), true),
                                new StoreLocal(Addr(7), 0),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            }
        };
    }
}