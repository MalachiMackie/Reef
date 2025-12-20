using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ClosureTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClosureAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
    [Fact]
    public void SingleTest()
    {
        var source = """
                     class MyClass
                     {
                         field MyField: string,

                         fn MyFn(param: string)
                         {
                             var a = "";
                             fn MiddleFn(b: i64)
                             {
                                 fn InnerFn()
                                 {
                                     var _a = a;
                                     var _b = b;
                                     var _param = param;
                                     var _myField = MyField;
                                 }
                                 InnerFn();
                             }
                             MiddleFn(3);
                         }
                     }
                     """;
        var expectedProgram = LoweredProgram(
            types:
            [
                DataType(ModuleId, "MyClass",
                    variants:
                    [
                        Variant("_classVariant", [Field("MyField", StringT)])
                    ]),
                DataType(ModuleId, "MyClass__MyFn__Locals",
                    variants:
                    [
                        Variant(
                            "_classVariant",
                            [
                                Field("param", StringT),
                                Field("a", StringT)
                            ])
                    ]),
                DataType(ModuleId, "MyClass__MyFn__MiddleFn__Locals",
                    variants:
                    [
                        Variant("_classVariant", [Field("b", Int64T)])
                    ]),
                DataType(ModuleId, "MyClass__MyFn__MiddleFn__Closure",
                    variants:
                    [
                        Variant(
                            "_classVariant",
                            [
                                Field("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                Field("MyClass__MyFn__Locals",
                                    new LoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
                            ])
                    ]),
                DataType(ModuleId, "MyClass__MyFn__MiddleFn__InnerFn__Closure",
                    variants:
                    [
                        Variant(
                            "_classVariant",
                            [
                                Field("this",
                                    new LoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                Field("MyClass__MyFn__Locals",
                                    new LoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                Field("MyClass__MyFn__MiddleFn__Locals",
                                    new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))
                            ])
                    ])
            ],
            methods:
            [
                Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"),
                    "MyClass__MyFn__MiddleFn__InnerFn",
                    [
                        new BasicBlock(
                            new BasicBlockId("bb0"),
                            [
                                new Assign(
                                    new Local("_local0"),
                                    new Use(new Copy(
                                        new Field(
                                            new Field(
                                                new Local("_param0"),
                                                "MyClass__MyFn__Locals",
                                                "_classVariant"),
                                            "a",
                                            "_classVariant")))),
                                new Assign(
                                    new Local("_local1"),
                                    new Use(new Copy(
                                        new Field(
                                            new Field(
                                                new Local("_param0"),
                                                "MyClass__MyFn__MiddleFn__Locals",
                                                "_classVariant"),
                                            "b",
                                            "_classVariant")))),
                                new Assign(
                                    new Local("_local2"),
                                    new Use(new Copy(
                                        new Field(
                                            new Field(
                                                new Local("_param0"),
                                                "MyClass__MyFn__Locals",
                                                "_classVariant"),
                                            "param",
                                            "_classVariant")))),
                                new Assign(
                                    new Local("_local3"),
                                    new Use(new Copy(
                                        new Field(
                                            new Field(
                                                new Local("_param0"),
                                                "this",
                                                "_classVariant"),
                                            "MyField",
                                            "_classVariant"))))
                            ],
                            new GoTo(new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                    ],
                    Unit,
                    locals:
                    [
                        new MethodLocal("_local0", "_a", StringT),
                        new MethodLocal("_local1", "_b", Int64T),
                        new MethodLocal("_local2", "_param", StringT),
                        new MethodLocal("_local3", "_myField", StringT)
                    ],
                    parameters:
                    [
                        ("closure",
                            new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                    ]),
                Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), "MyClass__MyFn__MiddleFn",
                    [
                        new BasicBlock(
                            new BasicBlockId("bb0"),
                            [
                                new Assign(
                                    new Local("_localsObject"),
                                    new CreateObject(
                                        new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals",
                                            new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))),
                                new Assign(
                                    new Field(
                                        new Local("_localsObject"),
                                        "b",
                                        "_classVariant"),
                                    new Use(new Copy(new Local("_param1")))),
                                new Assign(
                                    new Local("_local2"),
                                    new CreateObject(new LoweredConcreteTypeReference(
                                        "MyClass__MyFn__MiddleFn__InnerFn__Closure",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"),
                                        []))),
                                new Assign(
                                    new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                    new Use(new Copy(new Field(new Local("_param0"), "MyClass__MyFn__Locals",
                                        "_classVariant")))),
                                new Assign(
                                    new Field(new Local("_local2"), "MyClass__MyFn__MiddleFn__Locals", "_classVariant"),
                                    new Use(new Copy(new Local("_localsObject")))),
                                new Assign(
                                    new Field(new Local("_local2"), "this", "_classVariant"),
                                    new Use(new Copy(new Field(new Local("_param0"), "this", "_classVariant")))),
                            ],
                            new MethodCall(
                                new LoweredFunctionReference(
                                    new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), []),
                                [new Copy(new Local("_local2"))],
                                new Local("_local1"),
                                new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                    ],
                    Unit,
                    locals:
                    [
                        new MethodLocal("_localsObject", null,
                            new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])),
                        new MethodLocal("_local1", null, Unit),
                        new MethodLocal("_local2", null,
                            new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                    ],
                    parameters:
                    [
                        ("closure",
                            new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), [])),
                        ("b", Int64T)
                    ]),
                Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                    [
                        new BasicBlock(
                            new BasicBlockId("bb0"),
                            [
                                new Assign(
                                    new Local("_localsObject"),
                                    new CreateObject(new LoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
                                new Assign(
                                    new Field(new Local("_localsObject"), "param", "_classVariant"),
                                    new Use(new Copy(new Local("_param1")))),
                                new Assign(
                                    new Field(new Local("_localsObject"), "a", "_classVariant"),
                                    new Use(new StringConstant(""))),
                                new Assign(
                                    new Local("_local2"),
                                    new CreateObject(
                                        new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure",
                                            new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))),
                                new Assign(
                                    new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                    new Use(new Copy(new Local("_localsObject")))),
                                new Assign(
                                    new Field(new Local("_local2"), "this", "_classVariant"),
                                    new Use(new Copy(new Local("_param0"))))
                            ],
                            new MethodCall(
                                new LoweredFunctionReference(
                                    new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), []),
                                [
                                    new Copy(new Local("_local2")),
                                    new IntConstant(3, 8)
                                ],
                                new Local("_local1"),
                                new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                    ],
                    Unit,
                    locals:
                    [
                        new MethodLocal("_localsObject", null,
                            new LoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                        new MethodLocal("_local1", null, Unit),
                        new MethodLocal("_local2", null,
                            new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))
                    ],
                    parameters:
                    [
                        ("this",
                            new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                [])),
                        ("param", StringT)
                    ])
            ]);
        
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ClosureTests";
    
    public static TheoryData<string, string, LoweredModule> TestCases()
    {
        return new()
        {
             {
                 "Closure accesses local variable",
                 """
                 fn MyFn()
                 {
                     var a = "";
                     fn InnerFn() {
                         var b = a;
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyFn__Locals",
                             variants: [
                                 Variant("_classVariant", [Field("a", StringT)])
                             ]),
                         DataType(ModuleId, "MyFn__InnerFn__Closure",
                             variants: [
                                 Variant("_classVariant", [Field("MyFn__Locals", new LoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyFn"), "MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_localsObject"),
                                                 "a",
                                                 "_classVariant"),
                                             new Use(new StringConstant("")))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null, new LoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn"), "MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyFn__Locals",
                                                     "_classVariant"),
                                                     "a",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn__Closure"), []))
                             ],
                             locals: [
                                 new MethodLocal("_local0", "b", StringT)
                             ])
                     ])
             },
             {
                 "access local that is referenced in closure",
                 """
                 var a = "";
                 var c = a;
                 fn InnerFn() {
                     var b = a;
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "_Main__Locals",
                             variants: [
                                 Variant("_classVariant", [Field("a", StringT)])
                             ]),
                         DataType(ModuleId, "InnerFn__Closure",
                             variants: [
                                 Variant("_classVariant", [Field("_Main__Locals", new LoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new LoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "a", "_classVariant"),
                                             new Use(new StringConstant(""))),
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new Copy(
                                                 new Field(new Local("_localsObject"), "a", "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null, new LoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])),
                                 new MethodLocal("_local1", "c", StringT),
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.InnerFn"), "InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "_Main__Locals",
                                                         "_classVariant"),
                                                     "a",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))
                             ],
                             locals: [
                                 new MethodLocal("_local0", "b", StringT)
                             ])
                     ])
             },
             {
                 "parameter used in closure",
                 """
                 fn MyFn(a: string)
                 {
                     var b = a;
                     fn InnerFn()
                     {
                         var c = a;
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, 
                             "MyFn__Locals",
                             variants: [Variant("_classVariant", [Field("a", StringT)])]),
                         DataType(ModuleId, 
                             "MyFn__InnerFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [Field("MyFn__Locals", new LoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn"), "MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyFn__Locals",
                                                         "_classVariant"),
                                                     "a",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "c", StringT)],
                             parameters: [("closure", new LoweredConcreteTypeReference("MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn__Closure"), []))]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyFn"), "MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "a", "_classVariant"),
                                             new Use(new Copy(new Local("_param0")))),
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new Copy(
                                                 new Field(new Local("_localsObject"), "a", "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             returnType: Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null, new LoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), [])),
                                 new MethodLocal("_local1", "b", StringT)
                             ],
                             parameters: [("a", StringT)])
                     ])
             },
             {
                 "field used in closure",
                 """
                 class MyClass
                 {
                     field MyField: string,

                     fn MyFn()
                     {
                         var a = MyField;
                         fn InnerFn()
                         {
                             var b = MyField;
                         }
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant",
                                     [Field("MyField", StringT)])
                             ]),
                         DataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 Variant("_classVariant", [Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "this",
                                                         "_classVariant"),
                                                     "MyField",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "b", StringT)],
                             parameters: [("closure", new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(new Local("_param0"), "MyField", "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "a", StringT)],
                             parameters: [("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                     ])
             },
             {
                 "static field used in inner closure",
                 """
                 class MyClass
                 {
                     static field MyField: string = "",

                     fn MyFn()
                     {
                         fn InnerFn()
                         {
                             var b = MyField;
                         }
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant("_classVariant")
                             ],
                             staticFields: [StaticField(
                                 "MyField",
                                 StringT,
                                 [
                                     new BasicBlock(
                                         new BasicBlockId("bb0"),
                                         [new Assign(new Local("_returnValue"), new Use(new StringConstant("")))], 
                                         new GoTo(new BasicBlockId("bb1"))),
                                     new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                 ], 
                                 [])]),
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(new StaticField(
                                                 new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                                 "MyField"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new MethodLocal("_local0", "b", StringT)],
                             parameters: []),
                             Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                                 [
                                     new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                                 ],
                                 Unit,
                                 locals: [],
                                 parameters: [("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                     ])
             },
             {
                 "assigning local in closure",
                 """
                 var mut a = "";
                 a = "hi";
                 fn InnerFn()
                 {
                     a = "bye";
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "_Main__Locals",
                             variants: [
                                 Variant("_classVariant",
                                     [Field("a", StringT)])
                             ]),
                         DataType(ModuleId, 
                             "InnerFn__Closure",
                             variants: [
                                 Variant("_classVariant",
                                     [Field("_Main__Locals", new LoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.InnerFn"), "InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_param0"),
                                                     "_Main__Locals",
                                                     "_classVariant"),
                                                 "a",
                                                 "_classVariant"),
                                             new Use(new StringConstant("bye")))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new LoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "a", "_classVariant"),
                                             new Use(new StringConstant(""))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "a", "_classVariant"),
                                             new Use(new StringConstant("hi"))),
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null,
                                     new LoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))
                             ])
                     ])
             },
             {
                 "this reference in closure",
                 """
                 class MyClass
                 {
                     fn MyFn()
                     {
                         fn InnerFn()
                         {
                             var a = this;
                         }
                         InnerFn();
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [Variant("_classVariant")]),
                         DataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Local("_param0"),
                                                     "this",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))
                             ],
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
                                         new Assign(
                                             new Field(new Local("_local1"), "this", "_classVariant"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new MethodCall(
                                         new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), []),
                                         [new Copy(new Local("_local1"))],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", null, Unit),
                                 new MethodLocal(
                                     "_local1",
                                     null,
                                     new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
                             ],
                             parameters: [("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                     ])
             },
             {
                 "assigning field in closure",
                 """
                 class MyClass
                 {
                     mut field MyField: string,

                     mut fn MyFn()
                     {
                         MyField = "hi";
                         mut fn InnerFn()
                         {
                             MyField = "bye";
                         }
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [Field("MyField", StringT)])
                             ]),
                         DataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     fields: [Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Field(
                                                 new Field(
                                                     new Local("_param0"),
                                                     "this",
                                                     "_classVariant"),
                                                 "MyField",
                                                 "_classVariant"),
                                             new Use(new StringConstant("bye")))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 
                                 new BasicBlock(
                                     new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Field(new Local("_param0"), "MyField", "_classVariant"),
                                             new Use(new StringConstant("hi")))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                             ])
                     ])
             },
             {
                 "calling closure",
                 """
                 class MyClass
                 {
                     field MyField: string,

                     fn MyFn(param: string)
                     {
                         var a = "";
                         fn InnerFn(b: i64)
                         {
                             var _a = a;
                             var _param = param;
                             var _myField = MyField;
                         }
                         InnerFn(3);
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("MyField", StringT)
                                     ])
                             ]),
                         DataType(ModuleId, 
                             "MyClass__MyFn__Locals",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("param", StringT),
                                         Field("a", StringT),
                                     ])
                             ]),
                         DataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         Field("MyClass__MyFn__Locals", new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
                                     ])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyClass__MyFn__Locals",
                                                         "_classVariant"),
                                                     "a",
                                                     "_classVariant")))),
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyClass__MyFn__Locals",
                                                         "_classVariant"),
                                                     "param",
                                                     "_classVariant")))),
                                         new Assign(
                                             new Local("_local2"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "this",
                                                         "_classVariant"),
                                                     "MyField",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "_a", StringT),
                                 new MethodLocal("_local1", "_param", StringT),
                                 new MethodLocal("_local2", "_myField", StringT)
                             ],
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
                                 ("b", Int64T)
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "param", "_classVariant"),
                                             new Use(new Copy(new Local("_param1")))),
                                         new Assign(
                                             new Field(
                                                 new Local("_localsObject"),
                                                 "a",
                                                 "_classVariant"),
                                             new Use(new StringConstant(""))),
                                         new Assign(
                                             new Local("_local2"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local2"),
                                                 "MyClass__MyFn__Locals",
                                                 "_classVariant"),
                                             new Use(new Copy(new Local("_localsObject")))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local2"),
                                                 "this",
                                                 "_classVariant"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), []),
                                         [
                                             new Copy(new Local("_local2")),
                                             new IntConstant(3, 8)
                                         ],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null, new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                 new MethodLocal("_local1", null, Unit),
                                 new MethodLocal("_local2", null, new LoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
                             ],
                             parameters: [
                                 ("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 ("param", StringT)
                             ])
                     ])
             },
             {
                 "calling deep closure",
                 """
                 class MyClass
                 {
                     field MyField: string,

                     fn MyFn(param: string)
                     {
                         var a = "";
                         fn MiddleFn(b: i64)
                         {
                             fn InnerFn()
                             {
                                 var _a = a;
                                 var _b = b;
                                 var _param = param;
                                 var _myField = MyField;
                             }
                             InnerFn();
                         }
                         MiddleFn(3);
                     }
                 }
                 """,
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass",
                             variants: [
                                 Variant("_classVariant", [Field("MyField", StringT)])
                             ]),
                         DataType(ModuleId, "MyClass__MyFn__Locals",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("param", StringT),
                                         Field("a", StringT)
                                     ])
                             ]),
                         DataType(ModuleId, "MyClass__MyFn__MiddleFn__Locals",
                             variants: [
                                 Variant("_classVariant", [Field("b", Int64T)])
                             ]),
                         DataType(ModuleId, "MyClass__MyFn__MiddleFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         Field("MyClass__MyFn__Locals", new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
                                     ])
                             ]),
                         DataType(ModuleId, "MyClass__MyFn__MiddleFn__InnerFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         Field("MyClass__MyFn__Locals", new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                         Field("MyClass__MyFn__MiddleFn__Locals", new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))
                                     ])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), "MyClass__MyFn__MiddleFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyClass__MyFn__Locals",
                                                         "_classVariant"),
                                                     "a",
                                                     "_classVariant")))),
                                         new Assign(
                                             new Local("_local1"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyClass__MyFn__MiddleFn__Locals",
                                                         "_classVariant"),
                                                     "b",
                                                     "_classVariant")))),
                                         new Assign(
                                             new Local("_local2"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "MyClass__MyFn__Locals",
                                                         "_classVariant"),
                                                     "param",
                                                     "_classVariant")))),
                                         new Assign(
                                             new Local("_local3"),
                                             new Use(new Copy(
                                                 new Field(
                                                     new Field(
                                                         new Local("_param0"),
                                                         "this",
                                                         "_classVariant"),
                                                     "MyField",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "_a", StringT),
                                 new MethodLocal("_local1", "_b", Int64T),
                                 new MethodLocal("_local2", "_param", StringT),
                                 new MethodLocal("_local3", "_myField", StringT)
                             ],
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), "MyClass__MyFn__MiddleFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_localsObject"),
                                                 "b",
                                                 "_classVariant"),
                                             new Use(new Copy(new Local("_param1")))),
                                         new Assign(
                                             new Local("_local2"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))),
                                         new Assign(
                                             new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                             new Use(new Copy(new Field(new Local("_param0"), "MyClass__MyFn__Locals", "_classVariant")))),
                                         new Assign(
                                             new Field(new Local("_local2"), "MyClass__MyFn__MiddleFn__Locals", "_classVariant"),
                                             new Use(new Copy(new Local("_localsObject")))),
                                         new Assign(
                                             new Field(new Local("_local2"), "this", "_classVariant"),
                                             new Use(new Copy(new Field(new Local("_param0"), "this", "_classVariant")))),
                                     ],
                                     new MethodCall(
                                         new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), []),
                                         [new Copy(new Local("_local2"))],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null,
                                     new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])),
                                 new MethodLocal("_local1", null, Unit),
                                 new MethodLocal("_local2", null, new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                             ],
                             parameters: [
                                 ("closure", new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), [])),
                                 ("b", Int64T)
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "param", "_classVariant"),
                                             new Use(new Copy(new Local("_param1")))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "a", "_classVariant"),
                                             new Use(new StringConstant(""))),
                                         new Assign(
                                             new Local("_local2"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))),
                                         new Assign(
                                             new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                             new Use(new Copy(new Local("_localsObject")))),
                                         new Assign(
                                             new Field(new Local("_local2"), "this", "_classVariant"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new MethodCall(
                                         new LoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), []),
                                         [
                                             new Copy(new Local("_local2")),
                                             new IntConstant(3, 8)
                                         ],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_localsObject", null, new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                 new MethodLocal("_local1", null, Unit),
                                 new MethodLocal("_local2", null, new LoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))
                             ],
                             parameters: [
                                 ("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 ("param", StringT)
                             ])
                     ])
             }
        };
    }

}
