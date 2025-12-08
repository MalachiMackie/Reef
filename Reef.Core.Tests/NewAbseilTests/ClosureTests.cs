using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ClosureTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClosureAbseilTest(string description, string source, NewLoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

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
        var expectedProgram = NewLoweredProgram(
            types:
            [
                NewDataType(ModuleId, "MyClass",
                    variants:
                    [
                        NewVariant("_classVariant", [NewField("MyField", StringT)])
                    ]),
                NewDataType(ModuleId, "MyClass__MyFn__Locals",
                    variants:
                    [
                        NewVariant(
                            "_classVariant",
                            [
                                NewField("param", StringT),
                                NewField("a", StringT)
                            ])
                    ]),
                NewDataType(ModuleId, "MyClass__MyFn__MiddleFn__Locals",
                    variants:
                    [
                        NewVariant("_classVariant", [NewField("b", Int64T)])
                    ]),
                NewDataType(ModuleId, "MyClass__MyFn__MiddleFn__Closure",
                    variants:
                    [
                        NewVariant(
                            "_classVariant",
                            [
                                NewField("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                NewField("MyClass__MyFn__Locals",
                                    new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
                            ])
                    ]),
                NewDataType(ModuleId, "MyClass__MyFn__MiddleFn__InnerFn__Closure",
                    variants:
                    [
                        NewVariant(
                            "_classVariant",
                            [
                                NewField("this",
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                NewField("MyClass__MyFn__Locals",
                                    new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                NewField("MyClass__MyFn__MiddleFn__Locals",
                                    new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))
                            ])
                    ])
            ],
            methods:
            [
                NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"),
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
                        new NewMethodLocal("_local0", "_a", StringT),
                        new NewMethodLocal("_local1", "_b", Int64T),
                        new NewMethodLocal("_local2", "_param", StringT),
                        new NewMethodLocal("_local3", "_myField", StringT)
                    ],
                    parameters:
                    [
                        ("closure",
                            new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                    ]),
                NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), "MyClass__MyFn__MiddleFn",
                    [
                        new BasicBlock(
                            new BasicBlockId("bb0"),
                            [
                                new Assign(
                                    new Local("_localsObject"),
                                    new CreateObject(
                                        new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals",
                                            new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))),
                                new Assign(
                                    new Field(
                                        new Local("_localsObject"),
                                        "b",
                                        "_classVariant"),
                                    new Use(new Copy(new Local("_param1")))),
                                new Assign(
                                    new Local("_local2"),
                                    new CreateObject(new NewLoweredConcreteTypeReference(
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
                                new NewLoweredFunctionReference(
                                    new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), []),
                                [new Copy(new Local("_local2"))],
                                new Local("_local1"),
                                new BasicBlockId("bb1"))),
                        new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                    ],
                    Unit,
                    locals:
                    [
                        new NewMethodLocal("_localsObject", null,
                            new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])),
                        new NewMethodLocal("_local1", null, Unit),
                        new NewMethodLocal("_local2", null,
                            new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                    ],
                    parameters:
                    [
                        ("closure",
                            new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), [])),
                        ("b", Int64T)
                    ]),
                NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                    [
                        new BasicBlock(
                            new BasicBlockId("bb0"),
                            [
                                new Assign(
                                    new Local("_localsObject"),
                                    new CreateObject(new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals",
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
                                        new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure",
                                            new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))),
                                new Assign(
                                    new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                    new Use(new Copy(new Local("_localsObject")))),
                                new Assign(
                                    new Field(new Local("_local2"), "this", "_classVariant"),
                                    new Use(new Copy(new Local("_param0"))))
                            ],
                            new MethodCall(
                                new NewLoweredFunctionReference(
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
                        new NewMethodLocal("_localsObject", null,
                            new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                        new NewMethodLocal("_local1", null, Unit),
                        new NewMethodLocal("_local2", null,
                            new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure",
                                new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))
                    ],
                    parameters:
                    [
                        ("this",
                            new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"),
                                [])),
                        ("param", StringT)
                    ])
            ]);
        
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ClosureTests";
    
    public static TheoryData<string, string, NewLoweredModule> TestCases()
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyFn__Locals",
                             variants: [
                                 NewVariant("_classVariant", [NewField("a", StringT)])
                             ]),
                         NewDataType(ModuleId, "MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant("_classVariant", [NewField("MyFn__Locals", new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyFn"), "MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
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
                                 new NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn"), "MyFn__InnerFn",
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
                                 ("closure", new NewLoweredConcreteTypeReference("MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn__Closure"), []))
                             ],
                             locals: [
                                 new NewMethodLocal("_local0", "b", StringT)
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "_Main__Locals",
                             variants: [
                                 NewVariant("_classVariant", [NewField("a", StringT)])
                             ]),
                         NewDataType(ModuleId, "InnerFn__Closure",
                             variants: [
                                 NewVariant("_classVariant", [NewField("_Main__Locals", new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
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
                                 new NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])),
                                 new NewMethodLocal("_local1", "c", StringT),
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.InnerFn"), "InnerFn",
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
                                 ("closure", new NewLoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))
                             ],
                             locals: [
                                 new NewMethodLocal("_local0", "b", StringT)
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyFn__Locals",
                             variants: [NewVariant("_classVariant", [NewField("a", StringT)])]),
                         NewDataType(ModuleId, 
                             "MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [NewField("MyFn__Locals", new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn"), "MyFn__InnerFn",
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
                             locals: [new NewMethodLocal("_local0", "c", StringT)],
                             parameters: [("closure", new NewLoweredConcreteTypeReference("MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn__Closure"), []))]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyFn"), "MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
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
                                 new NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), [])),
                                 new NewMethodLocal("_local1", "b", StringT)
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant",
                                     [NewField("MyField", StringT)])
                             ]),
                         NewDataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant("_classVariant", [NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
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
                             locals: [new NewMethodLocal("_local0", "b", StringT)],
                             parameters: [("closure", new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
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
                             locals: [new NewMethodLocal("_local0", "a", StringT)],
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ],
                             staticFields: [NewStaticField(
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
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new Use(new Copy(new StaticField(
                                                 new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                                 "MyField"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [new NewMethodLocal("_local0", "b", StringT)],
                             parameters: []),
                             NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                                 [
                                     new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                                 ],
                                 Unit,
                                 locals: [],
                                 parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "_Main__Locals",
                             variants: [
                                 NewVariant("_classVariant",
                                     [NewField("a", StringT)])
                             ]),
                         NewDataType(ModuleId, 
                             "InnerFn__Closure",
                             variants: [
                                 NewVariant("_classVariant",
                                     [NewField("_Main__Locals", new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.InnerFn"), "InnerFn",
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
                                 ("closure", new NewLoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
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
                                 new NewMethodLocal("_localsObject", null,
                                     new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [NewVariant("_classVariant")]),
                         NewDataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
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
                                 ("closure", new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))
                             ],
                             locals: [
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local1"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
                                         new Assign(
                                             new Field(new Local("_local1"), "this", "_classVariant"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), []),
                                         [new Copy(new Local("_local1"))],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", null, Unit),
                                 new NewMethodLocal(
                                     "_local1",
                                     null,
                                     new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
                             ],
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [NewField("MyField", StringT)])
                             ]),
                         NewDataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     fields: [NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
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
                                 ("closure", new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
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
                                 ("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("MyField", StringT)
                                     ])
                             ]),
                         NewDataType(ModuleId, 
                             "MyClass__MyFn__Locals",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("param", StringT),
                                         NewField("a", StringT),
                                     ])
                             ]),
                         NewDataType(ModuleId, 
                             "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         NewField("MyClass__MyFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
                                     ])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
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
                                 new NewMethodLocal("_local0", "_a", StringT),
                                 new NewMethodLocal("_local1", "_param", StringT),
                                 new NewMethodLocal("_local2", "_myField", StringT)
                             ],
                             parameters: [
                                 ("closure", new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
                                 ("b", Int64T)
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
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
                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
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
                                         new NewLoweredFunctionReference(
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
                                 new NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                 new NewMethodLocal("_local1", null, Unit),
                                 new NewMethodLocal("_local2", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
                             ],
                             parameters: [
                                 ("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
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
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [
                                 NewVariant("_classVariant", [NewField("MyField", StringT)])
                             ]),
                         NewDataType(ModuleId, "MyClass__MyFn__Locals",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("param", StringT),
                                         NewField("a", StringT)
                                     ])
                             ]),
                         NewDataType(ModuleId, "MyClass__MyFn__MiddleFn__Locals",
                             variants: [
                                 NewVariant("_classVariant", [NewField("b", Int64T)])
                             ]),
                         NewDataType(ModuleId, "MyClass__MyFn__MiddleFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         NewField("MyClass__MyFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
                                     ])
                             ]),
                         NewDataType(ModuleId, "MyClass__MyFn__MiddleFn__InnerFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         NewField("MyClass__MyFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                         NewField("MyClass__MyFn__MiddleFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))
                                     ])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), "MyClass__MyFn__MiddleFn__InnerFn",
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
                                 new NewMethodLocal("_local0", "_a", StringT),
                                 new NewMethodLocal("_local1", "_b", Int64T),
                                 new NewMethodLocal("_local2", "_param", StringT),
                                 new NewMethodLocal("_local3", "_myField", StringT)
                             ],
                             parameters: [
                                 ("closure", new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), "MyClass__MyFn__MiddleFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_localsObject"),
                                                 "b",
                                                 "_classVariant"),
                                             new Use(new Copy(new Local("_param1")))),
                                         new Assign(
                                             new Local("_local2"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))),
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
                                         new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), []),
                                         [new Copy(new Local("_local2"))],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_localsObject", null,
                                     new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])),
                                 new NewMethodLocal("_local1", null, Unit),
                                 new NewMethodLocal("_local2", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
                             ],
                             parameters: [
                                 ("closure", new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), [])),
                                 ("b", Int64T)
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "param", "_classVariant"),
                                             new Use(new Copy(new Local("_param1")))),
                                         new Assign(
                                             new Field(new Local("_localsObject"), "a", "_classVariant"),
                                             new Use(new StringConstant(""))),
                                         new Assign(
                                             new Local("_local2"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))),
                                         new Assign(
                                             new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                             new Use(new Copy(new Local("_localsObject")))),
                                         new Assign(
                                             new Field(new Local("_local2"), "this", "_classVariant"),
                                             new Use(new Copy(new Local("_param0"))))
                                     ],
                                     new MethodCall(
                                         new NewLoweredFunctionReference(
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
                                 new NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                 new NewMethodLocal("_local1", null, Unit),
                                 new NewMethodLocal("_local2", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))
                             ],
                             parameters: [
                                 ("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 ("param", StringT)
                             ])
                     ])
             }
        };
    }

}
