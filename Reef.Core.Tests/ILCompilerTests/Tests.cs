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
        const string source = "var a: int;var b: string";
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var module = ILCompile.CompileToIL(program.ParsedProgram);

        module.Should().NotBeNull();

        var expected = Module(methods: [
            Method("!Main",
                isStatic: true,
                instructions: [],
                locals: [
                    new ReefMethod.Local {Type = ConcreteTypeReference("int"), DisplayName = "a"},
                    new ReefMethod.Local {Type = ConcreteTypeReference("string"), DisplayName = "b"},
                ])
        ]);
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
            {"empty module", "", Module() },
            {"empty class", "class MyClass{}", Module([Class("MyClass")])},
            {"empty union", "union MyUnion{}", Module([Union("MyUnion")])},
            {"union with unit variants", "union MyUnion{A, B}", Module([
                Union("MyUnion", [Variant("A"), Variant("B")])
            ])},
            {
                "empty top level method",
                "static fn someFn() {}",
                Module(methods: [
                    Method("someFn", isStatic: true)
                ])
            },
            {
                "empty class static method",
                "class MyClass { static fn SomeFn() {} }",
                Module([
                    Class("MyClass", methods: [Method("SomeFn", isStatic: true)])
                ])
            },
            {
                "empty union static method",
                "union MyUnion { static fn SomeFn() {} }",
                Module([
                    Union("MyUnion", methods: [Method("SomeFn", isStatic: true)])
                ])
            },
            {
                "method with parameters",
                "static fn SomeFn(a: int, b: string){}",
                Module(methods: [
                    Method("SomeFn", isStatic: true, parameters: [
                        Parameter("a", ConcreteTypeReference("int")),
                        Parameter("b", ConcreteTypeReference("string")),
                    ])
                ])
            },
            {
                "method with return type",
                "static fn SomeFn(): int {return 1;}",
                Module(methods: [
                    Method("SomeFn", isStatic: true, returnType: ConcreteTypeReference("int"))
                ])
            },
            {
                "generic method",
                "static fn SomeFn<T>() {}",
                Module(methods: [
                    Method("SomeFn", isStatic: true, typeParameters: ["T"])
                ])
            },
            {
                "generic method with type parameter as parameter and return type",
                "static fn SomeFn<T1, T2>(a: T1, b: T2): T2 {return b;}",
                Module(methods: [
                    Method("SomeFn",
                        isStatic: true,
                        typeParameters: ["T1", "T2"],
                        parameters: [
                            Parameter("a", GenericTypeReference("T1")),
                            Parameter("b", GenericTypeReference("T2")),
                        ],
                        returnType: GenericTypeReference("T2"))
                ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                Module(types: [
                    Class("MyClass", typeParameters: ["T"])
                ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                Module(types: [
                    Union("MyUnion", typeParameters: ["T"])
                ])
            },
            {
                "static method inside generic class",
                "class MyClass<T> { static fn SomeFn(param: T): T{ return param;} }",
                Module(types: [
                    Class("MyClass", typeParameters: ["T"], methods: [
                        Method("SomeFn",
                            isStatic: true,
                            parameters: [Parameter("param", GenericTypeReference("T"))],
                            returnType: GenericTypeReference("T"))
                    ])
                ])
            },
            {
                "static method inside generic union",
                "union MyUnion<T> { static fn SomeFn(param: T): T{ return param;} }",
                Module(types: [
                    Union("MyUnion", typeParameters: ["T"], methods: [
                        Method("SomeFn",
                            isStatic: true,
                            parameters: [Parameter("param", GenericTypeReference("T"))],
                            returnType: GenericTypeReference("T"))
                    ])
                ])
            },
            {
                "instance method inside class",
                "class MyClass { fn SomeFn(){}}",
                Module(types: [
                    Class("MyClass", methods: [
                        Method("SomeFn",
                            isStatic: false,
                            parameters: [
                                Parameter("this", ConcreteTypeReference("MyClass"))
                            ])
                    ])
                ])
            },
            {
                "instance method inside union",
                "union MyUnion { fn SomeFn(){}}",
                Module(types: [
                    Union("MyUnion", methods: [
                        Method("SomeFn",
                            isStatic: false,
                            parameters: [
                                Parameter("this", ConcreteTypeReference("MyUnion"))
                            ])
                    ])
                ])
            },
            {
                "class fields",
                "class MyClass { pub field MyField: string, static field OtherField: int = 1}",
                Module(types: [
                    Class("MyClass", 
                        instanceFields: [
                            Field("MyField", ConcreteTypeReference("string"), isPublic: true),
                        ],
                        staticFields: [
                            Field("OtherField", ConcreteTypeReference("int"), isStatic: true, staticInitializer: [new LoadIntConstant(new InstructionAddress(0), 1)])
                        ])
                ])
            },
            {
                "union variant fields",
                "union MyUnion { A, B(string, int), C { field MyField: bool } }",
                Module(types: [
                    Union("MyUnion",
                        variants: [
                            Variant("A"),
                            Variant("B",
                                instanceFields: [
                                    Field("First", ConcreteTypeReference("string"), isPublic: true),
                                    Field("Second", ConcreteTypeReference("int"), isPublic: true),
                                ]),
                            Variant("C",
                                instanceFields: [
                                    Field("MyField", ConcreteTypeReference("bool"), isPublic: true)
                                ])
                        ])
                ])
            },
            {
                "fn closure",
                """
                static fn SomeFn(param: int) {
                    fn InnerFn(): int {
                        return param;
                    }
                }
                """,
                Module(
                    types: [
                        Class("InnerFn!Closure", variantName: "ClosureVariant", instanceFields: [
                            Field("Field_0", ConcreteTypeReference("int"), isPublic: true)
                        ])
                    ],
                    methods: [
                        Method("SomeFn", isStatic: true, parameters: [
                            Parameter("param", ConcreteTypeReference("int"))
                        ]),
                        Method("InnerFn", isStatic: false, parameters: [
                            Parameter("ClosureParameter", ConcreteTypeReference("InnerFn!Closure"))
                        ], returnType: ConcreteTypeReference("int"))
                    ])
            },
            {
                "top level statements - push int",
                "1",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadIntConstant(new InstructionAddress(0), 1)
                        ])
                ])
            },
            {
                "top level statements - push constant string",
                "\"someString\"",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadStringConstant(new InstructionAddress(0), "someString")
                        ])
                ])
            },
            {
                "top level statements - push constant bool true",
                "true",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadBoolConstant(new InstructionAddress(0), true)
                        ])
                ])
            },
            {
                "top level statements - push constant bool false",
                "false",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadBoolConstant(new InstructionAddress(0), false)
                        ])
                ])
            },
            {
                "variable declaration without initializer",
                "var a: int",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [],
                        locals: [
                            new ReefMethod.Local {Type = ConcreteTypeReference("int"), DisplayName = "a"}
                        ])
                ])
            },
            {
                "two variable declarations without initializers",
                "var a: int;var b: string",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [],
                        locals: [
                            new ReefMethod.Local {Type = ConcreteTypeReference("int"), DisplayName = "a"},
                            new ReefMethod.Local {Type = ConcreteTypeReference("string"), DisplayName = "b"},
                        ])
                ])
            },
            {
                "variable declaration with value initializer",
                "var a = 1",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new StoreLocal(new InstructionAddress(1), 0)
                        ],
                        locals: [
                            new ReefMethod.Local {Type = ConcreteTypeReference("int"), DisplayName = "a"},
                        ])
                ])
            },
            {
                "two variable declarations with value initializers",
                "var a = 1;var b = \"hello\"",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new StoreLocal(new InstructionAddress(1), 0),
                            new LoadStringConstant(new InstructionAddress(2), "hello"),
                            new StoreLocal(new InstructionAddress(3), 1),
                        ],
                        locals: [
                            new ReefMethod.Local {Type = ConcreteTypeReference("int"), DisplayName = "a"},
                            new ReefMethod.Local {Type = ConcreteTypeReference("string"), DisplayName = "b"},
                        ])
                ])
            },
            {
                "less than",
                "var a = 1 < 2",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new LoadIntConstant(new InstructionAddress(1), 2),
                            new CompareIntLessThan(new InstructionAddress(2)),
                            new StoreLocal(new InstructionAddress(3), 0),
                        ],
                        locals: [
                            new ReefMethod.Local {Type = ConcreteTypeReference("bool"), DisplayName = "a"},
                        ])
                ])
            },
            {
                "greater than",
                "var a = 1 > 2",
                Module(methods: [
                    Method("!Main",
                        isStatic: true,
                        instructions: [
                            new LoadIntConstant(new InstructionAddress(0), 1),
                            new LoadIntConstant(new InstructionAddress(1), 2),
                            new CompareIntGreaterThan(new InstructionAddress(2)),
                            new StoreLocal(new InstructionAddress(3), 0),
                        ],
                        locals: [
                            new ReefMethod.Local {Type = ConcreteTypeReference("bool"), DisplayName = "a"},
                        ])
                ])
            },
        };
    }

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
    
    private static IReefTypeReference ConcreteTypeReference(string name)
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
}