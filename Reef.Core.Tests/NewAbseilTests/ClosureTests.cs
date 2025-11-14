using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ClosureTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClosureAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ClosureTests";
    
    public static TheoryData<string, string, NewLoweredProgram> TestCases()
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
//             {
//                 "access local that is referenced in closure",
//                 """
//                 var a = "";
//                 var c = a;
//                 fn InnerFn() {
//                     var b = a;
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "_Main__Locals",
//                             variants: [
//                                 Variant("_classVariant", [Field("a", StringT)])
//                             ]),
//                         DataType(ModuleId, "InnerFn__Closure",
//                             variants: [
//                                 Variant("_classVariant", [Field("_Main__Locals", new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "_localsObject", 
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])),
//                                         "_classVariant",
//                                         true),
//                                     false),
//                                 FieldAssignment(
//                                     LocalAccess("_localsObject", true, new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("", true),
//                                     false,
//                                     StringT),
//                                 VariableDeclaration(
//                                     "c",
//                                     FieldAccess(
//                                         LocalAccess(
//                                             "_localsObject",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                 new NewMethodLocal("_localX", "c", StringT),
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.InnerFn"), "InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "b",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(0, true, new NewLoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))),
//                                             "_Main__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "b", StringT)
//                             ])
//                     ])
//             },
//             {
//                 "parameter used in closure",
//                 """
//                 fn MyFn(a: string)
//                 {
//                     var b = a;
//                     fn InnerFn()
//                     {
//                         var c = a;
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyFn__Locals",
//                             variants: [Variant("_classVariant", [Field("a", StringT)])]),
//                         DataType(ModuleId, 
//                             "MyFn__InnerFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [Field("MyFn__Locals", new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), [])))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn"), "MyFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "c",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn__Closure"), []))),
//                                             "MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [Local("c", StringT)],
//                             parameters: [ConcreteTypeReference("MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyFn__InnerFn__Closure"))]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyFn"), "MyFn",
//                             [
//                                 VariableDeclaration(
//                                     "_localsObject",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), [])),
//                                         "_classVariant",
//                                         true,
//                                         new(){{"a", LoadArgument(0, true, StringT)}}),
//                                     false),
//                                 VariableDeclaration(
//                                     "b",
//                                     FieldAccess(
//                                         LocalAccess(
//                                             "_localsObject",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyFn__Locals"), []))),
//                                 new NewMethodLocal("_localX", "b", StringT)
//                             ],
//                             parameters: [StringT])
//                     ])
//             },
//             {
//                 "field used in closure",
//                 """
//                 class MyClass
//                 {
//                     field MyField: string,
//
//                     fn MyFn()
//                     {
//                         var a = MyField;
//                         fn InnerFn()
//                         {
//                             var b = MyField;
//                         }
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant",
//                                     [Field("MyField", StringT)])
//                             ]),
//                         DataType(ModuleId, 
//                             "MyClass__MyFn__InnerFn__Closure",
//                             variants: [
//                                 Variant("_classVariant", [Field("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "b",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
//                                             "this",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                         "MyField",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [Local("b", StringT)],
//                             parameters: [ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"))]),
//                             NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                                 [
//                                     VariableDeclaration(
//                                         "a",
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0, true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                             "MyField",
//                                             "_classVariant",
//                                             true,
//                                             StringT),
//                                         false),
//                                     MethodReturnUnit()
//                                 ],
//                                 locals: [Local("a", StringT)],
//                                 parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))])
//                     ])
//             },
//             {
//                 "static field used in inner closure",
//                 """
//                 class MyClass
//                 {
//                     static field MyField: string = "",
//
//                     fn MyFn()
//                     {
//                         fn InnerFn()
//                         {
//                             var b = MyField;
//                         }
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant("_classVariant")
//                             ],
//                             staticFields: [StaticField("MyField", StringT, StringConstant("", true))]),
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "b",
//                                     StaticFieldAccess(
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                         "MyField",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [Local("b", StringT)],
//                             parameters: []),
//                             NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                                 [
//                                     MethodReturnUnit()
//                                 ],
//                                 locals: [],
//                                 parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))])
//                     ])
//             },
//             {
//                 "assigning local in closure",
//                 """
//                 var mut a = "";
//                 a = "hi";
//                 fn InnerFn()
//                 {
//                     a = "bye";
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "_Main__Locals",
//                             variants: [
//                                 Variant("_classVariant",
//                                     [Field("a", StringT)])
//                             ]),
//                         DataType(ModuleId, 
//                             "InnerFn__Closure",
//                             variants: [
//                                 Variant("_classVariant",
//                                     [Field("_Main__Locals", new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.InnerFn"), "InnerFn",
//                             [
//                                 FieldAssignment(
//                                     FieldAccess(
//                                         LoadArgument(
//                                             0, true, new NewLoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))),
//                                         "_Main__Locals",
//                                         "_classVariant",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("bye", true),
//                                     false,
//                                     StringT),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.InnerFn__Closure"), []))
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "_localsObject",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])),
//                                         "_classVariant",
//                                         true,
//                                         []),
//                                     false),
//                                 FieldAssignment(
//                                     LocalAccess(
//                                         "_localsObject",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("", true),
//                                     false,
//                                     StringT),
//                                 FieldAssignment(
//                                     LocalAccess(
//                                         "_localsObject",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), []))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("hi", true),
//                                     false,
//                                     StringT),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 NewMethodLocal("_localsObject", null,
//                                     new NewLoweredConcreteTypeReference("_Main__Locals", new DefId(ModuleId, $"{ModuleId}._Main__Locals"), [])))
//                             ])
//                     ])
//             },
//             {
//                 "this reference in closure",
//                 """
//                 class MyClass
//                 {
//                     fn MyFn()
//                     {
//                         fn InnerFn()
//                         {
//                             var a = this;
//                         }
//                         InnerFn();
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [Variant("_classVariant")]),
//                         DataType(ModuleId, 
//                             "MyClass__MyFn__InnerFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [Field("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     FieldAccess(
//                                         LoadArgument(
//                                             0, true, new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
//                                         "this",
//                                         "_classVariant",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "a", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn"),
//                                     [
//                                         CreateObject(
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
//                                             "_classVariant",
//                                             true,
//                                             new(){{"this", LoadArgument(0, true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))}})
//                                     ],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))])
//                     ])
//             },
//             {
//                 "assigning field in closure",
//                 """
//                 class MyClass
//                 {
//                     mut field MyField: string,
//
//                     mut fn MyFn()
//                     {
//                         MyField = "hi";
//                         mut fn InnerFn()
//                         {
//                             MyField = "bye";
//                         }
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [Field("MyField", StringT)])
//                             ]),
//                         DataType(ModuleId, 
//                             "MyClass__MyFn__InnerFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     fields: [Field("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
//                             [
//                                 FieldAssignment(
//                                     FieldAccess(
//                                         LoadArgument(
//                                             0,
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
//                                         "this",
//                                         "_classVariant",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                     "_classVariant",
//                                     "MyField",
//                                     StringConstant("bye", true),
//                                     false,
//                                     StringT),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 FieldAssignment(
//                                     LoadArgument(
//                                         0, true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                     "_classVariant",
//                                     "MyField",
//                                     StringConstant("hi", true),
//                                     false,
//                                     StringT),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
//                             ])
//                     ])
//             },
//             {
//                 "calling closure",
//                 """
//                 class MyClass
//                 {
//                     field MyField: string,
//
//                     fn MyFn(param: string)
//                     {
//                         var a = "";
//                         fn InnerFn(b: i64)
//                         {
//                             var _a = a;
//                             var _param = param;
//                             var _myField = MyField;
//                         }
//                         InnerFn(3);
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, 
//                             "MyClass",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("MyField", StringT)
//                                     ])
//                             ]),
//                         DataType(ModuleId, 
//                             "MyClass__MyFn__Locals",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("param", StringT),
//                                         Field("a", StringT),
//                                     ])
//                             ]),
//                         DataType(ModuleId, 
//                             "MyClass__MyFn__InnerFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                         Field("MyClass__MyFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
//                                     ])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "_a",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
//                                             "MyClass__MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 VariableDeclaration(
//                                     "_param",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
//                                             "MyClass__MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                         "param",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 VariableDeclaration(
//                                     "_myField",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), []))),
//                                             "this",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                         "MyField",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "_a", StringT),
//                                 new NewMethodLocal("_localX", "_param", StringT),
//                                 new NewMethodLocal("_localX", "_myField", StringT)
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
//                                 Int64_t
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 VariableDeclaration(
//                                     "_localsObject",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
//                                         "_classVariant",
//                                         true,
//                                         new(){{"param", LoadArgument(1, true, StringT)}}),
//                                     false),
//                                 FieldAssignment(
//                                     LocalAccess(
//                                         "_localsObject",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("", true),
//                                     false,
//                                     StringT),
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn"),
//                                     [
//                                         CreateObject(
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"), [])),
//                                             "_classVariant",
//                                             true,
//                                             new(){
//                                                 {"this", LoadArgument(0, true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))},
//                                                 {"MyClass__MyFn__Locals", LocalAccess("_localsObject", true, new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))}
//                                             }),
//                                         Int64Constant(3, true)
//                                     ],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 StringT
//                             ])
//                     ])
//             },
//             {
//                 "calling deep closure",
//                 """
//                 class MyClass
//                 {
//                     field MyField: string,
//
//                     fn MyFn(param: string)
//                     {
//                         var a = "";
//                         fn MiddleFn(b: i64)
//                         {
//                             fn InnerFn()
//                             {
//                                 var _a = a;
//                                 var _b = b;
//                                 var _param = param;
//                                 var _myField = MyField;
//                             }
//                             InnerFn();
//                         }
//                         MiddleFn(3);
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass",
//                             variants: [
//                                 Variant("_classVariant", [Field("MyField", StringT)])
//                             ]),
//                         DataType(ModuleId, "MyClass__MyFn__Locals",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("param", StringT),
//                                         Field("a", StringT)
//                                     ])
//                             ]),
//                         DataType(ModuleId, "MyClass__MyFn__MiddleFn__Locals",
//                             variants: [
//                                 Variant("_classVariant", [Field("b", Int64_t)])
//                             ]),
//                         DataType(ModuleId, "MyClass__MyFn__MiddleFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                         Field("MyClass__MyFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
//                                     ])
//                             ]),
//                         DataType(ModuleId, "MyClass__MyFn__MiddleFn__InnerFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                         Field("MyClass__MyFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                         Field("MyClass__MyFn__MiddleFn__Locals", new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])))
//                                     ])
//                             ])
//                     ],
//                     methods: [
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), "MyClass__MyFn__MiddleFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "_a",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))),
//                                             "MyClass__MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 VariableDeclaration(
//                                     "_b",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))),
//                                             "MyClass__MyFn__MiddleFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), []))),
//                                         "b",
//                                         "_classVariant",
//                                         true,
//                                         Int64_t),
//                                     false),
//                                 VariableDeclaration(
//                                     "_param",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))),
//                                             "MyClass__MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                         "param",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 VariableDeclaration(
//                                     "_myField",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0,
//                                                 true,
//                                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))),
//                                             "this",
//                                             "_classVariant",
//                                             true,
//                                             new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
//                                         "MyField",
//                                         "_classVariant",
//                                         true,
//                                         StringT),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 new NewMethodLocal("_localX", "_a", StringT),
//                                 new NewMethodLocal("_localX", "_b", Int64_t),
//                                 new NewMethodLocal("_localX", "_param", StringT),
//                                 new NewMethodLocal("_localX", "_myField", StringT)
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), []))
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), "MyClass__MyFn__MiddleFn",
//                             [
//                                 VariableDeclaration(
//                                     "_localsObject",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])),
//                                         "_classVariant",
//                                         true,
//                                         new(){{"b", LoadArgument(1, true, Int64_t)}}),
//                                     false),
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn"), "MyClass__MyFn__MiddleFn__InnerFn"),
//                                     [
//                                         CreateObject(
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__InnerFn__Closure"), [])),
//                                             "_classVariant",
//                                             true,
//                                             new(){
//                                                 {"MyClass__MyFn__MiddleFn__Locals", LocalAccess("_localsObject", true, new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])))},
//                                                 {
//                                                     "MyClass__MyFn__Locals",
//                                                     FieldAccess(
//                                                         LoadArgument(0, true, new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))),
//                                                         "MyClass__MyFn__Locals",
//                                                         "_classVariant",
//                                                         true,
//                                                         new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
//                                                 },
//                                                 {
//                                                     "this",
//                                                     FieldAccess(
//                                                         LoadArgument(0, true, new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), []))),
//                                                         "this",
//                                                         "_classVariant",
//                                                         true,
//                                                         new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
//                                                 }
//                                             })
//                                     ],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 NewMethodLocal("_localsObject", null,
//                                     new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Locals"), [])))
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), [])),
//                                 Int64_t
//                             ]),
//                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 VariableDeclaration(
//                                     "_localsObject",
//                                     CreateObject(
//                                         new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "param",
//                                                 LoadArgument(1, true, StringT)
//                                             }
//                                         }),
//                                     false),
//                                 FieldAssignment(
//                                     LocalAccess(
//                                         "_localsObject",
//                                         true,
//                                         new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("", true),
//                                     false,
//                                     StringT),
//                                 MethodCall(
//                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn"), "MyClass__MyFn__MiddleFn"),
//                                     [
//                                         CreateObject(
//                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__MiddleFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__MiddleFn__Closure"), [])),
//                                             "_classVariant",
//                                             true,
//                                             new()
//                                             {
//                                                 {
//                                                     "MyClass__MyFn__Locals",
//                                                     LocalAccess("_localsObject", true, new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
//                                                 },
//                                                 {
//                                                     "this",
//                                                     LoadArgument(0, true, new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
//                                                 }
//                                             }),
//                                         Int64Constant(3, true)
//                                     ],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 NewMethodLocal("_localsObject", null, new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
//                             ],
//                             parameters: [
//                                 new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
//                                 StringT
//                             ])
//                     ])
//             }
        };
    }

}
