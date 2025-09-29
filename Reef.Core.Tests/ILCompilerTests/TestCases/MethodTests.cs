using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;
using Reef.IL;
using Xunit.Abstractions;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class MethodTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram);
        
        testOutputHelper.WriteLine(PrettyPrinter.PrettyPrintLoweredProgram(loweredProgram, false, false));

        var module = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }
    
    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid));
    }
    
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
                    methods: [
                        Method("SomeFn",
                            [
                                new LoadArgument(0),
                                new StoreLocal("foo"),
                                new LoadArgument(1),
                                new StoreLocal("bar"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [IntType, IntType],
                            locals: [
                                Local("foo", IntType),
                                Local("bar", IntType)
                            ])
                    ])
            },
            {
                "call global method",
                """
                static fn FirstFn(){}
                FirstFn();
                """,
                Module(
                    methods: [
                        Method("FirstFn",
                            [LoadUnit(), Return()]),
                        Method("_Main",
                            [
                                new LoadFunction(FunctionDefinitionReference("FirstFn")),
                                new Call(0),
                                Return(),
                                LoadUnit()
                            ])
                    ])
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
                        DataType("MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__Ignore", [LoadUnit(), Return()]),
                        Method("MyClass__StaticFn", [LoadUnit(), Return()]),
                        Method(
                            "MyClass__InstanceFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new LoadFunction(FunctionDefinitionReference("MyClass__InstanceFn")),
                                new Call(1),
                                new LoadFunction(FunctionDefinitionReference("MyClass__StaticFn")),
                                new Call(0),
                                LoadUnit(),
                                Return()
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
                        Method("SomeFn__InnerFn", [LoadUnit(), Return()]),
                        Method("SomeFn",
                            [
                                new LoadFunction(FunctionDefinitionReference("SomeFn__InnerFn")),
                                new Call(0),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "assign global function to variable",
                """
                fn SomeFn(param: int) {
                }
                var a = SomeFn;
                a(1);
                """,
                Module(
                    methods: [
                        Method("SomeFn",
                            [LoadUnit(), Return()],
                            parameters: [IntType]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("Function`2", [IntType, UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("SomeFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference("Function`2__Call", [IntType, UnitType])),
                                new Call(2),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`2", [IntType, UnitType]))
                            ])
                    ])
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
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new StoreLocal("a"),
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new LoadLocal("a"),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("b"),
                                new LoadLocal("b"),
                                new LoadFunction(FunctionDefinitionReference("Function`1__Call", [UnitType])),
                                new Call(1),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("Function`1", [UnitType]))
                            ])
                    ])
            },
            {
                "assign static type function to variable",
                """
                class MyClass { 
                    pub static fn MyFn() {}
                }
                var a = MyClass::MyFn;
                a();
                """,
                Module(
                    types: [DataType("MyClass", variants: [Variant("_classVariant")])],
                    methods: [
                        Method("MyClass__MyFn", [LoadUnit(), Return()]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadFunction(FunctionDefinitionReference("Function`1__Call", [UnitType])),
                                new Call(1),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`1", [UnitType]))
                            ])
                    ])
            },
            {
                "assign static type function with parameters and return type to variable",
                """
                class MyClass { 
                    pub static fn MyFn(a: string, b: int): bool { return true; }
                }
                var a = MyClass::MyFn;
                a("", 1);
                """,
                Module(
                    types: [DataType("MyClass", variants: [Variant("_classVariant")])],
                    methods: [
                        Method("MyClass__MyFn",
                            [new LoadBoolConstant(true), Return()],
                            parameters: [StringType, IntType],
                            returnType: BoolType),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("Function`3", [StringType, IntType, BoolType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadStringConstant(""),
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference("Function`3__Call", [StringType, IntType, BoolType])),
                                new Call(3),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`3", [StringType, IntType, BoolType]))
                            ])
                    ])
            },
            {
                "assign instance function to variable from within method",
                """
                class MyClass {
                    fn MyFn() {
                        var a = MyFn;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")],
                            locals: [Local("a", ConcreteTypeReference("Function`1", [UnitType]))])
                    ])
            },
            {
                "call instance function from within instance function",
                """
                class MyClass {
                    fn MyFn() {
                        MyFn();
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [
                                new LoadArgument(0),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new Call(1),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")])
                    ])
            },
            {
                "assign instance function to variable from within same instance but different method",
                """
                class MyClass {
                    fn MyFn() {}
                    fn OtherFn() {
                        var a = MyFn;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method("MyClass__OtherFn",
                            [
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("Function`1", [UnitType]))],
                            parameters: [ConcreteTypeReference("MyClass")])
                    ])
            },
            {
                "call generic method",
                """
                fn MyFn<T>() {}
                MyFn::<string>();
                """,
                Module(
                    methods: [
                        Method("MyFn", [LoadUnit(), Return()], typeParameters: ["T"]),
                        Method("_Main",
                            [
                                new LoadFunction(FunctionDefinitionReference("MyFn", [StringType])),
                                new Call(0),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "assign generic method to variable",
                """
                fn MyFn<T>(param: T){}
                var a = MyFn::<string>;
                """,
                Module(
                    methods: [
                        Method("MyFn",
                            [LoadUnit(), Return()],
                            typeParameters: ["T"],
                            parameters: [GenericTypeReference("T")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("Function`2", [StringType, UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyFn", [StringType])),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("Function`2", [StringType, UnitType]))
                            ])
                    ])
            },
            {
                "call static type generic function",
                """
                class MyClass<T> {
                    pub static fn MyFn<T2>(){}
                }
                MyClass::<int>::MyFn::<string>();
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method("MyClass__MyFn", [LoadUnit(), Return()], typeParameters: ["T", "T2"]),
                        Method("_Main",
                            [
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn", [IntType, StringType])),
                                new Call(0),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "call instance type generic function",
                """
                class MyClass<T> {
                    pub fn MyFn<T2>(){}
                }
                var a = new MyClass::<int>{};
                a.MyFn::<string>();
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference("MyClass", [GenericTypeReference("T")])],
                            typeParameters: ["T", "T2"]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass", [IntType])),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn", [IntType, StringType])),
                                new Call(1),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyClass", [IntType]))])
                    ])
            },
            {
                "call generic method within type",
                """
                class MyClass<T> {
                    fn MyFn<T2>() {}
                    fn OtherFn() {
                        MyFn::<string>();
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [LoadUnit(), Return()],
                            typeParameters: ["T", "T2"],
                            parameters: [ConcreteTypeReference("MyClass", [GenericTypeReference("T")])]),
                        Method("MyClass__OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn", [GenericTypeReference("T"), StringType])),
                                new Call(1),
                                LoadUnit(),
                                Return()
                            ],
                            typeParameters: ["T"],
                            parameters: [ConcreteTypeReference("MyClass", [GenericTypeReference("T")])])
                    ])
            }
        };
    }
}