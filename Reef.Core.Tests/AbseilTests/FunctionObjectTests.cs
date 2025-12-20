using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class FunctionObjectTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void FunctionObjectAbseilTest(string description, string source, LoweredModule expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var (loweredProgram, _) = ProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "FunctionObjectTests";
    
    public static TheoryData<string, string, LoweredModule> TestCases()
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
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyUnion",
                             variants: [
                                 Variant(
                                     "A",
                                     [
                                         Field("_variantIdentifier", UInt16T),
                                         Field("Item0", StringT)
                                     ])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), "MyUnion__Create__A",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_returnValue"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
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
                             returnType: new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(
                                                 FunctionObject([StringT], new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), []))))
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([StringT], new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                                         [new Copy(new Local("_local0")), new StringConstant("")],
                                         new Local("_local1"),
                                         new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredConcreteTypeReference(
                                         "Function`2",
                                         DefId.FunctionObject(1),
                                         [StringT, new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])])),
                                 new MethodLocal(
                                     "_local1",
                                     "b",
                                     new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), [])),
                             ])
                     ])
             },
             {
                 "assign global function to function object",
                 """
                 fn SomeFn(){}
                 var a = SomeFn;
                 """,
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
                             [
                                 new BasicBlock(new BasicBlockId("bb0"), [], new Return())
                             ],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
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
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), []))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", FunctionObject([], Unit))
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
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass",
                             variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
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
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", FunctionObject([], Unit))
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
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass",
                             variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), "MyClass__OtherFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
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
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []))))
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", FunctionObject([], Unit))
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
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass", variants: [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             parameters: [("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(new LoweredConcreteTypeReference(
                                                 "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         new Assign(
                                             new Local("_local1"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local1"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
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
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 new MethodLocal("_local1", "b", FunctionObject([], Unit))
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
                                         Field("a", StringT),
                                         Field("param", StringT)
                                     ])
                             ]),
                         DataType(ModuleId, "MyClass__MyFn__InnerFn__Closure",
                             variants: [
                                 Variant(
                                     "_classVariant",
                                     [
                                         Field("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                         Field(
                                             "MyClass__MyFn__Locals",
                                             new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))
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
                                 ("closure", new LoweredConcreteTypeReference(
                                     "MyClass__MyFn__InnerFn__Closure",
                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                     []))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_localsObject"),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
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
                                                 new LoweredConcreteTypeReference(
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
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), [])))),
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
                                 ("this", new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])),
                                 ("param", StringT)],
                             locals: [
                                 new MethodLocal(
                                     "_localsObject",
                                     null,
                                     new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])),
                                 new MethodLocal(
                                     "_local1",
                                     "b",
                                     FunctionObject([], Unit)),
                                 new MethodLocal(
                                     "_local2",
                                     null,
                                     new LoweredConcreteTypeReference(
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
                 LoweredProgram(
                     methods: [
                         Method(
                             new DefId(ModuleId, $"{ModuleId}.SomeFn"),
                             "SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
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
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     FunctionObject([], Unit)),
                                 new MethodLocal(
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
                 LoweredProgram(
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.SomeFn"), "SomeFn",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [new Assign(new Local("_returnValue"), new Use(new IntConstant(1, 8)))],
                                     new Return())
                             ],
                             parameters: [("a", StringT)],
                             returnType: Int64T),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(FunctionObject([StringT], Int64T))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
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
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     FunctionObject([StringT], Int64T)),
                                 new MethodLocal("_local1", "b", Int64T)
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
                 LoweredProgram(
                     types: [
                         DataType(ModuleId, "MyClass", ["T"], [Variant("_classVariant")])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "MyClass__SomeFn",
                             [new BasicBlock(new BasicBlockId("bb0"), [], new Return())],
                             Unit,
                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     new BasicBlockId("bb0"),
                                     [
                                         new Assign(
                                             new Local("_local0"),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Local("_local0"), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), [StringT, Int64T])))),
                                     ],
                                     new GoTo(new BasicBlockId("bb1"))),
                                 new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a",
                                     FunctionObject([], Unit))
                             ])
                     ])
             }
        };

        
    }
}

