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
             {
                 "assigning closure to function object",
                 """
                 class MyClass
                 {
                     mut field MyField: string,

                     mut fn MyFn(param: string)
                     {
                         var a = "";
                         fn InnerFn()
                         {
                             var _a = a;
                             var _param = param;
                             var _myField = MyField;
                         }
                         var b = InnerFn;
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
                                         NewField("a", StringT),
                                         NewField("param", StringT)
                                     ])
                             ]),
                         NewDataType(ModuleId, "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 NewVariant(
                                     "_classVariant",
                                     [
                                         NewField("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         NewField(
                                             "MyClass__MyFn__Locals",
                                             new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
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
                                 ("closure", new NewLoweredConcreteTypeReference(
                                     "MyClass__MyFn__InnerFn__Closure",
                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                     []))
                             ]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(
                                                 new NewLoweredConcreteTypeReference(
                                                     "MyClass__MyFn__Locals",
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"),
                                                     []))),
                                         new Assign(
                                             new Field(
                                                 new Local("_localsObject"),
                                                 "param",
                                                 "_classVariant"),
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
                                                 new NewLoweredConcreteTypeReference(
                                                     "MyClass__MyFn__InnerFn__Closure",
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                                     []))),
                                         new Assign(
                                             new Field(new Local("_local2"), "this", "_classVariant"),
                                             new Use(new Copy(new Local("_param0")))),
                                         new Assign(
                                             new Field(new Local("_local2"), "MyClass__MyFn__Locals", "_classVariant"),
                                             new Use(new Copy(new Local("_localsObject")))),
                                         new Assign(
                                             new Local("_local1"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local1"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new NewLoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), [])))),
                                         new Assign(
                                             new Field(new Local("_local1"), "FunctionParameter", "_classVariant"),
                                             new Use(new Copy(new Local("_local2"))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(
                                     new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("this", new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 ("param", StringT)],
                             locals: [
                                 new NewMethodLocal(
                                     "_localsObject",
                                     null,
                                     new NewLoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                 new NewMethodLocal(
                                     "_local1",
                                     "b",
                                     FunctionObject([], Unit)),
                                 new NewMethodLocal(
                                     "_local2",
                                     null,
                                     new NewLoweredConcreteTypeReference(
                                         "MyClass__MyFn__InnerFn__Closure",
                                         new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                         []))
                             ])
                     ])
             },
             {
                 "call function object without parameters",
                 """
                 fn SomeFn() {}
                 var a = SomeFn;
                 a();
                 """,
                 NewLoweredProgram(
                     methods: [
                         NewMethod(
                             new DefId(ModuleId, $"{ModuleId}.SomeFn"),
                             "SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.SomeFn"), [])))),
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([], Unit),
                                         [new Copy(new Local("_local0"))],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal(
                                     "_local0",
                                     "a",
                                     FunctionObject([], Unit)),
                                 new NewMethodLocal(
                                     "_local1",
                                     null,
                                     Unit)
                             ])
                     ])
             },
             {
                 "call function object with parameters",
                 """
                 fn SomeFn(a: string): i64 { return 1; }
                 var a = SomeFn;
                 var b = a("");
                 """,
                 NewLoweredProgram(
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [new Assign(new Local("_returnValue"), new Use(new IntConstant(1, 8)))],
                                     new Return())
                             ],
                             parameters: [("a", StringT)],
                             returnType: Int64T),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(FunctionObject([StringT], Int64T))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.SomeFn"), [])))),
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([StringT], Int64T),
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
                                     FunctionObject([StringT], Int64T)),
                                 new NewMethodLocal("_local1", "b", Int64T)
                             ])
                     ])
             },
             {
                 "assign generic function to function object",
                 """
                 class MyClass<T>
                 {
                     pub static fn SomeFn<T2>(){}
                 }
                 var a = MyClass::<string>::SomeFn::<i64>;
                 """,
                 NewLoweredProgram(
                     types: [
                         NewDataType(ModuleId, "MyClass", ["T"], [NewVariant("_classVariant")])
                     ],
                     methods: [
                         NewMethod(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")]),
                         NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new NewLoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), [StringT, Int64T])))),
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new NewMethodLocal("_local0", "a",
                                     FunctionObject([], Unit))
                             ])
                     ])
             }
        };

        
    }
}

