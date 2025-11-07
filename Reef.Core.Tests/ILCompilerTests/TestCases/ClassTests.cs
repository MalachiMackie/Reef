using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.IL;
using Reef.Core.TypeChecking;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;
using static TestHelpers;

public class ClassTests
{
    
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefILModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(_moduleId, tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            opts => opts.Excluding(x => x.Type == typeof(Stack<IReefTypeReference>)),
            description);
    }

    private const string _moduleId = "ClassTests";
    
    public static TheoryData<string, string, ReefILModule> TestCases()
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                new LoadArgument(0),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant", [Field("SomeField", StringType)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "SomeField"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")],
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("SomeField", StringType, [StringConstant("")])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                new LoadStaticField(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"), "SomeField"),
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
                    fn SomeFn(param: i64) {
                        var a = param;
                    }
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                new LoadArgument(1),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"), Int64Type],
                            locals: [Local("a", Int64Type)])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))
                            ])
                    ])
            },
            {
                "create object with fields",
                """
                class MyClass{ pub field Field1: i64, pub field Field2: string}
                var a = new MyClass{Field2 = "", Field1 = 2};
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [
                                        Field("Field1", Int64Type),
                                        Field("Field2", StringType)
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new CopyStack(),
                                new LoadInt64Constant(2),
                                new StoreField(0, "Field1"),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "Field2"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn")),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))])
                    ])
            },
            {
                "call instance method with parameters",
                """
                class MyClass {
                    pub fn SomeFn(a: i64, b: string){}
                }
                var a = new MyClass {};
                a.SomeFn(1, ""); 
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"), Int64Type, StringType]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadInt64Constant(1),
                                new LoadStringConstant(""),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn")),
                                new Call(3, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"))])
                    ])
            },
            {
                "call static class method",
                """
                class MyClass {
                    pub static fn MyFn(a: i64) {
                    }
                }
                MyClass::MyFn(1);
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [Int64Type]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadInt64Constant(1),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "get static field",
                """
                class MyClass { pub static field A: i64 = 1 }
                var a = MyClass::A;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [Variant("_classVariant")],
                            staticFields: [StaticField("A", Int64Type, [new LoadInt64Constant(1)])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadStaticField(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"), "A"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", Int64Type)])
                    ])
            },
            {
                "get instance field",
                """
                class MyClass { pub field MyField: i64 }
                var a = new MyClass { MyField = 1 };
                var b = a.MyField;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant", fields: [Field("MyField", Int64Type)])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new CopyStack(),
                                new LoadInt64Constant(1),
                                new StoreField(0, "MyField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadField(0, "MyField"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                Local("b", Int64Type)
                            ])
                    ])
            },
            {
                "get instance and static field",
                """
                class MyClass { pub field Ignore: i64, pub field InstanceField: string, pub static field StaticField: i64 = 2 }
                var a = new MyClass { Ignore = 1, InstanceField = "" };
                var b = a.InstanceField;
                var c = MyClass::StaticField;
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [
                                        Field("Ignore", Int64Type),
                                        Field("InstanceField", StringType),
                                    ])
                            ],
                            staticFields: [
                                StaticField("StaticField", Int64Type, [new LoadInt64Constant(2)])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new CopyStack(),
                                new LoadInt64Constant(1),
                                new StoreField(0, "Ignore"),
                                new CopyStack(),
                                new LoadStringConstant(""),
                                new StoreField(0, "InstanceField"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadField(0, "InstanceField"),
                                new StoreLocal("b"),
                                new LoadStaticField(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass"), "StaticField"),
                                new StoreLocal("c"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                Local("b", StringType),
                                Local("c", Int64Type)
                            ])
                    ])
            }
        };
    }
}