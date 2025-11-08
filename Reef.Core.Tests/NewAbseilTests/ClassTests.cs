using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;

using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ClassTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClassAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }
    
    [Fact]
    public void SingleTest()
    {
        const string source = """
                 class MyClass{pub static field MyField: string = ""}
                 var a = MyClass::MyField;
                 """; 
        var expectedProgram = NewLoweredProgram(
            types:
            [
                NewDataType(ModuleId,
                    "MyClass",
                    variants: [NewVariant("_classVariant")],
                    staticFields:
                    [
                        NewStaticField(
                            "MyField",
                            StringT,
                            [
                                new BasicBlock(new BasicBlockId("bb0"), [
                                    new Assign(new Local("_returnValue"), new Use(new StringConstant("")))
                                ])
                                {
                                    Terminator = new GoTo(new BasicBlockId("bb1"))
                                },
                                new BasicBlock(new BasicBlockId("bb1"), [])
                                {
                                    Terminator = new Return()
                                }
                            ],
                            [],
                            new NewMethodLocal("_returnValue", null, StringT))
                    ])
            ],
            methods:
            [
                NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                    [
                        new BasicBlock(new BasicBlockId("bb0"), [
                            new Assign(
                                new Local("_local0"),
                                new Use(new Copy(new StaticField(
                                    new NewLoweredConcreteTypeReference("MyClass",
                                        new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                    "MyField"))))
                        ])
                        {
                            Terminator = new GoTo(new BasicBlockId("bb1"))
                        },
                        new BasicBlock(new BasicBlockId("bb1"), [])
                        {
                            Terminator = new Return()
                        }
                    ],
                    Unit,
                    locals: [new NewMethodLocal("_local0", "a", StringT)])
            ]);
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ClassTests";

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
    {
        return new()
        {
             {
                 "empty class",
                 "class MyClass{}",
                 NewLoweredProgram(
                         types: [
                             NewDataType(ModuleId, "MyClass",
                                 variants: [NewVariant("_classVariant")])
                         ])
             },
             {
                 "generic class",
                 "class MyClass<T>{}",
                 NewLoweredProgram(
                         types: [
                             NewDataType(ModuleId, "MyClass",
                                 ["T"],
                                 variants: [NewVariant("_classVariant")])
                         ])
             },
             {
                 "generic class with instance function",
                 "class MyClass<T>{pub fn SomeFn(){}}",
                 NewLoweredProgram(
                         types: [
                             NewDataType(ModuleId, "MyClass",
                                 ["T"],
                                 [NewVariant("_classVariant")])
                         ], methods: [
                                     NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), 
                                         "MyClass__SomeFn",
                                         [new BasicBlock(new BasicBlockId("bb0"), []) {Terminator = new Return()}],
                                         Unit,
                                         typeParameters: [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")],
                                         parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [new NewLoweredGenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")]))])
                                 ])
             },
             {
                 "class with instance fields",
                 "class MyClass { pub field MyField: string, pub field OtherField: i64}",
                 NewLoweredProgram(types: [
                     NewDataType(ModuleId, "MyClass",
                         variants: [
                             NewVariant(
                                 "_classVariant",
                                 [
                                     NewField("MyField", StringT),
                                     NewField("OtherField", Int64T),
                                 ])
                         ])
                 ])
             },
             {
                 "class with static fields",
                 """class MyClass { pub static field MyField: string = ""}""",
                 NewLoweredProgram(types: [
                     NewDataType(ModuleId, "MyClass",
                         variants: [NewVariant("_classVariant")],
                         staticFields: [
                             NewStaticField(
                                 "MyField",
                                 StringT,
                                 [
                                     new BasicBlock(new BasicBlockId("bb0"), [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new Use(new StringConstant("")))
                                     ])
                                     {
                                         Terminator = new GoTo(new BasicBlockId("bb1"))
                                     },
                                     new BasicBlock(new BasicBlockId("bb1"), [])
                                     {
                                         Terminator = new Return()
                                     }
                                 ],
                                 [],
                                 new NewMethodLocal("_returnValue", null, StringT))
                         ])
                 ])
             },
             {
                 "access class field",
                 """
                 class MyClass {pub field A: string}
                 var a = new MyClass{A = ""};
                 var b = a.A;
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [
                                 NewVariant("_classVariant", [NewField("A", StringT)])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new CreateObject(new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                     new Assign(
                                         new Field("_local0", "A", "_classVariant"),
                                         new Use(new StringConstant(""))),
                                     new Assign(
                                         new Local("_local2"),
                                         new Use(new Copy(new Local("_local0")))),
                                     new Assign(
                                         new Local("_local1"),
                                         new Use(new Copy(new Field("_local2", "A", "_classVariant"))))
                                 ])
                                 {
                                     Terminator = new GoTo(new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), []) {Terminator = new Return()}
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new NewMethodLocal("_local1", "b", StringT),
                                 new NewMethodLocal("_local2", null, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                             ])
                     ])
             },
             {
                 "access static field",
                 """
                 class MyClass{pub static field MyField: string = ""}
                 var a = MyClass::MyField;
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [NewVariant("_classVariant")],
                             staticFields: [
                                 NewStaticField(
                                     "MyField",
                                     StringT,
                                     [
                                         new BasicBlock(new BasicBlockId("bb0"), [
                                             new Assign(new Local("_returnValue"), new Use(new StringConstant("")))
                                         ])
                                         {
                                             Terminator = new GoTo(new BasicBlockId("bb1"))
                                         },
                                         new BasicBlock(new BasicBlockId("bb1"), [])
                                         {
                                             Terminator = new Return()
                                         }
                                     ],
                                     [],
                                     new NewMethodLocal("_returnValue", null, StringT))
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new Use(new Copy(new StaticField(
                                             new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []),
                                             "MyField"))))
                                 ])
                                 {
                                     Terminator = new GoTo(new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), [])
                                 {
                                     Terminator = new Return()
                                 }
                             ],
                             Unit,
                             locals: [new NewMethodLocal("_local0", "a", StringT)])
                     ])
             },
             {
                 "call static method",
                 """
                 MyClass::MyFn();
                 class MyClass
                 {
                     pub static fn MyFn(){}
                 }
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", variants: [NewVariant("_classVariant")]),
                     ],
                     methods: [
                         NewMethod(
                             new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"),
                             "MyClass__MyFn",
                             [new BasicBlock(new BasicBlockId("bb0"), []) { Terminator = new Return() }],
                             Unit),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [])
                                 {
                                     Terminator = new MethodCall(
                                         new NewLoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                         [],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), [])
                                 {
                                     Terminator = new Return()
                                 }
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", null, Unit)
                             ])
                     ])
             },
             {
                 "assign instance method to function variable from within type but different method",
                 """
                 class MyClass {
                     pub fn OtherFn(){}
                     pub fn MyFn() {
                         var a = OtherFn; 
                     }
                 }
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                             [new BasicBlock(new BasicBlockId("bb0"), []) {Terminator = new Return()}],
                             Unit,
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new CreateObject(
                                             new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))),
                                     new Assign(
                                         new Field("_local0", "FunctionReference", "_classVariant"),
                                         new Use(new FunctionPointerConstant(
                                             new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"),
                                                 [])))),
                                     new Assign(
                                         new Field("_local0", "FunctionParameter", "_classVariant"),
                                         new Use(new Copy(new Local("_param0"))))
                                 ])
                                 {
                                     Terminator = new GoTo(new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), []) { Terminator = new Return() }
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit])),
                             ],
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                     ])
             },
             {
                 "assign instance method to function variable from within type",
                 """
                 class MyClass {
                     pub fn MyFn() {
                         var a = MyFn; 
                     }
                 }
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [
                                 NewVariant("_classVariant")
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new CreateObject(
                                             new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))),
                                     new Assign(
                                         new Field("_local0", "FunctionReference", "_classVariant"),
                                         new Use(new FunctionPointerConstant(
                                             new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), [])))),
                                     new Assign(
                                         new Field("_local0", "FunctionParameter", "_classVariant"),
                                         new Use(new Copy(new Local("_param0"))))
                                 ])
                                 {
                                     Terminator = new GoTo(new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), [])
                                 {
                                     Terminator = new Return()
                                 }
                             ],
                             Unit,
                             locals: [new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))],
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                     ])
             },
             {
                 "call instance method",
                 """
                 class MyClass
                 {
                     pub fn MyFn(){}
                 }
                 var a = new MyClass{};
                 a.MyFn();
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), 
                             "MyClass__MyFn",
                             [new BasicBlock(new BasicBlockId("bb0"), []) {Terminator = new Return()}],
                             Unit,
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), 
                             "_Main",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new CreateObject(
                                             new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                 ])
                                 {
                                     Terminator = new MethodCall(
                                         new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                         [new Copy(new Local("_local0"))],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), [])
                                 {
                                     Terminator = new Return()
                                 }
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new NewMethodLocal("_local1", null, Unit)
                             ])
                     ])
             },
             {
                 "call static method inside function",
                 """
                 class MyClass
                 {
                     pub static fn MyFn(){}
                     pub static fn OtherFn()
                     {
                         MyFn();
                     }
                 }
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn", [
                             new BasicBlock(new BasicBlockId("bb0"), []) { Terminator = new Return() }
                         ], Unit),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [])
                                 {
                                     Terminator = new MethodCall(
                                         new NewLoweredFunctionReference(
                                             new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), []),
                                         [],
                                         new Local("_local0"),
                                         new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), []) { Terminator = new Return() }
                             ],
                             Unit,
                             locals: [new NewMethodLocal("_local0", null, Unit)])
                     ])
             },
             {
                 "access instance field inside function",
                 """
                 class MyClass
                 {
                     field MyField: string,
                     pub fn MyFn()
                     {
                         var a = MyField;
                     }
                 }
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, 
                             "MyClass",
                             variants: [
                                 NewVariant("_classVariant", [NewField("MyField", StringT)])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [
                                     new Assign(
                                         new Local("_local0"),
                                         new Use(new Copy(new Field("_param0", "MyField", "_classVariant"))))
                                 ])
                                 {
                                     Terminator = new GoTo(new BasicBlockId("bb1"))
                                 },
                                 new BasicBlock(new BasicBlockId("bb1"), [])
                                 {
                                     Terminator = new Return()
                                 }
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", StringT),
                             ],
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))])
                     ])
             },
//             {
//                 "access static field inside function",
//                 """
//                 class MyClass
//                 {
//                     static field MyField: string = "",
//                     pub fn MyFn()
//                     {
//                         var a = MyField;
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [Variant("_classVariant")],
//                             staticFields: [StaticField("MyField", StringType, StringConstant("", true))])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     StaticFieldAccess(
//                                         ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")),
//                                         "MyField",
//                                         true,
//                                         StringType),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [Local("a", StringType)],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))])
//                     ])
//             },
//             {
//                 "call instance function inside instance function",
//                 """
//                 class MyClass
//                 {
//                     fn MyFn()
//                     {
//                         OtherFn();
//                     }
//                     fn OtherFn(){}
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn"),
//                                     [LoadArgument(0, true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")))],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))]),
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), 
//                             "MyClass__OtherFn",
//                             [MethodReturnUnit()],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))])
//                     ])
//             },
//             {
//                 "assign to field through member access",
//                 """
//                 class MyClass
//                 {
//                     pub mut field MyField: string
//                 }
//                 var mut a = new MyClass{MyField = ""};
//                 a.MyField = "hi";
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant", [Field("MyField", StringType)])
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration("a",
//                                     CreateObject(
//                                         ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")),
//                                         "_classVariant",
//                                         true,
//                                         new(){{"MyField", StringConstant("", true)}}),
//                                     false),
//                                 FieldAssignment(
//                                     LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
//                                     "_classVariant",
//                                     "MyField",
//                                     StringConstant("hi", true),
//                                     false,
//                                     StringType),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 Local("a", ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")))
//                             ])
//                     ])
//             },
//             {
//                 "assign to field in current type",
//                 """
//                 class MyClass
//                 {
//                     pub mut field MyField: string,
//
//                     mut fn SomeFn()
//                     {
//                         MyField = "hi";
//                     }
//
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant", [Field("MyField", StringType)])
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                             [
//                                 FieldAssignment(
//                                     LoadArgument(0, true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
//                                     "_classVariant",
//                                     "MyField",
//                                     StringConstant("hi", true),
//                                     false,
//                                     StringType),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [
//                                 ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))
//                             ])
//                     ])
//             },
//             {
//                 "assign to static field in current type",
//                 """
//                 class MyClass
//                 {
//                     pub mut static field MyField: string = "",
//
//                     static fn SomeFn()
//                     {
//                         MyField = "hi";
//                     }
//
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant")
//                             ],
//                             staticFields: [
//                                 StaticField("MyField", StringType, StringConstant("", true))
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                             [
//                                 StaticFieldAssignment(
//                                     ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")),
//                                     "MyField",
//                                     StringConstant("hi", true),
//                                     false,
//                                     StringType),
//                                 MethodReturnUnit()
//                             ])
//                     ])
//             },
//             {
//                 "assign to static field through static member access",
//                 """
//                 class MyClass
//                 {
//                     pub mut static field MyField: string = "",
//                 }
//                 MyClass::MyField = "hi";
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant")
//                             ],
//                             staticFields: [
//                                 StaticField("MyField", StringType, StringConstant("", true))
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 StaticFieldAssignment(
//                                     ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")),
//                                     "MyField",
//                                     StringConstant("hi", true),
//                                     false,
//                                     StringType),
//                                 MethodReturnUnit()
//                             ])
//                     ])
//             },
//             {
//                 "argument access in instance function",
//                 """
//                 class MyClass
//                 {
//                     fn SomeFn(a: string): string { return a; }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant")
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                             [
//                                 MethodReturn(LoadArgument(1, true, StringType))
//                             ],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")), StringType],
//                             returnType: StringType)
//                     ])
//             },
//             {
//                 "argument access in static function",
//                 """
//                 class MyClass
//                 {
//                     static fn SomeFn(a: string): string { return a; }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant")
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                             [
//                                 MethodReturn(LoadArgument(0, true, StringType))
//                             ],
//                             parameters: [StringType],
//                             returnType: StringType)
//                     ])
//             },
//             {
//                 "this reference",
//                 """
//                 class MyClass
//                 {
//                     fn SomeFn()
//                     {
//                         var a = this;
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", variants: [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                             [
//                                 VariableDeclaration("a",
//                                     LoadArgument(0, true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))],
//                             locals: [Local("a", ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")))])
//                     ])
//             },
//             {
//                 "non generic function in generic class",
//                 """
//                 class MyClass<T>
//                 {
//                     static fn SomeFn(){}
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [MethodReturnUnit()], [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
//                     ])
//             },
//             {
//                 "generic function in generic class",
//                 """
//                 class MyClass<T>
//                 {
//                     static fn SomeFn<T1>(){}
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [MethodReturnUnit()], [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")])
//                     ])
//             },
//             {
//                 "reference static generic method in generic type",
//                 """
//                 class MyClass<T>
//                 {
//                     pub static fn SomeFn<T1>(){}
//                 }
//                 MyClass::<string>::SomeFn::<i64>()
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [MethodReturnUnit()], [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")]),
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 MethodCall(
//                                     FunctionReference(
//                                         new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"),
//                                         "MyClass__SomeFn",
//                                         [StringType, Int64_t]),
//                                     [],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ])
//                     ])
//             },
//             {
//                 "reference generic method on instance of generic type",
//                 """
//                 class MyClass<T>
//                 {
//                     pub fn SomeFn<T2>(){}
//                 }
//                 var a = new MyClass::<string>{};
//                 a.SomeFn::<i64>();
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], variants: [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), 
//                             "MyClass__SomeFn",
//                             [MethodReturnUnit()],
//                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [GenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])]),
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringType]),
//                                         "_classVariant",
//                                         true),
//                                     false),
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [StringType, Int64_t]),
//                                     [LocalAccess("a", true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringType]))],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 Local("a", ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [StringType]))
//                             ])
//                     ])
//             },
//             {
//                 "reference generic method inside generic type",
//                 """
//                 class MyClass<T>
//                 {
//                     static fn SomeFn<T1>(){}
//  
//                     static fn OtherFn()
//                     {
//                         SomeFn::<string>();
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [MethodReturnUnit()], [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T1")]),
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
//                             [
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                                         [GenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), StringType]),
//                                     [],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
//                     ])
//             },
//             {
//                 "reference non generic method inside generic type",
//                 """
//                 class MyClass<T>
//                 {
//                     static fn SomeFn(){}
//
//                     static fn OtherFn()
//                     {
//                         SomeFn();
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [MethodReturnUnit()], [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")]),
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
//                             [
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                                         [GenericPlaceholder(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")]),
//                                     [],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T")])
//                     ])
//             }
        };
    }
}
