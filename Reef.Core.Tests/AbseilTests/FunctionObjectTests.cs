using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class FunctionObjectTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void FunctionObjectAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "assign global function to function object",
                """
                fn SomeFn(){}
                var a = SomeFn;
                """,
                LoweredProgram(
                    methods: [
                        Method("SomeFn",
                            [
                                MethodReturnUnit()
                            ]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new() {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("SomeFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assign static function to function object inside type",
                """
                class MyClass {
                    static fn OtherFn(){}
                    static fn MyFn() {
                        var a = OtherFn;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__OtherFn",
                            [MethodReturnUnit()]),
                        Method("MyClass__MyFn",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new(){
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__OtherFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assign static function to function object",
                """
                class MyClass {
                    pub static fn OtherFn(){}
                }
                var a = MyClass::OtherFn;
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__OtherFn",
                            [MethodReturnUnit()]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new(){
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__OtherFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assign instance function to function object",
                """
                class MyClass {
                    pub fn MyFn(){}
                }
                var a = new MyClass{};
                var b = a.MyFn;
                """,
                LoweredProgram(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(
                            "MyClass__MyFn",
                            [MethodReturnUnit()],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method("_Main",
                            [
                                VariableDeclaration(
                                    "a",
                                    CreateObject(
                                        ConcreteTypeReference("MyClass"),
                                        "_classVariant",
                                        true),
                                    false),
                                VariableDeclaration(
                                    "b",
                                    CreateObject(
                                        ConcreteTypeReference("Function`1", [Unit]),
                                        "_classVariant",
                                        true,
                                        new()
                                        {
                                            {
                                                "FunctionReference",
                                                FunctionReferenceConstant(
                                                    FunctionReference("MyClass__MyFn"),
                                                    true,
                                                    FunctionType([], Unit))
                                            },
                                            {
                                                "FunctionParameter",
                                                LocalAccess(
                                                    "a",
                                                    true,
                                                    ConcreteTypeReference("MyClass"))
                                            }
                                        }),
                                    false),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("Function`1", [Unit]))
                            ])
                    ])
            },
            {
                "assigning closure to function object",
                """
                class MyClass
                {
                    mut field MyField: string,

                    mut fn MyFn(param: string)
                    {
                        var a = "";
                        fn InnerFn()
                        {
                            var _a = a;
                            var _param = param;
                            var _myField = MyField;
                        }
                        var b = InnerFn;
                    }
                }
                """,
                LoweredProgram()
            },
            {
                "call function object without parameters",
                """
                fn SomeFn() {}
                var a = SomeFn;
                a();
                """,
                LoweredProgram()
            },
            {
                "call function object with parameters",
                """
                fn SomeFn(a: string): string { return a; }
                var a = SomeFn;
                a("");
                """,
                LoweredProgram()
            }
        };
    }
}

