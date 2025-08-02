using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class MethodTests
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "access parameter",
                """
                static fn SomeFn(a: int, b: int) {
                    var foo = a;
                    var bar = b;
                }
                """,
                Module(
                    methods:
                    [
                        Method("SomeFn",
                            isStatic: true,
                            locals:
                            [
                                new ReefMethod.Local { DisplayName = "foo", Type = ConcreteTypeReference("int") },
                                new ReefMethod.Local { DisplayName = "bar", Type = ConcreteTypeReference("int") },
                            ],
                            parameters:
                            [
                                Parameter("a", ConcreteTypeReference("int")),
                                Parameter("b", ConcreteTypeReference("int")),
                            ],
                            instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 1),
                                new StoreLocal(Addr(3), 1),
                                LoadUnit(4),
                                Return(5)
                            ]),
                    ])
            },
            {
                "call global method",
                """
                static fn FirstFn(){}
                FirstFn();
                """,
                Module(
                    methods:
                    [
                        Method("FirstFn", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                        Method("!Main", isStatic: true, instructions:
                        [
                            new LoadGlobalFunction(Addr(0), FunctionReference("FirstFn")),
                            new Call(Addr(1)),
                            Drop(2),
                            LoadUnit(3),
                            Return(4)
                        ])
                    ]
                )
            },
            {
                "call instance and static methods",
                """
                class MyClass {
                    static fn Ignore() {}
                    pub static fn StaticFn() {}
                    pub fn InstanceFn() {}
                }
                new MyClass{}.InstanceFn();
                MyClass::StaticFn();
                """,
                Module(
                    types: [
                        Class("MyClass",
                            methods: [
                                Method("Ignore", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                                Method("StaticFn", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                                Method(
                                    "InstanceFn",
                                    isStatic: false,
                                    parameters: [Parameter("this", ConcreteTypeReference("MyClass"))],
                                    instructions: [LoadUnit(0), Return(1)]),
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new LoadTypeFunction(Addr(1), ConcreteTypeReference("MyClass"), 2),
                                new Call(Addr(2)),
                                Drop(3),
                                new LoadTypeFunction(Addr(4), ConcreteTypeReference("MyClass"), 1),
                                new Call(Addr(5)),
                                Drop(6),
                                LoadUnit(7),
                                Return(8)
                            ])
                    ])
            },
            {
                "functions in inner block",
                """
                static fn SomeFn() {
                    { 
                        fn InnerFn() {
                        }
                        
                        InnerFn();
                    }
                }
                """,
                Module(
                    methods: [
                        Method(
                            "InnerFn",
                            isStatic: false,
                            instructions: [LoadUnit(0), Return(1)]),
                        Method("SomeFn",
                            isStatic: true,
                            instructions: [
                                new LoadGlobalFunction(Addr(0), FunctionReference("InnerFn")),
                                new Call(Addr(1)),
                                Drop(2),
                                LoadUnit(3),
                                Return(4)
                            ])
                    ])
            },
            {
                "assign function to variable",
                """
                fn SomeFn(param: int) {
                }
                var a = SomeFn;
                a(1);
                """,
                Module()
            },
            {
                "assign instance function to variable",
                """
                class MyClass {
                    pub fn MyFn() {
                    }
                }
                var a = new MyClass{};
                var b = a.MyFn;
                b();
                """,
                Module()
            }
        };
    }
}