﻿using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.IL;
using Reef.Core.TypeChecking;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ModuleStructure
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
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
            description);
    }

    private const string _moduleId = "ModuleStructure";
    
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            { "empty module", "", Module() },
            { "empty class", "class MyClass{}", Module(
                types: [DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])])},
            { "empty union", "union MyUnion{}", Module(
                types: [DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion", variants: [])])},
            {
                "union with unit variants", "union MyUnion{A, B}", Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant("A", fields: [Field("_variantIdentifier", IntType)]),
                                Variant("B", fields: [Field("_variantIdentifier", IntType)]),
                            ])
                    ])
            },
            {
                "empty top level method",
                "static fn someFn() {}",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.someFn"), "someFn",
                            instructions: [LoadUnit(), Return()])
                    ])
            },
            {
                "empty class static method",
                "class MyClass { static fn SomeFn() {} }",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            instructions: [
                                LoadUnit(), Return()
                            ])
                    ])
            },
            {
                "empty union static method",
                "union MyUnion { static fn SomeFn() {} }",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion", variants: [])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                            instructions: [LoadUnit(), Return()])
                    ])
            },
            {
                "method with parameters",
                "static fn SomeFn(a: int, b: string){}",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [LoadUnit(), Return()],
                            parameters: [IntType, StringType])
                    ])
            },
            {
                "method with return type",
                "static fn SomeFn(): int {return 1;}",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadIntConstant(1),
                                Return()
                            ],
                            returnType: IntType)
                    ])
            },
            {
                "generic method",
                "static fn SomeFn<T>() {}",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [LoadUnit(), Return()],
                            typeParameters: ["T"])
                    ])
            },
            {
                "generic method with type parameter as parameter and return type",
                "static fn SomeFn<T1, T2>(a: T1, b: T2): T2 {return b;}",
                Module(
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "SomeFn",
                            [
                                new LoadArgument(1),
                                Return()
                            ],
                            parameters: [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "T1"), GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "T2")],
                            typeParameters: ["T1", "T2"],
                            returnType: GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.SomeFn"), "T2"))
                    ])
            },
            {
                "generic class",
                "class MyClass<T>{}",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ])
            },
            {
                "generic union",
                "union MyUnion<T>{}",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion", variants: [], typeParameters: ["T"])
                    ])
            },
            {
                "static method inside generic class",
                "class MyClass<T> { static fn SomeFn(param: T): T{ return param;} }",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")], typeParameters: ["T"])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [
                                new LoadArgument(0),
                                Return()
                            ],
                            parameters: [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"),"T")],
                            returnType: GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "T"),
                            typeParameters: ["T"])
                    ])
            },
            {
                "static method inside generic union",
                "union MyUnion<T> { static fn SomeFn(param: T): T{ return param;} }",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion", variants: [], typeParameters: ["T"])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                            [
                                new LoadArgument(0),
                                Return()
                            ],
                            parameters: [GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T")],
                            returnType: GenericTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "T"),
                            typeParameters: ["T"])
                    ])
            },
            {
                "instance method inside class",
                "class MyClass { fn SomeFn(){}}",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass", variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass")])
                    ])
            },
            {
                "instance method inside union",
                "union MyUnion { fn SomeFn(){}}",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion", variants: [])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__SomeFn"), "MyUnion__SomeFn",
                            [LoadUnit(), Return()],
                            parameters: [ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")])
                    ])
            },
            {
                "class fields",
                "class MyClass { pub field MyField: string, static field OtherField: int = 1}",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyClass"), "MyClass",
                            variants: [
                                Variant("_classVariant",
                                    fields: [
                                        Field("MyField", StringType)
                                    ])
                            ],
                            staticFields: [
                                StaticField("OtherField", IntType, [new LoadIntConstant(1)])
                            ])
                    ])
            },
            {
                "union variant fields",
                "union MyUnion { A, B(string, int), C { field MyField: bool } }",
                Module(
                    types: [
                        DataType(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion",
                            variants: [
                                Variant(
                                    "A",
                                    fields: [Field("_variantIdentifier", IntType)]),
                                Variant(
                                    "B",
                                    fields: [
                                        Field("_variantIdentifier", IntType),
                                        Field("Item0", StringType),
                                        Field("Item1", IntType)
                                    ]),
                                Variant("C",
                                    fields: [
                                        Field("_variantIdentifier", IntType),
                                        Field("MyField", BoolType)
                                    ])
                            ])
                    ],
                    methods: [
                        Method(new DefId(_moduleId, $"{_moduleId}.MyUnion__Create__B"), "MyUnion__Create__B",
                            [
                                new CreateObject(
                                    ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion")),
                                new CopyStack(),
                                new LoadIntConstant(1),
                                new StoreField(1, "_variantIdentifier"),
                                new CopyStack(),
                                new LoadArgument(0),
                                new StoreField(1, "Item0"),
                                new CopyStack(),
                                new LoadArgument(1),
                                new StoreField(1, "Item1"),
                                Return()
                            ],
                            parameters: [StringType, IntType],
                            returnType: ConcreteTypeReference(new DefId(_moduleId, $"{_moduleId}.MyUnion"), "MyUnion"))
                    ])
            },
        };
    }
    
}
