using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.IL;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;
using Xunit.Abstractions;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ClosureTests(ITestOutputHelper testOutputHelper)
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

    private const string _moduleId = "ClosureTests";
    
    [Fact]
    public void SingleTest()
    {
        var source = """
                var a = "";
                SomeFn();

                fn SomeFn() {
                    var b = a;
                }
                """;
                var expectedModule = Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure",
                            variants: [
                                Variant("_classVariant", 
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure")],
                            locals: [Local("b", StringType)]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadStringConstant(""),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ]);
        
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
            opts => opts.Excluding(x => x.Type == typeof(Stack<IReefTypeReference>)));
    }
    
    public static TheoryData<string, string, ReefILModule> TestCases()
    {
        return new TheoryData<string, string, ReefILModule>
        {
            {
                "fn closure",
                """
                static fn SomeFn(param: i64, param2: string) {
                    fn InnerFn(): i64 {
                        var a = param2;
                        return param;
                    }
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("param", Int64Type),
                                        Field("param2", StringType)
                                    ])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn__Closure"), "SomeFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("SomeFn__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals"))
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn"), "SomeFn__InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "SomeFn__Locals"),
                                new LoadField(0, "param2"),
                                new StoreLocal("a"),
                                new LoadArgument(0),
                                new LoadField(0, "SomeFn__Locals"),
                                new LoadField(0, "param"),
                                new Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn__Closure"), "SomeFn__InnerFn__Closure")],
                            locals: [Local("a", StringType)],
                            returnType: Int64Type),
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "param"),
                                new CopyStack(),
                                new LoadArgument(1),
                                new StoreField(0, "param2"),
                                new StoreLocal("__locals"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals"))
                            ],
                            parameters: [Int64Type, StringType])
                    ])
            },
            {
                "access parameter in closure",
                """
                var a: i64;
                fn SomeMethod(param: string) {
                    var b = param;
                    var c = a;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", Int64Type)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeMethod__Closure"), "SomeMethod__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeMethod"), "SomeMethod",
                            [
                                new LoadArgument(1),
                                new StoreLocal("b"),
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("c"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", StringType), Local("c", Int64Type)],
                            parameters: [
                                ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeMethod__Closure"), "SomeMethod__Closure"),
                                StringType
                            ]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ])
            },
            {
                "access outer parameter in closure",
                """
                static fn OuterFn(outerParam: string) {
                    fn SomeMethod() {
                        var a = outerParam;
                    }
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.OuterFn__Locals"), "OuterFn__Locals",
                            variants: [
                                Variant("_classVariant", [Field("outerParam", StringType)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.OuterFn__SomeMethod__Closure"), "OuterFn__SomeMethod__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("OuterFn__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OuterFn__Locals"), "OuterFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.OuterFn__SomeMethod"), "OuterFn__SomeMethod",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "OuterFn__Locals"),
                                new LoadField(0, "outerParam"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", StringType)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OuterFn__SomeMethod__Closure"), "OuterFn__SomeMethod__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}.OuterFn"), "OuterFn",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OuterFn__Locals"), "OuterFn__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "outerParam"),
                                new StoreLocal("__locals"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OuterFn__Locals"), "OuterFn__Locals"))],
                            parameters: [StringType])
                    ])
            },
            {
                "call closure",
                """
                var a = "";
                SomeFn();

                fn SomeFn() {
                    var b = a;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure",
                            variants: [
                                Variant("_classVariant", 
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure")],
                            locals: [Local("b", StringType)]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadStringConstant(""),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ])
            },
            {
                "call closure that references parameter",
                """
                static fn Outer(a: string) {
                    fn SomeFn() {
                        var b = a;
                    }
                    SomeFn();
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", StringType)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.Outer__SomeFn__Closure"), "Outer__SomeFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("Outer__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.Outer__SomeFn"), "Outer__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "Outer__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", StringType)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__SomeFn__Closure"), "Outer__SomeFn__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}.Outer"), "Outer",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "a"),
                                new StoreLocal("__locals"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__SomeFn__Closure"), "Outer__SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "Outer__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.Outer__SomeFn"), "Outer__SomeFn")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals"))],
                            parameters: [StringType])
                    ])
            },
            {
                "call closure with parameter",
                """
                var a = "";
                SomeFn(1);

                fn SomeFn(c: i64) {
                    var b = a;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure",
                            variants: [
                                Variant("_classVariant", [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", StringType)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure"), Int64Type]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadStringConstant(""),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Closure"), "SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadInt64Constant(1),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn")),
                                new Call(2, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ])
            },
            {
                "closure references two functions out",
                """
                static fn First(a: string) {
                    fn Second() {
                        fn Third() {
                            var c = 1;
                            fn Fourth() {
                                var b = a;
                                var d = c;
                            }
                            
                            Fourth();
                        }
                        Third();
                    }
                    Second();
                }
                """,
            Module(
                types: [
                    DataType(new DefId(_moduleId, $"{_moduleId}.First__Locals"), "First__Locals",
                        variants: [
                            Variant("_classVariant",
                                [Field("a", StringType)])
                        ]),
                    DataType(new DefId(_moduleId, $"{_moduleId}.First__Second__Closure"), "First__Second__Closure",
                        variants: [
                            Variant("_classVariant",
                                [Field("First__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Locals"), "First__Locals"))])
                        ]),
                    DataType(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Locals"), "First__Second__Third__Locals",
                        variants: [
                            Variant("_classVariant",
                                [Field("c", Int32Type)])
                        ]),
                    DataType(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Closure"), "First__Second__Third__Closure",
                        variants: [
                            Variant("_classVariant",
                                [Field("First__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Locals"), "First__Locals"))])
                        ]),
                    DataType(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Fourth__Closure"), "First__Second__Third__Fourth__Closure",
                        variants: [
                            Variant("_classVariant",
                                [
                                    Field("First__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Locals"), "First__Locals")),
                                    Field("First__Second__Third__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Locals"), "First__Second__Third__Locals"))
                                ])
                        ])
                ],
                methods: [
                    Method(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Fourth"), "First__Second__Third__Fourth",
                        [
                            new LoadArgument(0),
                            new LoadField(0, "First__Locals"),
                            new LoadField(0, "a"),
                            new StoreLocal("b"),
                            new LoadArgument(0),
                            new LoadField(0, "First__Second__Third__Locals"),
                            new LoadField(0, "c"),
                            new StoreLocal("d"),
                            LoadUnit(),
                            Return()
                        ],
                        locals: [Local("b", StringType), Local("d", Int32Type)],
                        parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Fourth__Closure"), "First__Second__Third__Fourth__Closure")]),
                    Method(new DefId(_moduleId, $"{_moduleId}.First__Second__Third"), "First__Second__Third",
                        [
                            new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Locals"), "First__Second__Third__Locals")),
                            new StoreLocal("__locals"),
                            new LoadLocal("__locals"),
                            new LoadInt32Constant(1),
                            new StoreField(0, "c"),
                            new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Fourth__Closure"), "First__Second__Third__Fourth__Closure")),
                            new CopyStack(),
                            new LoadArgument(0),
                            new LoadField(0, "First__Locals"),
                            new StoreField(0, "First__Locals"),
                            new CopyStack(),
                            new LoadLocal("__locals"),
                            new StoreField(0, "First__Second__Third__Locals"),
                            new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Fourth"), "First__Second__Third__Fourth")),
                            new Call(1, [], false),
                            LoadUnit(),
                            Return()
                        ],
                        parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Closure"), "First__Second__Third__Closure")],
                        locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Locals"), "First__Second__Third__Locals"))]),
                    Method(new DefId(_moduleId, $"{_moduleId}.First__Second"), "First__Second",
                        [
                            new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third__Closure"), "First__Second__Third__Closure")),
                            new CopyStack(),
                            new LoadArgument(0),
                            new LoadField(0, "First__Locals"),
                            new StoreField(0, "First__Locals"),
                            new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Third"), "First__Second__Third")),
                            new Call(1, [], false),
                            LoadUnit(),
                            Return()
                        ],
                        parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Closure"), "First__Second__Closure")]),
                    Method(new DefId(_moduleId, $"{_moduleId}.First"), "First",
                        [
                            new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Locals"), "First__Locals")),
                            new CopyStack(),
                            new LoadArgument(0),
                            new StoreField(0, "a"),
                            new StoreLocal("__locals"),
                            new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Second__Closure"), "First__Second__Closure")),
                            new CopyStack(),
                            new LoadLocal("__locals"),
                            new StoreField(0, "First__Locals"),
                            new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.First__Second"), "First__Second")),
                            new Call(1, [], false),
                            LoadUnit(),
                            Return()
                        ],
                        parameters: [StringType],
                        locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.First__Locals"), "First__Locals"))])
                ])
            },
            {
                "accessing variable in and out of closure",
                """
                var a = 1;
                var c = a;
                InnerFn();
                fn InnerFn() {
                    var b = a;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("a", Int32Type)
                                    ])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.InnerFn__Closure"), "InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.InnerFn"), "InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn__Closure"), "InnerFn__Closure")],
                            locals: [Local("b", Int32Type)]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadInt32Constant(1),
                                new StoreField(0, "a"),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("c"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn__Closure"), "InnerFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.InnerFn"), "InnerFn")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                Local("c", Int32Type)
                            ])
                    ])
            },
            {
                "multiple closures",
                """
                var a = 1;
                var b = 2;
                var c = 3;
                
                InnerFn1();
                InnerFn1();
                InnerFn2();
                
                fn InnerFn1() {
                    var d = a;
                }
                fn InnerFn2() {
                    var e = b;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant", 
                                    [
                                        Field("a", Int32Type),
                                        Field("b", Int32Type)
                                    ])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.InnerFn1__Closure"), "InnerFn1__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.InnerFn2__Closure"), "InnerFn2__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.InnerFn1"), "InnerFn1",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("d"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("d", Int32Type)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn1__Closure"), "InnerFn1__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}.InnerFn2"), "InnerFn2",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "b"),
                                new StoreLocal("e"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("e", Int32Type)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn2__Closure"), "InnerFn2__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadInt32Constant(1),
                                new StoreField(0, "a"),
                                new LoadLocal("__locals"),
                                new LoadInt32Constant(2),
                                new StoreField(0, "b"),
                                new LoadInt32Constant(3),
                                new StoreLocal("c"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn1__Closure"), "InnerFn1__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.InnerFn1"), "InnerFn1")),
                                new Call(1, [], false),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn1__Closure"), "InnerFn1__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.InnerFn1"), "InnerFn1")),
                                new Call(1, [], false),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn2__Closure"), "InnerFn2__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.InnerFn2"), "InnerFn2")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                Local("c", Int32Type)
                            ])
                    ])
            },
            {
                "parameter referenced in closure",
                """
                static fn SomeFn(a: i64) { 
                    fn InnerFn() {
                        var b = a;
                    }
                    var c = 2;
                    InnerFn();
                    var d = a;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", Int64Type)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn__Closure"), "SomeFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("SomeFn__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn"), "SomeFn__InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "SomeFn__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", Int64Type)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn__Closure"), "SomeFn__InnerFn__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "a"),
                                new StoreLocal("__locals"),
                                new LoadInt32Constant(2),
                                new StoreLocal("c"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn__Closure"), "SomeFn__InnerFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "SomeFn__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__InnerFn"), "SomeFn__InnerFn")),
                                new Call(1, [], false),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("d"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn__Locals"), "SomeFn__Locals")),
                                Local("c", Int32Type),
                                Local("d", Int64Type)
                            ],
                            parameters: [Int64Type])
                    ])
            },
            {
                "closure references variables from multiple functions",
                """
                static fn Outer(a: i64) {
                    var d = a;
                    Inner1();
                    fn Inner1() {
                        var b = 2;
                        Inner2();
                        
                        fn Inner2() {
                            var aa = a;
                            var bb = b;
                        }
                    }
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", Int64Type)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Locals"), "Outer__Inner1__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("b", Int32Type)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Closure"), "Outer__Inner1__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("Outer__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals"))])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Inner2__Closure"), "Outer__Inner1__Inner2__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("Outer__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals")),
                                        Field("Outer__Inner1__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Locals"), "Outer__Inner1__Locals")),
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Inner2"), "Outer__Inner1__Inner2",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "Outer__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("aa"),
                                new LoadArgument(0),
                                new LoadField(0, "Outer__Inner1__Locals"),
                                new LoadField(0, "b"),
                                new StoreLocal("bb"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Inner2__Closure"), "Outer__Inner1__Inner2__Closure")],
                            locals: [
                                Local("aa", Int64Type),
                                Local("bb", Int32Type)
                            ]),
                        Method(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1"), "Outer__Inner1",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Locals"), "Outer__Inner1__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadInt32Constant(2),
                                new StoreField(0, "b"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Inner2__Closure"), "Outer__Inner1__Inner2__Closure")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new LoadField(0, "Outer__Locals"),
                                new StoreField(0, "Outer__Locals"),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "Outer__Inner1__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Inner2"), "Outer__Inner1__Inner2")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Closure"), "Outer__Inner1__Closure")],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Locals"), "Outer__Inner1__Locals")),
                            ]),
                        Method(new DefId(_moduleId, $"{_moduleId}.Outer"), "Outer",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "a"),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("d"),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1__Closure"), "Outer__Inner1__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "Outer__Locals"),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.Outer__Inner1"), "Outer__Inner1")),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [Int64Type],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Outer__Locals"), "Outer__Locals")),
                                Local("d", Int64Type)
                            ])
                    ])
            },
            {
                "mutating referenced value",
                """
                var mut a = 1;
                fn InnerFn() {
                    var b = a;
                    a = 2;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", Int32Type)])
                            ]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.InnerFn__Closure"), "InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.InnerFn"), "InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadInt32Constant(2),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", Int32Type)],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.InnerFn__Closure"), "InnerFn__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadInt32Constant(1),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))
                            ])
                    ])
            },
            {
                "assign closure to variable",
                """
                var a = 1;
                fn Inner() {
                    var b = a;
                }
                var c = Inner;
                c();
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [Variant("_classVariant", [Field("a", Int32Type)])]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.Inner__Closure"), "Inner__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.Inner"), "Inner",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Inner__Closure"), "Inner__Closure")],
                            locals: [Local("b", Int32Type)]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadInt32Constant(1),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.Inner"), "Inner")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.Inner__Closure"), "Inner__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("c"),
                                new LoadLocal("c"),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [UnitType])),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                Local("c", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))
                            ])
                    ])
            },
            {
                "call function variable from closure",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    a();
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [Variant("_classVariant",
                                [Field("a", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))])]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.OtherFn__Closure"), "OtherFn__Closure",
                            variants: [Variant("_classVariant",
                                [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn",
                            [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.OtherFn"), "OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [UnitType])),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OtherFn__Closure"), "OtherFn__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ])
            },
            {
                "reference function variable from closure",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    var b = a;
                }
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [Variant("_classVariant",
                                [Field("a", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))])]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.OtherFn__Closure"), "OtherFn__Closure",
                            variants: [Variant("_classVariant",
                                [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn",
                            [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.OtherFn"), "OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OtherFn__Closure"), "OtherFn__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ])
            },
            {
                "reference function variable from closure and call from locals",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    var b = a;
                }
                a();
                """,
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals",
                            variants: [Variant("_classVariant",
                                [Field("a", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))])]),
                        DataType(new DefId(_moduleId, $"{_moduleId}.OtherFn__Closure"), "OtherFn__Closure",
                            variants: [Variant("_classVariant",
                                [Field("_Main__Locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn",
                            [LoadUnit(), Return()]),
                        Method(new DefId(_moduleId, $"{_moduleId}.OtherFn"), "OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType]))],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.OtherFn__Closure"), "OtherFn__Closure")]),
                        Method(new DefId(_moduleId, $"{_moduleId}._Main"), "_Main",
                            [
                                new CreateObject(ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new CreateObject(ConcreteTypeReference(DefId.FunctionObject(0), "Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference(new DefId(_moduleId, $"{_moduleId}.MyFn"), "MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreField(0, "a"),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new LoadFunction(FunctionDefinitionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [UnitType])),
                                new Call(1, [], false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}._Main__Locals"), "_Main__Locals"))])
                    ])
            }
        };
    }
}