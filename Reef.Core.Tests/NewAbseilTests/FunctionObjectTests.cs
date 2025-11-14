using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;
using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class FunctionObjectTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void FunctionObjectAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "FunctionObjectTests";
    
    public static TheoryData<string, string, NewLoweredProgram> TestCases()
    {
        return new()
        {
             {
                 "assign union tuple variant method to function object",
                 """
                 union MyUnion{A(string)}
                 var a = MyUnion::A;
                 var b = a("");
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyUnion",
                             variants: [
                                 NewVariant(
                                     "A",
                                     [
                                         NewField("_variantIdentifier", UInt16T),
                                         NewField("Item0", StringT)
                                     ])
                             ])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference(
                                                     "MyUnion",
                                                     new DefId(ModuleId, $"{ModuleId}.MyUnion"),
                                                     []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_returnValue"),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(
                                                 new Local("_returnValue"),
                                                 "Item0",
                                                 "A"),
                                             new Use(
                                                 new Copy(
                                                     new Local("_param0"))))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", StringT)],
                             returnType: new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 FunctionObject([StringT], new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), []))))
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([StringT], new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                         [new Copy(new Local("_local0")), new StringConstant("")],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal(
                                     "_local0",
                                     "a",
                                     new NewLoweredConcreteTypeReference(
                                         "Function`2",
                                         DefId.FunctionObject(1),
                                         [StringT, new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])])),
                                 new NewMethodLocal(
                                     "_local1",
                                     "b",
                                     new NewLoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                             ])
                     ])
             },
             {
                 "assign global function to function object",
                 """
                 fn SomeFn(){}
                 var a = SomeFn;
                 """,
                 NewLoweredProgram(
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "FunctionReference",
                                                 "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), []))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", FunctionObject([], Unit))
                             ])
                     ])
             },
             {
                 "assign static function to function object inside type",
                 """
                 class MyClass {
                     static fn OtherFn(){}
                     static fn MyFn() {
                         var a = OtherFn;
                     }
                 }
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "FunctionReference",
                                                 "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", FunctionObject([], Unit))
                             ])
                     ])
             },
             {
                 "assign static function to function object",
                 """
                 class MyClass {
                     pub static fn OtherFn(){}
                 }
                 var a = MyClass::OtherFn;
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass",
                             variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(
                                                 new Local("_local0"),
                                                 "FunctionReference",
                                                 "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a", FunctionObject([], Unit))
                             ])
                     ])
             },
             {
                 "assign instance function to function object",
                 """
                 class MyClass {
                     pub fn MyFn(){}
                 }
                 var a = new MyClass{};
                 var b = a.MyFn;
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", variants: [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             parameters: [("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new NewLoweredConcreteTypeReference(
                                                 "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Local("_local1"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local1"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), [])))),
                                         new Assign(
                                             new Field(new Local("_local1"), "FunctionParameter", "_classVariant"),
                                             new Use(new Copy(new Local("_local0"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal(
                                     "_local0",
                                     "a",
                                     new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new NewMethodLocal("_local1", "b", FunctionObject([], Unit))
                             ])
                     ])
             },
//             {
//                 "assigning closure to function object",
//                 """
//                 class MyClass
//                 {
//                     mut field MyField: string,
//
//                     mut fn MyFn(param: string)
//                     {
//                         var a = "";
//                         fn InnerFn()
//                         {
//                             var _a = a;
//                             var _param = param;
//                             var _myField = MyField;
//                         }
//                         var b = InnerFn;
//                     }
//                 }
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass",
//                             variants: [
//                                 Variant("_classVariant", [Field("MyField", StringType)])
//                             ]),
//                         DataType(ModuleId, "MyClass__MyFn__Locals",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("a", StringType),
//                                         Field("param", StringType)
//                                     ])
//                             ]),
//                         DataType(ModuleId, "MyClass__MyFn__InnerFn__Closure",
//                             variants: [
//                                 Variant(
//                                     "_classVariant",
//                                     [
//                                         Field("this", ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
//                                         Field(
//                                             "MyClass__MyFn__Locals",
//                                             ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals")))
//                                     ])
//                             ])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
//                             [
//                                 VariableDeclaration(
//                                     "_a",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"))),
//                                             "MyClass__MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"))),
//                                         "a",
//                                         "_classVariant",
//                                         true,
//                                         StringType),
//                                     false),
//                                 VariableDeclaration(
//                                     "_param",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"))),
//                                             "MyClass__MyFn__Locals",
//                                             "_classVariant",
//                                             true,
//                                             ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"))),
//                                         "param",
//                                         "_classVariant",
//                                         true,
//                                         StringType),
//                                     false),
//                                 VariableDeclaration(
//                                     "_myField",
//                                     FieldAccess(
//                                         FieldAccess(
//                                             LoadArgument(
//                                                 0, true, ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"))),
//                                             "this",
//                                             "_classVariant",
//                                             true,
//                                             ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"))),
//                                         "MyField",
//                                         "_classVariant",
//                                         true,
//                                         StringType),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 Local("_a", StringType),
//                                 Local("_param", StringType),
//                                 Local("_myField", StringType)
//                             ],
//                             parameters: [
//                                 ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"))
//                             ]),
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
//                             [
//                                 VariableDeclaration(
//                                     "__locals",
//                                     CreateObject(
//                                         ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals")),
//                                         "_classVariant",
//                                         true,
//                                         new(){{"param", LoadArgument(1, true, StringType)}}),
//                                     false),
//                                 FieldAssignment(
//                                     LocalAccess("__locals", true, ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"))),
//                                     "_classVariant",
//                                     "a",
//                                     StringConstant("", true),
//                                     false,
//                                     StringType),
//                                 VariableDeclaration(
//                                     "b",
//                                     CreateObject(
//                                         ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "FunctionReference",
//                                                 FunctionReferenceConstant(
//                                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn"),
//                                                     true,
//                                                     FunctionType(
//                                                         [ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"))],
//                                                         Unit))
//                                             },
//                                             {
//                                                 "FunctionParameter",
//                                                 CreateObject(
//                                                     ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure")),
//                                                     "_classVariant",
//                                                     true,
//                                                     new()
//                                                     {
//                                                         {
//                                                             "this",
//                                                             LoadArgument(0, true, ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")))
//                                                         },
//                                                         {
//                                                             "MyClass__MyFn__Locals",
//                                                             LocalAccess(
//                                                                 "__locals",
//                                                                 true,
//                                                                 ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals")))
//                                                         }
//                                                     })
//                                             }
//                                         }),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             parameters: [ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")), StringType],
//                             locals: [
//                                 Local("__locals", ConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"))),
//                                 Local("b", ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
//                             ])
//                     ])
//             },
//             {
//                 "call function object without parameters",
//                 """
//                 fn SomeFn() {}
//                 var a = SomeFn;
//                 a();
//                 """,
//                 LoweredProgram(
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn", [MethodReturnUnit()]),
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "FunctionReference",
//                                                 FunctionReferenceConstant(
//                                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn"),
//                                                     true,
//                                                     FunctionType([], Unit))
//                                             }
//                                         }),
//                                     false),
//                                 MethodCall(
//                                     FunctionReference(DefId.FunctionObject_Call(0), "Function`1__Call", [Unit]),
//                                     [
//                                         LocalAccess(
//                                             "a",
//                                             true,
//                                             ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
//                                     ],
//                                     false,
//                                     Unit),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 Local(
//                                     "a",
//                                     ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
//                             ])
//                     ])
//             },
//             {
//                 "call function object with parameters",
//                 """
//                 fn SomeFn(a: string): i64 { return 1; }
//                 var a = SomeFn;
//                 var b = a("");
//                 """,
//                 LoweredProgram(
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
//                             [MethodReturn(Int64Constant(1, true))],
//                             parameters: [StringType],
//                             returnType: Int64_t),
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, Int64_t]),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "FunctionReference",
//                                                 FunctionReferenceConstant(
//                                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn"),
//                                                     true,
//                                                     FunctionType([StringType], Int64_t))
//                                             }
//                                         }),
//                                     false),
//                                 VariableDeclaration(
//                                     "b",
//                                     MethodCall(
//                                         FunctionReference(DefId.FunctionObject_Call(1), "Function`2__Call", [StringType, Int64_t]),
//                                         [
//                                             LocalAccess(
//                                                 "a",
//                                                 true,
//                                                 ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, Int64_t])),
//                                             StringConstant("", true)
//                                         ],
//                                         true,
//                                         Int64_t),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 Local(
//                                     "a",
//                                     ConcreteTypeReference("Function`2", DefId.FunctionObject(1), [StringType, Int64_t])),
//                                 Local("b", Int64_t)
//                             ])
//                     ])
//             },
//             {
//                 "assign generic function to function object",
//                 """
//                 class MyClass<T>
//                 {
//                     pub static fn SomeFn<T2>(){}
//                 }
//                 var a = MyClass::<string>::SomeFn::<i64>;
//                 """,
//                 LoweredProgram(
//                     types: [
//                         DataType(ModuleId, "MyClass", ["T"], [Variant("_classVariant")])
//                     ],
//                     methods: [
//                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
//                             [MethodReturnUnit()],
//                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")]),
//                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
//                             [
//                                 VariableDeclaration(
//                                     "a",
//                                     CreateObject(
//                                         ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]),
//                                         "_classVariant",
//                                         true,
//                                         new()
//                                         {
//                                             {
//                                                 "FunctionReference",
//                                                 FunctionReferenceConstant(
//                                                     FunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn", [StringType, Int64_t]),
//                                                     true,
//                                                     FunctionType([], Unit))
//                                             }
//                                         }),
//                                     false),
//                                 MethodReturnUnit()
//                             ],
//                             locals: [
//                                 Local("a",
//                                     ConcreteTypeReference("Function`1", DefId.FunctionObject(0), [Unit]))
//                             ])
//                     ])
//             }
        };

        
    }
}

