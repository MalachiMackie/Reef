using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class ModuleStructure
{
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            { "empty module", "", Module() },
            { "empty class", "class MyClass{}", Module([Class("MyClass")]) },
            { "empty union", "union MyUnion{}", Module([Union("MyUnion")]) },
            {
                "union with unit variants", "union MyUnion{A, B}", Module([
                    Union("MyUnion", [
                        Variant("A", fields: [
                            Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                        ]),
                        Variant("B", fields: [
                            Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                        ])])
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
                        fields:
                        [
                            Field("MyField", ConcreteTypeReference("string"), isPublic: true),
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
                            Variant("A", fields: [
                                Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                            ]),
                            Variant("B",
                                fields:
                                [
                                    Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                                    Field("Item0", ConcreteTypeReference("string"), isPublic: true),
                                    Field("Item1", ConcreteTypeReference("int"), isPublic: true),
                                ]),
                            Variant("C",
                                fields:
                                [
                                    Field("_variantIdentifier", ConcreteTypeReference("int"), isPublic: true),
                                    Field("MyField", ConcreteTypeReference("bool"), isPublic: true)
                                ])
                        ],
                        methods: [
                            Method(
                                "MyUnion_B_Create",
                                isStatic: true,
                                parameters: [
                                    Parameter("Item0", ConcreteTypeReference("string")),
                                    Parameter("Item1", ConcreteTypeReference("int")),
                                ],
                                returnType: ConcreteTypeReference("MyUnion"),
                                instructions: [
                                    new CreateObject(Addr(0), ConcreteTypeReference("MyUnion")),
                                    new CopyStack(Addr(1)),
                                    new LoadIntConstant(Addr(2), 1),
                                    new StoreField(Addr(3), 1, 0),
                                    new CopyStack(Addr(4)),
                                    new LoadArgument(Addr(5), 0),
                                    new StoreField(Addr(6), 1, 1),
                                    new CopyStack(Addr(7)),
                                    new LoadArgument(Addr(8), 1),
                                    new StoreField(Addr(9), 1, 2),
                                    new Return(Addr(10))
                                ])
                        ])
                ])
            },
        };
    }
}
