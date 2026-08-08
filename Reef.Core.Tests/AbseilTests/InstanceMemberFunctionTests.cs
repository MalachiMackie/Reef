using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class InstanceMemberFunctionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task InstanceMemberFunctionAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = await CreateProgram(ModuleId, source);
        var loweredProgram = Lower(program, ModuleId);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    [Fact]
    public async Task SingleTest()
    {
        var source = """
                        union MyUnion {
                            A,
                            pub fn first_fn(){}
                        }
                        var a = boxed MyUnion::A;
                        var b = unboxed MyUnion::A;

                        a.first_fn();
                        b.first_fn();
                        """;
                        var expectedProgram = LoweredProgram(ModuleId,
                            types: [
                                DataType(
                                    ModuleId,
                                    "MyUnion",
                                    variants: [Variant("A", fields: [Field("_variantIdentifier", UInt16T)])]
                                ),
                                DataType(
                                    ModuleId,
                                    "MyUnion__VariantOf",
                                    variants: [Variant("A", fields: [Field("_variantIdentifier", UInt16T)])]
                                ),
                            ],
                            methods: [
                                Method(
                                    new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__unboxed"),
                                    "MyUnion__first_fn__unboxed",
                                    [new BasicBlock(BB0, [], new Return())],
                                    Unit,
                                    parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyUnion", ModuleId)))]),
                                Method(
                                    new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__boxed"),
                                    "MyUnion__first_fn__boxed",
                                    [new BasicBlock(BB0, [], new Return())],
                                    Unit,
                                    parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyUnion", ModuleId))))]),
                                Method(
                                    new DefId(ModuleId, $"{ModuleId}:::_Main"),
                                    "_Main",
                                    [
                                        new BasicBlock(
                                            BB0,
                                            [],
                                            AllocateMethodCall(
                                                BoxedValue(ConcreteTypeReference("MyUnion", ModuleId)),
                                                Local0,
                                                BB1)),
                                        new BasicBlock(
                                            BB1,
                                            [
                                                ..CreateBoxedObject(new Deref(Local0), ConcreteTypeReference("MyUnion", ModuleId)),
                                                new Assign(
                                                    new Field(new Field(new Deref(Local0), "Value", "_classVariant"), "_variantIdentifier", "A"),
                                                    new Use(new UIntConstant(0, 2))
                                                ),
                                                new Assign(
                                                    Local1,
                                                    new CreateObject(ConcreteTypeReference("MyUnion", ModuleId))
                                                ),
                                                new Assign(
                                                    new Field(Local1, "_variantIdentifier", "A"),
                                                    new Use(new UIntConstant(0, 2))
                                                )
                                            ],
                                            new MethodCall(
                                                new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__boxed"), []),
                                                [new Copy(Local0)],
                                                Local2,
                                                BB2)
                                        ),
                                        new BasicBlock(
                                            BB2,
                                            [],
                                            new MethodCall(
                                                new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__unboxed"), []),
                                                [new AddressOf(Local1)],
                                                Local3,
                                                BB3)
                                        ),
                                        new BasicBlock(BB3, [], new Return())
                                    ],
                                    Unit,
                                    locals: [
                                        new MethodLocal("_local0", "a", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyUnion", ModuleId)))),
                                        new MethodLocal("_local1", "b", ConcreteTypeReference("MyUnion", ModuleId)),
                                        new MethodLocal("_local2", null, Unit),
                                        new MethodLocal("_local3", null, Unit),
                                    ]),
                            ]
                        );

        var program = await CreateProgram(ModuleId, source);
        var loweredProgram = Lower(program, ModuleId);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private static readonly ModuleId ModuleId = new("main");

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "instance class function",
                """
                class MyClass{
                    pub fn some_fn(){}
                }
                """,
                LoweredProgram(ModuleId,
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::MyClass__some_fn__boxed"),
                            "MyClass__some_fn__boxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [
                                ("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}:::MyClass__some_fn__unboxed"),
                            "MyClass__some_fn__unboxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [
                                ("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))
                            ]),
                    ])
            },
            {
                "instance union function",
                """
                union MyUnion{
                    pub fn some_fn(){}
                }
                """,
                LoweredProgram(ModuleId,
                    types:
                    [
                        DataType(ModuleId, "MyUnion",
                            variants: []),
                        DataType(ModuleId, "MyUnion__VariantOf")
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::MyUnion__some_fn__boxed"),
                            "MyUnion__some_fn__boxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [
                                ("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyUnion", ModuleId))))
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}:::MyUnion__some_fn__unboxed"),
                            "MyUnion__some_fn__unboxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [
                                ("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyUnion", ModuleId)))
                            ]),
                    ])
            },
            {
                "value access inside instance function",
                """
                class MyClass{
                    pub field some_field: u32,

                    pub fn some_fn() {
                        var a = some_field;
                    }
                }
                """,
                LoweredProgram(ModuleId,
                    types:
                    [
                        DataType(ModuleId, "MyClass",
                            variants: [Variant("_classVariant", fields: [Field("some_field", UInt32T)])])
                    ],
                    methods: [
                        Method(new DefId(ModuleId, $"{ModuleId}:::MyClass__some_fn__boxed"),
                            "MyClass__some_fn__boxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new Use(new Copy(
                                                new Field(
                                                    new Field(new Deref(Param0), "Value", "_classVariant"),
                                                    "some_field", "_classVariant"))))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", "a", UInt32T)],
                            parameters: [
                                ("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))
                            ]),
                        Method(new DefId(ModuleId, $"{ModuleId}:::MyClass__some_fn__unboxed"),
                            "MyClass__some_fn__unboxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            Local0,
                                            new Use(new Copy(
                                                new Field(
                                                    new Deref(Param0),
                                                    "some_field", "_classVariant"))))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", "a", UInt32T)],
                            parameters: [
                                ("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))
                            ]),
                    ])
            },
            {
                "Call function with this as a parameter",
                """
                fn some_fn<T>(val: T) {}
                class MyClass {
                    pub field val: u32,

                    pub fn instance_fn() {
                        some_fn(this);
                    }
                }
                """,
                LoweredProgram(
                    ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant", fields: [Field("val", UInt32T)])])
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::some_fn"),
                            "some_fn",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            typeParameters: [(new DefId(ModuleId, $"{ModuleId}:::some_fn"), "T")],
                            parameters: [("val", new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}:::some_fn"), "T"))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__instance_fn__unboxed"),
                            "MyClass__instance_fn__unboxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::some_fn"), [ConcreteTypeReference("MyClass", ModuleId)]),
                                        [new Copy(new Deref(Param0))],
                                        Local0,
                                        BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))],
                            locals: [new MethodLocal("_local0", null, Unit)]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__instance_fn__boxed"),
                            "MyClass__instance_fn__boxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::some_fn"), [new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId)))]),
                                        [new Copy(Param0)],
                                        Local0,
                                        BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))],
                            locals: [new MethodLocal("_local0", null, Unit)]),
                    ])
            },
            {
                "Assign this externally",
                """
                class Container<T> {
                    pub mut field value: T,
                }

                class MyClass {
                    pub fn store(mut container: Container::<Self>) {
                        container.value = this;
                    }
                }
                """,
                LoweredProgram(ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "Container",
                            typeParameters: ["T"],
                            variants: [Variant("_classVariant", [Field("value", new LoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}:::Container"), "T"))])]),
                        DataType(
                            ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")]),
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__store__unboxed"),
                            "MyClass__store__unboxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            new Field(new Field(new Deref(Param1), "Value", "_classVariant"), "value", "_classVariant"),
                                            new Use(new Copy(new Deref(Param0)))
                                        )
                                    ], new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            parameters: [
                                ("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId))),
                                ("container", new LoweredPointer(BoxedValue(ConcreteTypeReference("Container", ModuleId, [ConcreteTypeReference("MyClass", ModuleId)]))))
                            ]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__store__boxed"),
                            "MyClass__store__boxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(
                                            new Field(new Field(new Deref(Param1), "Value", "_classVariant"), "value", "_classVariant"),
                                            new Use(new Copy(Param0))
                                        )
                                    ], new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            parameters: [
                                ("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId)))),
                                ("container", new LoweredPointer(BoxedValue(ConcreteTypeReference("Container", ModuleId, [new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId)))]))))
                            ]),
                    ])
            },
            {
                "Assign this to variable",
                """
                class MyClass {
                    pub fn some_fn() {
                        var x = this;
                    }
                }
                """,
                LoweredProgram(ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__some_fn__unboxed"),
                            "MyClass__some_fn__unboxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(Local0, new Use(new Copy(new Deref(Param0))))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", "x", ConcreteTypeReference("MyClass", ModuleId))],
                            parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__some_fn__boxed"),
                            "MyClass__some_fn__boxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [
                                        new Assign(Local0, new Use(new Copy(Param0)))
                                    ],
                                    new GoTo(BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", "x", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))],
                            parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))]),
                    ])
            },
            {
                "Call other instance function",
                """
                class MyClass {
                    pub fn first_fn() {}
                    pub fn second_fn() {
                        first_fn();
                    }
                }
                """,
                LoweredProgram(ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__unboxed"),
                            "MyClass__first_fn__unboxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__boxed"),
                            "MyClass__first_fn__boxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__second_fn__unboxed"),
                            "MyClass__second_fn__unboxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__unboxed"), []),
                                        [new Copy(Param0)],
                                        Local0,
                                        BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", null, Unit)],
                            parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__second_fn__boxed"),
                            "MyClass__second_fn__boxed",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__boxed"), []),
                                        [new Copy(Param0)],
                                        Local0,
                                        BB1)),
                                new BasicBlock(BB1, [], new Return())
                            ],
                            Unit,
                            locals: [new MethodLocal("_local0", null, Unit)],
                            parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))]),
                    ])
            },
            {
                "Call class instance function",
                """
                class MyClass {
                    pub fn first_fn(){}
                }
                var a = new boxed MyClass{};
                var b = new unboxed MyClass{};

                a.first_fn();
                b.first_fn();
                """,
                LoweredProgram(ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "MyClass",
                            variants: [Variant("_classVariant")])
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__unboxed"),
                            "MyClass__first_fn__unboxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyClass", ModuleId)))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__boxed"),
                            "MyClass__first_fn__boxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId))))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::_Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        BoxedValue(ConcreteTypeReference("MyClass", ModuleId)),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        ..CreateBoxedObject(new Deref(Local0), ConcreteTypeReference("MyClass", ModuleId)),
                                        new Assign(
                                            Local1,
                                            new CreateObject(ConcreteTypeReference("MyClass", ModuleId))
                                        ),
                                    ],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__boxed"), []),
                                        [new Copy(Local0)],
                                        Local2,
                                        BB2)
                                ),
                                new BasicBlock(
                                    BB2,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyClass__first_fn__unboxed"), []),
                                        [new AddressOf(Local1)],
                                        Local3,
                                        BB3)
                                ),
                                new BasicBlock(BB3, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyClass", ModuleId)))),
                                new MethodLocal("_local1", "b", ConcreteTypeReference("MyClass", ModuleId)),
                                new MethodLocal("_local2", null, Unit),
                                new MethodLocal("_local3", null, Unit),
                            ]),
                    ])
            },
            {
                "Call union instance function",
                """
                union MyUnion {
                    A,
                    pub fn first_fn(){}
                }
                var a = boxed MyUnion::A;
                var b = unboxed MyUnion::A;

                a.first_fn();
                b.first_fn();
                """,
                LoweredProgram(ModuleId,
                    types: [
                        DataType(
                            ModuleId,
                            "MyUnion",
                            variants: [Variant("A", fields: [Field("_variantIdentifier", UInt16T)])]
                        ),
                        DataType(
                            ModuleId,
                            "MyUnion__VariantOf",
                            variants: [Variant("A", fields: [Field("_variantIdentifier", UInt16T)])]
                        )
                    ],
                    methods: [
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__unboxed"),
                            "MyUnion__first_fn__unboxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [("this", new LoweredEphemeralPointer(ConcreteTypeReference("MyUnion", ModuleId)))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__boxed"),
                            "MyUnion__first_fn__boxed",
                            [new BasicBlock(BB0, [], new Return())],
                            Unit,
                            parameters: [("this", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyUnion", ModuleId))))]),
                        Method(
                            new DefId(ModuleId, $"{ModuleId}:::_Main"),
                            "_Main",
                            [
                                new BasicBlock(
                                    BB0,
                                    [],
                                    AllocateMethodCall(
                                        BoxedValue(ConcreteTypeReference("MyUnion", ModuleId)),
                                        Local0,
                                        BB1)),
                                new BasicBlock(
                                    BB1,
                                    [
                                        ..CreateBoxedObject(new Deref(Local0), ConcreteTypeReference("MyUnion", ModuleId)),
                                        new Assign(
                                            new Field(new Field(new Deref(Local0), "Value", "_classVariant"), "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))
                                        ),
                                        new Assign(
                                            Local1,
                                            new CreateObject(ConcreteTypeReference("MyUnion", ModuleId))
                                        ),
                                        new Assign(
                                            new Field(Local1, "_variantIdentifier", "A"),
                                            new Use(new UIntConstant(0, 2))
                                        )
                                    ],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__boxed"), []),
                                        [new Copy(Local0)],
                                        Local2,
                                        BB2)
                                ),
                                new BasicBlock(
                                    BB2,
                                    [],
                                    new MethodCall(
                                        new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}:::MyUnion__first_fn__unboxed"), []),
                                        [new AddressOf(Local1)],
                                        Local3,
                                        BB3)
                                ),
                                new BasicBlock(BB3, [], new Return())
                            ],
                            Unit,
                            locals: [
                                new MethodLocal("_local0", "a", new LoweredPointer(BoxedValue(ConcreteTypeReference("MyUnion", ModuleId)))),
                                new MethodLocal("_local1", "b", ConcreteTypeReference("MyUnion", ModuleId)),
                                new MethodLocal("_local2", null, Unit),
                                new MethodLocal("_local3", null, Unit),
                            ]),
                    ]
                )
            }
        };
    }
}
