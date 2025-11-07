using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;
using Reef.Core.IL;
using Xunit.Abstractions;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class MethodTests(ITestOutputHelper testOutputHelper)
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
        
        testOutputHelper.WriteLine(PrettyPrinter.PrettyPrintLoweredProgram(loweredProgram, false, false));

        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            opts => opts.Excluding(x => x.Type == typeof(Stack<IReefTypeReference>)),
            description);
    }

    private const string _moduleId = "MethodTests";
    
    public static TheoryData<string, string, ReefILModule> TestCases()
    {
        return new()
        {
            {
                "access parameter",
                """
                static fn SomeFn(a: i64, b: i64) {
                    var foo = a;
                    var bar = b;
                }
                """,
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadArgument(0),
                                new StoreLocal("foo"),
                                new LoadArgument(1),
                                new StoreLocal("bar"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [Int64Type, Int64Type],
                            locals: [
                                Local("foo", Int64Type),
                                Local("bar", Int64Type)
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
                        Method(new DefId(_moduleId, $"{_moduleId}.FirstFn"), "FirstFn",
                            [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"),"_Main",
                            [
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.FirstFn"), "FirstFn")),
                                new Call(0, false),
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__Ignore"),"MyClass__Ignore", [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__StaticFn"),"MyClass__StaticFn", [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__InstanceFn"),
                            "MyClass__InstanceFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"),"_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__InstanceFn"), "MyClass__InstanceFn")),
                                new Call(1, false),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__StaticFn"), "MyClass__StaticFn")),
                                new Call(0, false),
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
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn"),"SomeFn__InnerFn", [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn"), "SomeFn__InnerFn")),
                                new Call(0, false),
                                LoadUnit(),
                                Return()
                            ])
                    ])
            },
            {
                "assign global function to variable",
                """
                fn SomeFn(param: i64) {
                }
                var a = SomeFn;
                a(1);
                """,
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"),"SomeFn",
                            [LoadUnit(), Return()],
                            parameters: [Int64Type]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"),"_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [Int64Type, UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadInt64Constant(1),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [Int64Type, UnitType])),
                                new Call(2, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [Int64Type, UnitType]))
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"),"MyClass__MyFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"),"_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                new StoreLocal("a"),
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new LoadLocal("a"),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("b"),
                                new LoadLocal("b"),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [UnitType])),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")),
                                Local("b", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))
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
                    types: [DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"),"MyClass__MyFn", [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"),"_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [UnitType])),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))
                            ])
                    ])
            },
            {
                "assign static type function with parameters and return type to variable",
                """
                class MyClass { 
                    pub static fn MyFn(a: string, b: i64): bool { return true; }
                }
                var a = MyClass::MyFn;
                a("", 1);
                """,
                Module(
                    types: [DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [new LoadBoolConstant(true), Return()],
                            parameters: [StringType, Int64Type],
                            returnType: BoolType),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(2), "Function`3", [StringType, Int64Type, BoolType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadStringConstant(""),
                                new LoadInt64Constant(1),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(2), "Function`3__Call", [StringType, Int64Type, BoolType])),
                                new Call(3, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(DefId.FunctionObject(2), "Function`3", [StringType, Int64Type, BoolType]))
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")],
                            locals: [Local("a", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [
                                new LoadArgument(0),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")])
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
                        Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [LoadUnit(), Return()], typeParameters: ["T"]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [StringType])),
                                new Call(0, false),
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
                        Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn",
                            [LoadUnit(), Return()],
                            typeParameters: ["T"],
                            parameters: [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "T")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [StringType, UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn", [StringType])),
                                new StoreField(0, "FunctionReference"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("a", ConcreteTypeReference(DefId.FunctionObject(1), "Function`2", [StringType, UnitType]))
                            ])
                    ])
            },
            {
                "call static type generic function",
                """
                class MyClass<T> {
                    pub static fn MyFn<T2>(){}
                }
                MyClass::<i64>::MyFn::<string>();
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn", [LoadUnit(), Return()], typeParameters: ["T", "T2"]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn", [Int64Type, StringType])),
                                new Call(0, false),
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
                var a = new MyClass::<i64>{};
                a.MyFn::<string>();
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T")])],
                            typeParameters: ["T", "T2"]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", [Int64Type])),
                                new StoreLocal("a"),
                                new LoadLocal("a"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn", [Int64Type, StringType])),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", [Int64Type]))])
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
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn",
                            [LoadUnit(), Return()],
                            typeParameters: ["T", "T2"],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T")])]),
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyClass__MyFn"), "MyClass__MyFn", [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T"), StringType])),
                                new Call(1, false),
                                LoadUnit(),
                                Return()
                            ],
                            typeParameters: ["T"],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T")])])
                    ])
            }
        };
    }
}