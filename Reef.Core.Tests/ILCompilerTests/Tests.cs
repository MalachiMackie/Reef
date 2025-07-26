using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests;

public class Tests
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

        var module = ILCompile.CompileToIL(program.ParsedProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }

    [Fact]
    public void SingleTest()
    {
        const string source = """
                              var a = "";
                              SomeFn();

                              fn SomeFn() {
                                  var b = a;
                              }
                              """;
        var expected = Module(
            types:
            [
                Class("SomeFn!Closure",
                    variantName: "ClosureVariant",
                    instanceFields:
                    [
                        Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                    ])
            ],
            methods:
            [
                Method("SomeFn",
                    parameters:
                    [
                        Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
                    ],
                    locals:
                    [
                        Local("b", ConcreteTypeReference("string"))
                    ],
                    instructions:
                    [
                        new LoadArgument(Addr(0), 0),
                        new LoadField(Addr(1), 0, 0),
                        new StoreLocal(Addr(2), 0),
                        LoadUnit(3),
                        Return(4)
                    ]),
                Method("!Main",
                    isStatic: true,
                    locals:
                    [
                        Local("a", ConcreteTypeReference("string"))
                    ],
                    instructions:
                    [
                        new LoadStringConstant(Addr(0), ""),
                        new StoreLocal(Addr(1), 0),
                        new CreateObject(Addr(2), ConcreteTypeReference("SomeFn!Closure")),
                        new CopyStack(Addr(3)),
                        new LoadLocal(Addr(4), 0),
                        new StoreField(Addr(5), 0, 0),
                        new LoadGlobalFunction(Addr(6), FunctionReference("SomeFn")),
                        new Call(Addr(7)),
                        Drop(8),
                        LoadUnit(9),
                        Return(10)
                    ])
            ]);
        
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var module = ILCompile.CompileToIL(program.ParsedProgram);

        module.Should().NotBeNull();
        module.Should().BeEquivalentTo(expected, ConfigureEquivalencyCheck);
    }

    private static EquivalencyOptions<ReefModule> ConfigureEquivalencyCheck(EquivalencyOptions<ReefModule> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid));
    }

    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            { "empty module", "", Module() },
            { "empty class", "class MyClass{}", Module([Class("MyClass")]) },
            { "empty union", "union MyUnion{}", Module([Union("MyUnion")]) },
            {
                "union with unit variants", "union MyUnion{A, B}", Module([
                    Union("MyUnion", [Variant("A"), Variant("B")])
                ])
            },
            {
                "empty top level method",
                "static fn someFn() {}",
                Module(methods:
                [
                    Method("someFn", isStatic: true, instructions: [LoadUnit(0), Return(1)])
                ])
            },
            {
                "empty class static method",
                "class MyClass { static fn SomeFn() {} }",
                Module([
                    Class("MyClass",
                        methods: [Method("SomeFn", isStatic: true, instructions: [LoadUnit(0), Return(1)])])
                ])
            },
            {
                "empty union static method",
                "union MyUnion { static fn SomeFn() {} }",
                Module([
                    Union("MyUnion",
                        methods: [Method("SomeFn", isStatic: true, instructions: [LoadUnit(0), Return(1)])])
                ])
            },
            {
                "method with parameters",
                "static fn SomeFn(a: int, b: string){}",
                Module(methods:
                [
                    Method("SomeFn", isStatic: true, parameters:
                    [
                        Parameter("a", ConcreteTypeReference("int")),
                        Parameter("b", ConcreteTypeReference("string")),
                    ], instructions: [LoadUnit(0), Return(1)])
                ])
            },
            {
                "method with return type",
                "static fn SomeFn(): int {return 1;}",
                Module(methods:
                [
                    Method(
                        "SomeFn",
                        isStatic: true,
                        returnType: ConcreteTypeReference("int"),
                        instructions:
                        [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            Return(1)
                        ])
                ])
            },
            {
                "generic method",
                "static fn SomeFn<T>() {}",
                Module(methods:
                [
                    Method("SomeFn", isStatic: true, typeParameters: ["T"], instructions: [LoadUnit(0), Return(1)])
                ])
            },
            {
                "generic method with type parameter as parameter and return type",
                "static fn SomeFn<T1, T2>(a: T1, b: T2): T2 {return b;}",
                Module(methods:
                [
                    Method("SomeFn",
                        isStatic: true,
                        typeParameters: ["T1", "T2"],
                        parameters:
                        [
                            Parameter("a", GenericTypeReference("T1")),
                            Parameter("b", GenericTypeReference("T2")),
                        ],
                        returnType: GenericTypeReference("T2"),
                        instructions:
                        [
                            new LoadArgument(new InstructionAddress(0), ArgumentIndex: 1),
                            Return(1)
                        ])
                ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                Module(types:
                [
                    Class("MyClass", typeParameters: ["T"])
                ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                Module(types:
                [
                    Union("MyUnion", typeParameters: ["T"])
                ])
            },
            {
                "static method inside generic class",
                "class MyClass<T> { static fn SomeFn(param: T): T{ return param;} }",
                Module(types:
                [
                    Class("MyClass", typeParameters: ["T"], methods:
                    [
                        Method("SomeFn",
                            isStatic: true,
                            parameters: [Parameter("param", GenericTypeReference("T"))],
                            returnType: GenericTypeReference("T"),
                            instructions:
                            [
                                new LoadArgument(new InstructionAddress(0), ArgumentIndex: 0),
                                Return(1)
                            ])
                    ])
                ])
            },
            {
                "static method inside generic union",
                "union MyUnion<T> { static fn SomeFn(param: T): T{ return param;} }",
                Module(types:
                [
                    Union("MyUnion", typeParameters: ["T"], methods:
                    [
                        Method("SomeFn",
                            isStatic: true,
                            parameters: [Parameter("param", GenericTypeReference("T"))],
                            returnType: GenericTypeReference("T"),
                            instructions:
                            [
                                new LoadArgument(new InstructionAddress(0), ArgumentIndex: 0),
                                Return(1)
                            ]
                        )
                    ])
                ])
            },
            {
                "instance method inside class",
                "class MyClass { fn SomeFn(){}}",
                Module(types:
                [
                    Class("MyClass", methods:
                    [
                        Method("SomeFn",
                            isStatic: false,
                            parameters:
                            [
                                Parameter("this", ConcreteTypeReference("MyClass"))
                            ],
                            instructions: [LoadUnit(0), Return(1)])
                    ])
                ])
            },
            {
                "instance method inside union",
                "union MyUnion { fn SomeFn(){}}",
                Module(types:
                [
                    Union("MyUnion", methods:
                    [
                        Method("SomeFn",
                            isStatic: false,
                            parameters:
                            [
                                Parameter("this", ConcreteTypeReference("MyUnion"))
                            ], instructions: [LoadUnit(0), Return(1)])
                    ])
                ])
            },
            {
                "class fields",
                "class MyClass { pub field MyField: string, static field OtherField: int = 1}",
                Module(types:
                [
                    Class("MyClass",
                        instanceFields:
                        [
                            Field("MyField", ConcreteTypeReference("string"), isPublic: true),
                        ],
                        staticFields:
                        [
                            Field("OtherField", ConcreteTypeReference("int"), isStatic: true,
                                staticInitializer: [new LoadIntConstant(new InstructionAddress(0), 1)])
                        ])
                ])
            },
            {
                "union variant fields",
                "union MyUnion { A, B(string, int), C { field MyField: bool } }",
                Module(types:
                [
                    Union("MyUnion",
                        variants:
                        [
                            Variant("A"),
                            Variant("B",
                                instanceFields:
                                [
                                    Field("First", ConcreteTypeReference("string"), isPublic: true),
                                    Field("Second", ConcreteTypeReference("int"), isPublic: true),
                                ]),
                            Variant("C",
                                instanceFields:
                                [
                                    Field("MyField", ConcreteTypeReference("bool"), isPublic: true)
                                ])
                        ])
                ])
            },
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
                    types:
                    [
                        Class("InnerFn!Closure", variantName: "ClosureVariant", instanceFields:
                        [
                            Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            Field("Field_1", ConcreteTypeReference("int"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("SomeFn", isStatic: true, parameters:
                        [
                            Parameter("param", ConcreteTypeReference("int")),
                            Parameter("param2", ConcreteTypeReference("string")),
                        ], instructions: [LoadUnit(0), Return(1)]),
                        Method("InnerFn",
                            isStatic: false,
                            parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("InnerFn!Closure"))
                            ],
                            locals: [
                                new ReefMethod.Local{DisplayName = "a", Type = ConcreteTypeReference("string")}
                            ],
                            returnType: ConcreteTypeReference("int"),
                            instructions:
                            [
                                new LoadArgument(new InstructionAddress(0), 0),
                                new LoadField(new InstructionAddress(1), VariantIndex: 0, FieldIndex: 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(new InstructionAddress(3), 0),
                                new LoadField(new InstructionAddress(4), VariantIndex: 0, FieldIndex: 1),
                                new Return(new InstructionAddress(5))
                            ])
                    ])
            },
            {
                "top level statements - push int",
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
                "top level statements - push constant string",
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
                "top level statements - push constant bool true",
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
                "top level statements - push constant bool false",
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
                "access this in instance method",
                """
                class MyClass {
                    fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("MyClass", methods:
                        [
                            Method("SomeFn",
                                parameters:
                                [
                                    Parameter("this", ConcreteTypeReference("MyClass"))
                                ],
                                locals:
                                [
                                    new ReefMethod.Local { Type = ConcreteTypeReference("MyClass"), DisplayName = "a" },
                                ],
                                instructions:
                                [
                                    new LoadArgument(new InstructionAddress(0), 0),
                                    new StoreLocal(new InstructionAddress(1), 0),
                                    LoadUnit(2),
                                    Return(3)
                                ]
                            )
                        ])
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
                    types:
                    [
                        Class("MyClass",
                            instanceFields:
                            [
                                Field("SomeField", ConcreteTypeReference("string"))
                            ],
                            methods:
                            [
                                Method("SomeFn",
                                    parameters:
                                    [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    locals:
                                    [
                                        new ReefMethod.Local
                                            { Type = ConcreteTypeReference("string"), DisplayName = "a" },
                                    ],
                                    instructions:
                                    [
                                        new LoadArgument(new InstructionAddress(0), 0),
                                        new LoadField(new InstructionAddress(1), 0, 0),
                                        new StoreLocal(new InstructionAddress(2), 0),
                                        LoadUnit(3),
                                        Return(4)
                                    ]
                                )
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
                    types:
                    [
                        Class("MyClass",
                            staticFields:
                            [
                                Field("SomeField", ConcreteTypeReference("string"), isStatic: true,
                                    staticInitializer: [new LoadStringConstant(new InstructionAddress(0), "")])
                            ],
                            methods:
                            [
                                Method("SomeFn",
                                    parameters: [],
                                    locals:
                                    [
                                        new ReefMethod.Local
                                            { Type = ConcreteTypeReference("string"), DisplayName = "a" },
                                    ],
                                    isStatic: true,
                                    instructions:
                                    [
                                        new LoadStaticField(new InstructionAddress(0), ConcreteTypeReference("MyClass"),
                                            0, 0),
                                        new StoreLocal(new InstructionAddress(1), 0),
                                        LoadUnit(2),
                                        Return(3)
                                    ]
                                )
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
                "access parameter in instance method",
                """
                class MyClass {
                    fn SomeFn(param: int) {
                        var a = param;
                    }
                }
                """,
                Module(
                    types:
                    [
                        Class("MyClass",
                            methods:
                            [
                                Method("SomeFn",
                                    parameters:
                                    [
                                        Parameter("this", ConcreteTypeReference("MyClass")),
                                        Parameter("param", ConcreteTypeReference("int")),
                                    ],
                                    locals:
                                    [
                                        new ReefMethod.Local { Type = ConcreteTypeReference("int"), DisplayName = "a" },
                                    ],
                                    instructions:
                                    [
                                        new LoadArgument(new InstructionAddress(0), 1),
                                        new StoreLocal(new InstructionAddress(1), 0),
                                        LoadUnit(2),
                                        Return(3)
                                    ]
                                )
                            ])
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
                    types:
                    [
                        Class("SomeMethod!Closure", variantName: "ClosureVariant", instanceFields:
                        [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("!Main",
                            locals: [new ReefMethod.Local() { DisplayName = "a", Type = ConcreteTypeReference("int") }],
                            isStatic: true,
                            instructions: [LoadUnit(0), Return(1)]),
                        Method("SomeMethod", parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeMethod!Closure")),
                                Parameter("param", ConcreteTypeReference("string"))
                            ], locals:
                            [
                                new ReefMethod.Local { DisplayName = "b", Type = ConcreteTypeReference("string") },
                                new ReefMethod.Local { DisplayName = "c", Type = ConcreteTypeReference("int") },
                            ], instructions:
                            [
                                new LoadArgument(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                new LoadArgument(Addr(2), 0),
                                new LoadField(Addr(3), VariantIndex: 0, FieldIndex: 0),
                                new StoreLocal(Addr(4), LocalIndex: 1),
                                LoadUnit(5),
                                Return(6)
                            ])
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
                    types:
                    [
                        Class("SomeMethod!Closure", variantName: "ClosureVariant", instanceFields:
                        [
                            Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                        ])
                    ],
                    methods:
                    [
                        Method("OuterFn",
                            isStatic: true,
                            parameters: [
                                Parameter("outerParam", ConcreteTypeReference("string"))
                            ],
                            instructions: [LoadUnit(0), Return(1)]),
                        Method("SomeMethod", parameters:
                            [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeMethod!Closure")),
                            ], locals:
                            [
                                new ReefMethod.Local { DisplayName = "a", Type = ConcreteTypeReference("string") },
                            ], instructions:
                            [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ])
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
                        Class("MyClass")
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
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
                        Class("MyClass", instanceFields: [
                            Field("Field1", ConcreteTypeReference("int"), isPublic: true),
                            Field("Field2", ConcreteTypeReference("string"), isPublic: true),
                        ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new CopyStack(Addr(1)),
                                new LoadStringConstant(Addr(2), ""),
                                new StoreField(Addr(3), 0, 1),
                                new CopyStack(Addr(4)),
                                new LoadIntConstant(Addr(5), 2),
                                new StoreField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 0),
                                LoadUnit(8),
                                Return(9)
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
                        Method("FirstFn", isStatic: true, instructions: [LoadUnit(0), Return(1)]),
                        Method("!Main", isStatic: true, instructions: [
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
                        Class("SomeFn!Closure",
                            variantName: "ClosureVariant",
                            instanceFields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods: [
                        Method("SomeFn",
                            parameters: [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
                            ],
                            locals: [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new LoadStringConstant(Addr(0), ""),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("SomeFn!Closure")),
                                new CopyStack(Addr(3)),
                                new LoadLocal(Addr(4), 0),
                                new StoreField(Addr(5), 0, 0),
                                new LoadGlobalFunction(Addr(6), FunctionReference("SomeFn")),
                                new Call(Addr(7)),
                                Drop(8),
                                LoadUnit(9),
                                Return(10)
                            ])
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
                        Class("SomeFn!Closure",
                            variantName: "ClosureVariant",
                            instanceFields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods: [
                        Method("SomeFn",
                            parameters: [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
                            ],
                            locals: [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("Outer",
                            isStatic: true,
                            parameters: [
                                Parameter("a", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("SomeFn!Closure")),
                                new CopyStack(Addr(1)),
                                new LoadArgument(Addr(2), 0),
                                new StoreField(Addr(3), 0, 0),
                                new LoadGlobalFunction(Addr(4), FunctionReference("SomeFn")),
                                new Call(Addr(5)),
                                Drop(6),
                                LoadUnit(7),
                                Return(8)
                            ])
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
                        Class("SomeFn!Closure",
                            variantName: "ClosureVariant",
                            instanceFields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                            ])
                    ],
                    methods: [
                        Method("SomeFn",
                            parameters: [
                                Parameter("ClosureParameter", ConcreteTypeReference("SomeFn!Closure")),
                                Parameter("c", ConcreteTypeReference("int"))
                            ],
                            locals: [
                                Local("b", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                LoadUnit(3),
                                Return(4)
                            ]),
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new LoadStringConstant(Addr(0), ""),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("SomeFn!Closure")),
                                new CopyStack(Addr(3)),
                                new LoadLocal(Addr(4), 0),
                                new StoreField(Addr(5), 0, 0),
                                new LoadIntConstant(Addr(6), 1),
                                new LoadGlobalFunction(Addr(7), FunctionReference("SomeFn")),
                                new Call(Addr(8)),
                                Drop(9),
                                LoadUnit(10),
                                Return(11)
                            ])
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
                        Class("Fourth!Closure",
                            variantName: "ClosureVariant",
                            instanceFields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true),
                                Field("Field_1", ConcreteTypeReference("int"), isPublic: true)
                            ]),
                        Class("Third!Closure",
                            variantName: "ClosureVariant",
                            instanceFields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                            ]),
                        Class("Second!Closure",
                            variantName: "ClosureVariant",
                            instanceFields: [
                                Field("Field_0", ConcreteTypeReference("string"), isPublic: true)
                            ]),
                    ],
                    methods: [
                        Method("First", 
                            parameters: [
                                Parameter("a", ConcreteTypeReference("string"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Second!Closure")),
                                new CopyStack(Addr(1)),
                                new LoadArgument(Addr(2), 0),
                                new StoreField(Addr(3), 0, 0),
                                new LoadGlobalFunction(Addr(4), FunctionReference("Second")),
                                new Call(Addr(5)),
                                LoadUnit(6),
                                Return(7)
                            ]),
                        Method("Second",
                            parameters: [
                                Parameter("ClosureParameter", ConcreteTypeReference("Second!Closure"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("Third!Closure")),
                                new CopyStack(Addr(1)),
                                new LoadArgument(Addr(2), 0),
                                new LoadField(Addr(3), 0, 0),
                                new StoreField(Addr(4), 0, 0),
                                new LoadGlobalFunction(Addr(5), FunctionReference("Third")),
                                new Call(Addr(6)),
                                LoadUnit(7),
                                Return(8)
                            ]),
                        Method("Third",
                            locals: [
                                Local("c", ConcreteTypeReference("int"))
                            ],
                            parameters: [
                                Parameter("ClosureParameter", ConcreteTypeReference("Third!Closure"))
                            ],
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new StoreLocal(Addr(1), 0),
                                new CreateObject(Addr(2), ConcreteTypeReference("Fourth!Closure")),
                                new CopyStack(Addr(3)),
                                new LoadArgument(Addr(4), 0),
                                new LoadField(Addr(5), 0, 0),
                                new StoreField(Addr(6), 0, 0),
                                new CopyStack(Addr(7)),
                                new LoadLocal(Addr(8), 0),
                                new StoreField(Addr(9), 0, 1),
                                new LoadGlobalFunction(Addr(10), FunctionReference("Fourth")),
                                new Call(Addr(11)),
                                LoadUnit(12),
                                Return(13)
                            ]),
                        Method("Fourth",
                            locals: [
                                Local("b", ConcreteTypeReference("string")),
                                Local("d", ConcreteTypeReference("int")),
                            ],
                            parameters: [
                                Parameter("ClosureParameter", ConcreteTypeReference("Fourth!Closure"))
                            ],
                            instructions: [
                                new LoadArgument(Addr(0), 0),
                                new LoadField(Addr(1), 0, 0),
                                new StoreLocal(Addr(2), 0),
                                new LoadArgument(Addr(3), 0),
                                new LoadField(Addr(4), 0, 1),
                                new StoreLocal(Addr(5), 1),
                                LoadUnit(6),
                                Return(7)
                            ]),
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
                        Class(
                            "MyClass",
                            methods: [
                                Method("SomeFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass"))
                                    ],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadTypeFunction(Addr(3), ConcreteTypeReference("MyClass"), 0),
                                new Call(Addr(4)),
                                Drop(5),
                                LoadUnit(6),
                                Return(7)
                            ])
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
                        Class(
                            "MyClass",
                            methods: [
                                Method("SomeFn",
                                    parameters: [
                                        Parameter("this", ConcreteTypeReference("MyClass")),
                                        Parameter("a", ConcreteTypeReference("int")),
                                        Parameter("b", ConcreteTypeReference("string")),
                                    ],
                                    instructions: [
                                        LoadUnit(0),
                                        Return(1)
                                    ])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass"))
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new StoreLocal(Addr(1), 0),
                                new LoadLocal(Addr(2), 0),
                                new LoadIntConstant(Addr(3), 1),
                                new LoadStringConstant(Addr(4), ""),
                                new LoadTypeFunction(Addr(5), ConcreteTypeReference("MyClass"), 0),
                                new Call(Addr(6)),
                                Drop(7),
                                LoadUnit(8),
                                Return(9)
                            ])
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
                        Class("MyClass",
                            methods: [
                                Method("MyFn",
                                    isStatic: true,
                                    parameters: [Parameter("a", ConcreteTypeReference("int"))],
                                    instructions: [LoadUnit(0), Return(1)])
                            ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            instructions: [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadTypeFunction(Addr(1), ConcreteTypeReference("MyClass"), 0),
                                new Call(Addr(2)),
                                Drop(3),
                                LoadUnit(4),
                                Return(5)
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
                        Class("MyClass", staticFields: [
                            Field("A",
                                isStatic: true,
                                isPublic: true,
                                type: ConcreteTypeReference("int"),
                                staticInitializer: [new LoadIntConstant(Addr(0), 1)])
                        ])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions: [
                                new LoadStaticField(Addr(0), ConcreteTypeReference("MyClass"), VariantIndex: 0, FieldIndex: 0),
                                new StoreLocal(Addr(1), 0),
                                LoadUnit(2),
                                Return(3)
                            ])
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
                        Class("MyClass", instanceFields: [Field("MyField", isPublic: true, type: ConcreteTypeReference("int"))])
                    ],
                    methods: [
                        Method("!Main",
                            isStatic: true,
                            locals: [
                                Local("a", ConcreteTypeReference("MyClass")),
                                Local("b", ConcreteTypeReference("int")),
                            ],
                            instructions: [
                                new CreateObject(Addr(0), ConcreteTypeReference("MyClass")),
                                new CopyStack(Addr(1)),
                                new LoadIntConstant(Addr(2), 1),
                                new StoreField(Addr(3), 0, 0),
                                new StoreLocal(Addr(4), 0),
                                new LoadLocal(Addr(5), 0),
                                new LoadField(Addr(6), 0, 0),
                                new StoreLocal(Addr(7), 1),
                                LoadUnit(8),
                                Return(9)
                            ])
                    ])
            }
        };
    }

    private static ReefMethod.Local Local(string name, IReefTypeReference type)
    {
        return new ReefMethod.Local { DisplayName = name, Type = type };
    }
    
    private static FunctionReference FunctionReference(string name) => new()
    {
        Name = name,
        TypeArguments = [],
        DefinitionId = Guid.Empty
    };

    private static ReefField Field(string name, IReefTypeReference type, bool isStatic = false, bool isPublic = false, IReadOnlyList<IInstruction>? staticInitializer = null)
    {
        return new ReefField
        {
            IsStatic = isStatic,
            IsPublic = isPublic,
            Type = type,
            DisplayName = name,
            StaticInitializerInstructions = staticInitializer ?? []
        };
    }

    private static IReefTypeReference GenericTypeReference(string typeParameterName)
    {
        return new GenericReefTypeReference
        {
            TypeParameterName = typeParameterName,
            DefinitionId = Guid.Empty
        };
    }
    
    private static ConcreteReefTypeReference ConcreteTypeReference(string name)
    {
        return new ConcreteReefTypeReference
        {
            Name = name,
            DefinitionId = Guid.Empty,
            TypeArguments = []
        };
    }

    private static ReefMethod.Parameter Parameter(string name, IReefTypeReference typeReference)
    {
        return new ReefMethod.Parameter
        {
            DisplayName = name,
            Type = typeReference
        };
    }

    private static ReefMethod Method(
        string name,
        bool isStatic = false,
        IReadOnlyList<ReefMethod.Parameter>? parameters = null,
        IReefTypeReference? returnType = null,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<IInstruction>? instructions = null,
        IReadOnlyList<ReefMethod.Local>? locals = null)
    {
        return new ReefMethod
        {
            DisplayName = name,
            IsStatic = isStatic,
            TypeParameters = typeParameters ?? [],
            Instructions = instructions ?? [],
            Locals = locals ?? [],
            Parameters = parameters ?? [],
            ReturnType = returnType ?? ConcreteTypeReference("Unit")
        };
    }

    private static ReefVariant Variant(string name,
        IReadOnlyList<ReefField>? instanceFields = null,
        IReadOnlyList<ReefField>? staticFields = null)
    {
        return new ReefVariant
        {
            DisplayName = name,
            InstanceFields = instanceFields ?? [],
            StaticFields = staticFields ?? []
        };
    }

    private static ReefTypeDefinition Union(
        string name,
        IReadOnlyList<ReefVariant>? variants = null,
        IReadOnlyList<ReefMethod>? methods = null,
        IReadOnlyList<string>? typeParameters = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Methods = methods ?? [],
            Id = Guid.Empty,
            IsValueType = false,
            TypeParameters = typeParameters ?? [],
            Variants = variants ?? []
        };
    }

    private static ReefTypeDefinition Class(
        string name,
        string? variantName = null,
        IReadOnlyList<ReefMethod>? methods = null,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<ReefField>? instanceFields = null,
        IReadOnlyList<ReefField>? staticFields = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Id = Guid.Empty,
            Methods = methods ?? [],
            IsValueType = false,
            TypeParameters = typeParameters ?? [],
            Variants = [Variant(variantName ?? "!ClassVariant", instanceFields: instanceFields, staticFields: staticFields)]
        };
    }

    private static ReefModule Module(IReadOnlyList<ReefTypeDefinition>? types = null, IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefModule
        {
            MainMethod = methods?.FirstOrDefault(x => x.DisplayName == "!Main"),
            Methods = methods ?? [],
            Types = types ?? []
        };
    }

    private static LoadUnitConstant LoadUnit(uint address)
    {
        return new LoadUnitConstant(new InstructionAddress(address));
    }

    private static Return Return(uint address)
    {
        return new Return(new InstructionAddress(address));
    }

    private static Drop Drop(uint address)
    {
        return new Drop(new InstructionAddress(address));
    }

    private static InstructionAddress Addr(uint address)
    {
        return new InstructionAddress(address);
    }
}