using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;
using Reef.IL;
using Xunit.Abstractions;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ClosureTests(ITestOutputHelper testOutputHelper)
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
        
        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }
    
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
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType("_Main__SomeFn__Closure",
                            variants: [
                                Variant("_classVariant", 
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("_Main__SomeFn__Closure")],
                            locals: [Local("b", StringType)]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadStringConstant(""),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference("_Main__SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference("_Main__SomeFn")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
                    ]);
        
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 
        
        testOutputHelper.WriteLine(PrettyPrinter.PrettyPrintLoweredProgram(loweredProgram, false, false));
        
        var (module, _) = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck);
    }
    
    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid));
    }
    
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            {
                "fn closure",
                """
                static fn SomeFn(param: int, param2: string) {
                    fn InnerFn(): int {
                        var a = param2;
                        return param;
                    }
                }
                """,
                Module(
                    types: [
                        DataType("SomeFn__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("param", IntType),
                                        Field("param2", StringType)
                                    ])
                            ]),
                        DataType("SomeFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("SomeFn__Locals", ConcreteTypeReference("SomeFn__Locals"))
                                    ])
                            ])
                    ],
                    methods: [
                        Method("SomeFn__InnerFn",
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
                            parameters: [ConcreteTypeReference("SomeFn__InnerFn__Closure")],
                            locals: [Local("a", StringType)],
                            returnType: IntType),
                        Method("SomeFn",
                            [
                                new CreateObject(ConcreteTypeReference("SomeFn__Locals")),
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
                                Local("__locals", ConcreteTypeReference("SomeFn__Locals"))
                            ],
                            parameters: [IntType, StringType])
                    ])
            },
            {
                "access parameter in closure",
                """
                var a: int;
                fn SomeMethod(param: string) {
                    var b = param;
                    var c = a;
                }
                """,
                Module(
                    types: [
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", IntType)])
                            ]),
                        DataType("_Main__SomeMethod__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__SomeMethod",
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
                            locals: [Local("b", StringType), Local("c", IntType)],
                            parameters: [
                                ConcreteTypeReference("_Main__SomeMethod__Closure"),
                                StringType
                            ]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
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
                        DataType("OuterFn__Locals",
                            variants: [
                                Variant("_classVariant", [Field("outerParam", StringType)])
                            ]),
                        DataType("OuterFn__SomeMethod__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("OuterFn__Locals", ConcreteTypeReference("OuterFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("OuterFn__SomeMethod",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "OuterFn__Locals"),
                                new LoadField(0, "outerParam"),
                                new StoreLocal("a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("a", StringType)],
                            parameters: [ConcreteTypeReference("OuterFn__SomeMethod__Closure")]),
                        Method("OuterFn",
                            [
                                new CreateObject(ConcreteTypeReference("OuterFn__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "outerParam"),
                                new StoreLocal("__locals"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("OuterFn__Locals"))],
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
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType("_Main__SomeFn__Closure",
                            variants: [
                                Variant("_classVariant", 
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("_Main__SomeFn__Closure")],
                            locals: [Local("b", StringType)]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadStringConstant(""),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference("_Main__SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference("_Main__SomeFn")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
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
                        DataType("Outer__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", StringType)])
                            ]),
                        DataType("Outer__SomeFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("Outer__Locals", ConcreteTypeReference("Outer__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("Outer__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "Outer__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", StringType)],
                            parameters: [ConcreteTypeReference("Outer__SomeFn__Closure")]),
                        Method("Outer",
                            [
                                new CreateObject(ConcreteTypeReference("Outer__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "a"),
                                new StoreLocal("__locals"),
                                new CreateObject(ConcreteTypeReference("Outer__SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "Outer__Locals"),
                                new LoadFunction(FunctionDefinitionReference("Outer__SomeFn")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("Outer__Locals"))],
                            parameters: [StringType])
                    ])
            },
            {
                "call closure with parameter",
                """
                var a = "";
                SomeFn(1);

                fn SomeFn(c: int) {
                    var b = a;
                }
                """,
                Module(
                    types: [
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType("_Main__SomeFn__Closure",
                            variants: [
                                Variant("_classVariant", [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__SomeFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", StringType)],
                            parameters: [ConcreteTypeReference("_Main__SomeFn__Closure"), IntType]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadStringConstant(""),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference("_Main__SomeFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadIntConstant(1),
                                new LoadFunction(FunctionDefinitionReference("_Main__SomeFn")),
                                new Call(2, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
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
                    DataType("First__Locals",
                        variants: [
                            Variant("_classVariant",
                                [Field("a", StringType)])
                        ]),
                    DataType("First__Second__Closure",
                        variants: [
                            Variant("_classVariant",
                                [Field("First__Locals", ConcreteTypeReference("First__Locals"))])
                        ]),
                    DataType("First__Second__Third__Locals",
                        variants: [
                            Variant("_classVariant",
                                [Field("c", IntType)])
                        ]),
                    DataType("First__Second__Third__Closure",
                        variants: [
                            Variant("_classVariant",
                                [Field("First__Locals", ConcreteTypeReference("First__Locals"))])
                        ]),
                    DataType("First__Second__Third__Fourth__Closure",
                        variants: [
                            Variant("_classVariant",
                                [
                                    Field("First__Locals", ConcreteTypeReference("First__Locals")),
                                    Field("First__Second__Third__Locals", ConcreteTypeReference("First__Second__Third__Locals"))
                                ])
                        ])
                ],
                methods: [
                    Method("First__Second__Third__Fourth",
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
                        locals: [Local("b", StringType), Local("d", IntType)],
                        parameters: [ConcreteTypeReference("First__Second__Third__Fourth__Closure")]),
                    Method("First__Second__Third",
                        [
                            new CreateObject(ConcreteTypeReference("First__Second__Third__Locals")),
                            new StoreLocal("__locals"),
                            new LoadLocal("__locals"),
                            new LoadIntConstant(1),
                            new StoreField(0, "c"),
                            new CreateObject(ConcreteTypeReference("First__Second__Third__Fourth__Closure")),
                            new CopyStack(),
                            new LoadArgument(0),
                            new LoadField(0, "First__Locals"),
                            new StoreField(0, "First__Locals"),
                            new CopyStack(),
                            new LoadLocal("__locals"),
                            new StoreField(0, "First__Second__Third__Locals"),
                            new LoadFunction(FunctionDefinitionReference("First__Second__Third__Fourth")),
                            new Call(1, 0, false),
                            LoadUnit(),
                            Return()
                        ],
                        parameters: [ConcreteTypeReference("First__Second__Third__Closure")],
                        locals: [Local("__locals", ConcreteTypeReference("First__Second__Third__Locals"))]),
                    Method("First__Second",
                        [
                            new CreateObject(ConcreteTypeReference("First__Second__Third__Closure")),
                            new CopyStack(),
                            new LoadArgument(0),
                            new LoadField(0, "First__Locals"),
                            new StoreField(0, "First__Locals"),
                            new LoadFunction(FunctionDefinitionReference("First__Second__Third")),
                            new Call(1, 0, false),
                            LoadUnit(),
                            Return()
                        ],
                        parameters: [ConcreteTypeReference("First__Second__Closure")]),
                    Method("First",
                        [
                            new CreateObject(ConcreteTypeReference("First__Locals")),
                            new CopyStack(),
                            new LoadArgument(0),
                            new StoreField(0, "a"),
                            new StoreLocal("__locals"),
                            new CreateObject(ConcreteTypeReference("First__Second__Closure")),
                            new CopyStack(),
                            new LoadLocal("__locals"),
                            new StoreField(0, "First__Locals"),
                            new LoadFunction(FunctionDefinitionReference("First__Second")),
                            new Call(1, 0, false),
                            LoadUnit(),
                            Return()
                        ],
                        parameters: [StringType],
                        locals: [Local("__locals", ConcreteTypeReference("First__Locals"))])
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
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("a", IntType)
                                    ])
                            ]),
                        DataType("_Main__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("_Main__InnerFn__Closure")],
                            locals: [Local("b", IntType)]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadIntConstant(1),
                                new StoreField(0, "a"),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("c"),
                                new CreateObject(ConcreteTypeReference("_Main__InnerFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference("_Main__InnerFn")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("_Main__Locals")),
                                Local("c", IntType)
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
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant", 
                                    [
                                        Field("a", IntType),
                                        Field("b", IntType)
                                    ])
                            ]),
                        DataType("_Main__InnerFn1__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ]),
                        DataType("_Main__InnerFn2__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__InnerFn1",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("d"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("d", IntType)],
                            parameters: [ConcreteTypeReference("_Main__InnerFn1__Closure")]),
                        Method("_Main__InnerFn2",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "b"),
                                new StoreLocal("e"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("e", IntType)],
                            parameters: [ConcreteTypeReference("_Main__InnerFn2__Closure")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadIntConstant(1),
                                new StoreField(0, "a"),
                                new LoadLocal("__locals"),
                                new LoadIntConstant(2),
                                new StoreField(0, "b"),
                                new LoadIntConstant(3),
                                new StoreLocal("c"),
                                new CreateObject(ConcreteTypeReference("_Main__InnerFn1__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference("_Main__InnerFn1")),
                                new Call(1, 0, false),
                                new CreateObject(ConcreteTypeReference("_Main__InnerFn1__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference("_Main__InnerFn1")),
                                new Call(1, 0, false),
                                new CreateObject(ConcreteTypeReference("_Main__InnerFn2__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new LoadFunction(FunctionDefinitionReference("_Main__InnerFn2")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("_Main__Locals")),
                                Local("c", IntType)
                            ])
                    ])
            },
            {
                "parameter referenced in closure",
                """
                static fn SomeFn(a: int) { 
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
                        DataType("SomeFn__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", IntType)])
                            ]),
                        DataType("SomeFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("SomeFn__Locals", ConcreteTypeReference("SomeFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("SomeFn__InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "SomeFn__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", IntType)],
                            parameters: [ConcreteTypeReference("SomeFn__InnerFn__Closure")]),
                        Method("SomeFn",
                            [
                                new CreateObject(ConcreteTypeReference("SomeFn__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "a"),
                                new StoreLocal("__locals"),
                                new LoadIntConstant(2),
                                new StoreLocal("c"),
                                new CreateObject(ConcreteTypeReference("SomeFn__InnerFn__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "SomeFn__Locals"),
                                new LoadFunction(FunctionDefinitionReference("SomeFn__InnerFn")),
                                new Call(1, 0, false),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("d"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("SomeFn__Locals")),
                                Local("c", IntType),
                                Local("d", IntType)
                            ],
                            parameters: [IntType])
                    ])
            },
            {
                "closure references variables from multiple functions",
                """
                static fn Outer(a: int) {
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
                        DataType("Outer__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", IntType)])
                            ]),
                        DataType("Outer__Inner1__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("b", IntType)])
                            ]),
                        DataType("Outer__Inner1__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("Outer__Locals", ConcreteTypeReference("Outer__Locals"))])
                            ]),
                        DataType("Outer__Inner1__Inner2__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [
                                        Field("Outer__Locals", ConcreteTypeReference("Outer__Locals")),
                                        Field("Outer__Inner1__Locals", ConcreteTypeReference("Outer__Inner1__Locals")),
                                    ])
                            ])
                    ],
                    methods: [
                        Method("Outer__Inner1__Inner2",
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
                            parameters: [ConcreteTypeReference("Outer__Inner1__Inner2__Closure")],
                            locals: [
                                Local("aa", IntType),
                                Local("bb", IntType)
                            ]),
                        Method("Outer__Inner1",
                            [
                                new CreateObject(ConcreteTypeReference("Outer__Inner1__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadIntConstant(2),
                                new StoreField(0, "b"),
                                new CreateObject(ConcreteTypeReference("Outer__Inner1__Inner2__Closure")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new LoadField(0, "Outer__Locals"),
                                new StoreField(0, "Outer__Locals"),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "Outer__Inner1__Locals"),
                                new LoadFunction(FunctionDefinitionReference("Outer__Inner1__Inner2")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("Outer__Inner1__Closure")],
                            locals: [
                                Local("__locals", ConcreteTypeReference("Outer__Inner1__Locals")),
                            ]),
                        Method("Outer",
                            [
                                new CreateObject(ConcreteTypeReference("Outer__Locals")),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(0, "a"),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("d"),
                                new CreateObject(ConcreteTypeReference("Outer__Inner1__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "Outer__Locals"),
                                new LoadFunction(FunctionDefinitionReference("Outer__Inner1")),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [IntType],
                            locals: [
                                Local("__locals", ConcreteTypeReference("Outer__Locals")),
                                Local("d", IntType)
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
                        DataType("_Main__Locals",
                            variants: [
                                Variant("_classVariant",
                                    [Field("a", IntType)])
                            ]),
                        DataType("_Main__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__InnerFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadIntConstant(2),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", IntType)],
                            parameters: [ConcreteTypeReference("_Main__InnerFn__Closure")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadIntConstant(1),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("_Main__Locals"))
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
                        DataType("_Main__Locals",
                            variants: [Variant("_classVariant", [Field("a", IntType)])]),
                        DataType("_Main__Inner__Closure",
                            variants: [
                                Variant("_classVariant",
                                    [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])
                            ])
                    ],
                    methods: [
                        Method("_Main__Inner",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("_Main__Inner__Closure")],
                            locals: [Local("b", IntType)]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new LoadIntConstant(1),
                                new StoreField(0, "a"),
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("_Main__Inner")),
                                new StoreField(0, "FunctionReference"),
                                new CopyStack(),
                                new CreateObject(ConcreteTypeReference("_Main__Inner__Closure")),
                                new CopyStack(),
                                new LoadLocal("__locals"),
                                new StoreField(0, "_Main__Locals"),
                                new StoreField(0, "FunctionParameter"),
                                new StoreLocal("c"),
                                new LoadLocal("c"),
                                new LoadFunction(FunctionDefinitionReference("Function`1__Call", [UnitType])),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("_Main__Locals")),
                                Local("c", ConcreteTypeReference("Function`1", [UnitType]))
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
                        DataType("_Main__Locals",
                            variants: [Variant("_classVariant",
                                [Field("a", ConcreteTypeReference("Function`1", [UnitType]))])]),
                        DataType("_Main__OtherFn__Closure",
                            variants: [Variant("_classVariant",
                                [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])])
                    ],
                    methods: [
                        Method("MyFn",
                            [LoadUnit(), Return()]),
                        Method("_Main__OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new LoadFunction(FunctionDefinitionReference("Function`1__Call", [UnitType])),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            parameters: [ConcreteTypeReference("_Main__OtherFn__Closure")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
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
                        DataType("_Main__Locals",
                            variants: [Variant("_classVariant",
                                [Field("a", ConcreteTypeReference("Function`1", [UnitType]))])]),
                        DataType("_Main__OtherFn__Closure",
                            variants: [Variant("_classVariant",
                                [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])])
                    ],
                    methods: [
                        Method("MyFn",
                            [LoadUnit(), Return()]),
                        Method("_Main__OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", ConcreteTypeReference("Function`1", [UnitType]))],
                            parameters: [ConcreteTypeReference("_Main__OtherFn__Closure")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreField(0, "a"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
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
                        DataType("_Main__Locals",
                            variants: [Variant("_classVariant",
                                [Field("a", ConcreteTypeReference("Function`1", [UnitType]))])]),
                        DataType("_Main__OtherFn__Closure",
                            variants: [Variant("_classVariant",
                                [Field("_Main__Locals", ConcreteTypeReference("_Main__Locals"))])])
                    ],
                    methods: [
                        Method("MyFn",
                            [LoadUnit(), Return()]),
                        Method("_Main__OtherFn",
                            [
                                new LoadArgument(0),
                                new LoadField(0, "_Main__Locals"),
                                new LoadField(0, "a"),
                                new StoreLocal("b"),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("b", ConcreteTypeReference("Function`1", [UnitType]))],
                            parameters: [ConcreteTypeReference("_Main__OtherFn__Closure")]),
                        Method("_Main",
                            [
                                new CreateObject(ConcreteTypeReference("_Main__Locals")),
                                new StoreLocal("__locals"),
                                new LoadLocal("__locals"),
                                new CreateObject(ConcreteTypeReference("Function`1", [UnitType])),
                                new CopyStack(),
                                new LoadFunction(FunctionDefinitionReference("MyFn")),
                                new StoreField(0, "FunctionReference"),
                                new StoreField(0, "a"),
                                new LoadLocal("__locals"),
                                new LoadField(0, "a"),
                                new LoadFunction(FunctionDefinitionReference("Function`1__Call", [UnitType])),
                                new Call(1, 0, false),
                                LoadUnit(),
                                Return()
                            ],
                            locals: [Local("__locals", ConcreteTypeReference("_Main__Locals"))])
                    ])
            }
        };
    }
}