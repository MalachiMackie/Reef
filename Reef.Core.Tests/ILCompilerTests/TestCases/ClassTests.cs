using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;
using static TestHelpers;

public class ClassTests
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

        var (module, _) = ILCompile.CompileToIL(loweredProgram);
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
                "access this in instance method",
                """
                class MyClass {
                    fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                new LoadArgument(0),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")],
                            locals: [Local("a", ConcreteTypeReference("MyClass"))])
                    ])
            },
            {
                "access instance field via variable in instance method",
                """
                class MyClass {
                    field SomeField: string,

                    fn SomeFn() {
                        var a = SomeField;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant", [Field("SomeField", StringType)])
                            ])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "SomeField"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")],
                            locals: [
                                Local("a", StringType)
                            ])
                    ])
            },
            {
                "access static field via variable in method",
                """
                class MyClass {
                    static field SomeField: string = "",

                    static fn SomeFn() {
                        var a = SomeField;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("SomeField", StringType, [StringConstant("")])])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                new LoadStaticField(ConcreteTypeReference("MyClass"), "SomeField"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", StringType)])
                    ])
            },
            {
                "access parameter in instance method",
                """
                class MyClass {
                    fn SomeFn(param: int) {
                        var a = param;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                new LoadArgument(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass"), IntType],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "create object without fields",
                """
                class MyClass{}
                var a = new MyClass{};
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ])
                    ])
            },
            {
                "create object with fields",
                """
                class MyClass{ pub field Field1: int, pub field Field2: string}
                var a = new MyClass{Field2 = "", Field1 = 2};
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [
                                        Field("Field1", IntType),
                                        Field("Field2", StringType)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new CopyStack(),
                                new LoadIntConstant(2),
                                new StoreField(0, "Field1"),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "Field2"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ])
                    ])
            },
            {
                "call instance method",
                """
                class MyClass {
                    pub fn SomeFn(){}
                }
                var a = new MyClass {};
                a.SomeFn(); 
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadFunction(FunctionDefinitionReference("MyClass__SomeFn")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyClass"))])
                    ])
            },
            {
                "call instance method with parameters",
                """
                class MyClass {
                    pub fn SomeFn(a: int, b: string){}
                }
                var a = new MyClass {};
                a.SomeFn(1, ""); 
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__SomeFn",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("MyClass"), IntType, StringType]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadIntConstant(1),
                                new LoadStringConstant(""),
                                new LoadFunction(FunctionDefinitionReference("MyClass__SomeFn")),
                                new Call(3, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference("MyClass"))])
                    ])
            },
            {
                "call static class method",
                """
                class MyClass {
                    pub static fn MyFn(a: int) {
                    }
                }
                MyClass::MyFn(1);
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method("MyClass__MyFn",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [IntType]),
                        Method("_Main",
                            [
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference("MyClass__MyFn")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "get static field",
                """
                class MyClass { pub static field A: int = 1 }
                var a = MyClass::A;
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("A", IntType, [new LoadIntConstant(1)])])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new LoadStaticField(ConcreteTypeReference("MyClass"), "A"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", IntType)])
                    ])
            },
            {
                "get instance field",
                """
                class MyClass { pub field MyField: int }
                var a = new MyClass { MyField = 1 };
                var b = a.MyField;
                """,
                Module(
                    types: [
                        DataType("MyClass", variants: [Variant("_classVariant", fields: [Field("MyField", IntType)])])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(0, "MyField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadField(0, "MyField"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", IntType)
                            ])
                    ])
            },
            {
                "get instance and static field",
                """
                class MyClass { pub field Ignore: int, pub field InstanceField: string, pub static field StaticField: int = 2 }
                var a = new MyClass { Ignore = 1, InstanceField = "" };
                var b = a.InstanceField;
                var c = MyClass::StaticField;
                """,
                Module(
                    types: [
                        DataType("MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [
                                        Field("Ignore", IntType),
                                        Field("InstanceField", StringType),
                                    ])
                            ],
                            staticFields: [
                                StaticField("StaticField", IntType, [new LoadIntConstant(2)])
                            ])
                    ],
                    methods: [
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("MyClass")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(0, "Ignore"),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "InstanceField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadField(0, "InstanceField"),
                                new StoreLocal("b"),
                                new LoadStaticField(ConcreteTypeReference("MyClass"), "StaticField"),
                                new StoreLocal("c"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", StringType),
                                Local("c", IntType)
                            ])
                    ])
            }
        };
    }
}