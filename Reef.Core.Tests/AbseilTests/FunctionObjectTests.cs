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
    
    [Fact]
    public void Single()
    {
        var source = """
                 union MyUnion{A(string)}
                 var a = MyUnion::A;
                 var b = a("");
                 """;
                 var expectedProgram = LoweredProgram(
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
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         ConcreteTypeReference("MyUnion", ModuleId),
                                         ReturnValue,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(ReturnValue),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
                                                     "MyUnion",
                                                     new DefId(ModuleId, $"{ModuleId}.MyUnion"),
                                                     []))),
                                         new Assign(
                                             new Field(
                                                 new Deref(ReturnValue),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(
                                                 new Deref(ReturnValue),
                                                 "Item0",
                                                 "A"),
                                             new Use(
                                                 new Copy(
                                                     Param0)))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", StringT)],
                             returnType: new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([StringT], new LoweredPointer(ConcreteTypeReference("MyUnion", ModuleId))),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(
                                                 FunctionObject([StringT], new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))))),
                                         new Assign(
                                             new Field(new Deref(Local0), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), []))))
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([StringT], new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         [new Copy(Local0), new StringConstant("")],
                                         Local1,
                                         BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredPointer(new LoweredConcreteTypeReference(
                                         "Function`2",
                                         DefId.FunctionObject(1),
                                         [StringT, new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))]))),
                                 new MethodLocal(
                                     "_local1",
                                     "b",
                                     new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                             ])
                     ]);
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
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         ConcreteTypeReference("MyUnion", ModuleId),
                                         ReturnValue,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(ReturnValue),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
                                                     "MyUnion",
                                                     new DefId(ModuleId, $"{ModuleId}.MyUnion"),
                                                     []))),
                                         new Assign(
                                             new Field(
                                                 new Deref(ReturnValue),
                                                 "_variantIdentifier",
                                                 "A"),
                                             new Use(new UIntConstant(0, 2))),
                                         new Assign(
                                             new Field(
                                                 new Deref(ReturnValue),
                                                 "Item0",
                                                 "A"),
                                             new Use(
                                                 new Copy(
                                                     Param0)))
                                     ],
                                     new Return())
                             ],
                             parameters: [("Item0", StringT)],
                             returnType: new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([StringT], new LoweredPointer(ConcreteTypeReference("MyUnion", ModuleId))),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(
                                                 FunctionObject([StringT], new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))))),
                                         new Assign(
                                             new Field(new Deref(Local0), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyUnion__Create__A"), []))))
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([StringT], new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
                                         [new Copy(Local0), new StringConstant("")],
                                         Local1,
                                         BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredPointer(new LoweredConcreteTypeReference(
                                         "Function`2",
                                         DefId.FunctionObject(1),
                                         [StringT, new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))]))),
                                 new MethodLocal(
                                     "_local1",
                                     "b",
                                     new LoweredPointer(new LoweredConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion"), []))),
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
                                 new BasicBlock(BB0, [], new Return())
                             ],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(
                                                 FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(
                                                 new Deref(Local0),
                                                 "FunctionReference",
                                                 "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.SomeFn"), []))))
                                     ],
                                     new GoTo(BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredPointer(FunctionObject([], Unit)))
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
                             [new BasicBlock(BB0, [], new Return())],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(
                                                 FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(
                                                 new Deref(Local0),
                                                 "FunctionReference",
                                                 "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []))))
                                     ],
                                     new GoTo(BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredPointer(FunctionObject([], Unit)))
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
                             [new BasicBlock(BB0, [], new Return())],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(
                                                 FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(
                                                 new Deref(Local0),
                                                 "FunctionReference",
                                                 "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__OtherFn"), []))))
                                     ],
                                     new GoTo(BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a", new LoweredPointer(FunctionObject([], Unit)))
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
                             [new BasicBlock(BB0, [], new Return())],
                             Unit,
                             parameters: [("this", new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         ConcreteTypeReference("MyClass", ModuleId),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(new LoweredConcreteTypeReference(
                                                 "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                     ],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local1,
                                         BB2)),
                                 new BasicBlock(
                                     BB2,
                                     [
                                         new Assign(
                                             new Deref(Local1),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Deref(Local1), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), [])))),
                                         new Assign(
                                             new Field(new Deref(Local1), "FunctionParameter", "_classVariant"),
                                             new Use(new Copy(Local0)))
                                     ],
                                     new GoTo(BB3)),
                                 new BasicBlock(BB3, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                 new MethodLocal("_local1", "b", new LoweredPointer(FunctionObject([], Unit)))
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
                                         Field("this", new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                         Field(
                                             "MyClass__MyFn__Locals",
                                             new LoweredPointer(new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), [])))
                                     ])
                             ])
                     ],
                     methods: [
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), "MyClass__MyFn__InnerFn",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [
                                         new Assign(
                                             Local0,
                                             new Use(new Copy(
                                                 new Field(
                                                     new Deref(new Field(
                                                         new Deref(Param0),
                                                         "MyClass__MyFn__Locals",
                                                         "_classVariant")),
                                                     "a",
                                                     "_classVariant")))),
                                         new Assign(
                                             Local1,
                                             new Use(new Copy(
                                                 new Field(
                                                     new Deref(new Field(
                                                         new Deref(Param0),
                                                         "MyClass__MyFn__Locals",
                                                         "_classVariant")),
                                                     "param",
                                                     "_classVariant")))),
                                         new Assign(
                                             Local2,
                                             new Use(new Copy(
                                                 new Field(
                                                     new Deref(new Field(
                                                         new Deref(Param0),
                                                         "this",
                                                         "_classVariant")),
                                                     "MyField",
                                                     "_classVariant"))))
                                     ],
                                     new GoTo(BB1)),
                                 new BasicBlock(BB1, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "_a", StringT),
                                 new MethodLocal("_local1", "_param", StringT),
                                 new MethodLocal("_local2", "_myField", StringT)
                             ],
                             parameters: [
                                 ("closure", new LoweredPointer(new LoweredConcreteTypeReference(
                                     "MyClass__MyFn__InnerFn__Closure",
                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                     [])))
                             ]),
                         Method(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn"), "MyClass__MyFn",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         ConcreteTypeReference("MyClass__MyFn__Locals", ModuleId),
                                         LocalsObject,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(LocalsObject),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
                                                     "MyClass__MyFn__Locals",
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"),
                                                     []))),
                                         new Assign(
                                             new Field(
                                                 new Deref(LocalsObject),
                                                 "param",
                                                 "_classVariant"),
                                             new Use(new Copy(Param1))),
                                         new Assign(
                                             new Field(
                                                 new Deref(LocalsObject),
                                                 "a",
                                                 "_classVariant"),
                                             new Use(new StringConstant("")))
                                     ],
                                     AllocateMethodCall(
                                         ConcreteTypeReference("MyClass__MyFn__InnerFn__Closure", ModuleId),
                                         Local2,
                                         BB2)),
                                 new BasicBlock(
                                     BB2,
                                     [
                                         new Assign(
                                             new Deref(Local2),
                                             new CreateObject(
                                                 new LoweredConcreteTypeReference(
                                                     "MyClass__MyFn__InnerFn__Closure",
                                                     new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                                     []))),
                                         new Assign(
                                             new Field(new Deref(Local2), "this", "_classVariant"),
                                             new Use(new Copy(Param0))),
                                         new Assign(
                                             new Field(new Deref(Local2), "MyClass__MyFn__Locals", "_classVariant"),
                                             new Use(new Copy(LocalsObject)))
                                     ],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local1,
                                         BB3)),
                                 new BasicBlock(
                                     BB3,
                                     [
                                         new Assign(
                                             new Deref(Local1),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Deref(Local1), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(
                                                 new LoweredFunctionReference(new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn"), [])))),
                                         new Assign(
                                             new Field(new Deref(Local1), "FunctionParameter", "_classVariant"),
                                             new Use(new Copy(Local2)))
                                     ],
                                     new GoTo(BB4)),
                                 new BasicBlock(
                                     BB4, [], new Return())
                             ],
                             Unit,
                             parameters: [
                                 ("this", new LoweredPointer(new LoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))),
                                 ("param", StringT)],
                             locals: [
                                 new MethodLocal(
                                     "_localsObject",
                                     null,
                                     new LoweredPointer(new LoweredConcreteTypeReference("MyClass__MyFn__Locals", new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__Locals"), []))),
                                 new MethodLocal(
                                     "_local1",
                                     "b",
                                     new LoweredPointer(FunctionObject([], Unit))),
                                 new MethodLocal(
                                     "_local2",
                                     null,
                                     new LoweredPointer(new LoweredConcreteTypeReference(
                                         "MyClass__MyFn__InnerFn__Closure",
                                         new DefId(ModuleId, $"{ModuleId}.MyClass__MyFn__InnerFn__Closure"),
                                         [])))
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
                             [new BasicBlock(BB0, [], new Return())],
                             Unit),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Deref(Local0), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.SomeFn"), [])))),
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([], Unit),
                                         [new Copy(Local0)],
                                         Local1,
                                         BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredPointer(FunctionObject([], Unit))),
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
                                     BB0,
                                     [new Assign(ReturnValue, new Use(new IntConstant(1, 8)))],
                                     new Return())
                             ],
                             parameters: [("a", StringT)],
                             returnType: Int64T),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([StringT], Int64T),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(FunctionObject([StringT], Int64T))),
                                         new Assign(
                                             new Field(new Deref(Local0), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.SomeFn"), [])))),
                                     ],
                                     new MethodCall(
                                         FunctionObjectCall([StringT], Int64T),
                                         [new Copy(Local0), new StringConstant("")],
                                         Local1,
                                         BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal(
                                     "_local0",
                                     "a",
                                     new LoweredPointer(FunctionObject([StringT], Int64T))),
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
                             [new BasicBlock(BB0, [], new Return())],
                             Unit,
                             [(new DefId(ModuleId, $"{ModuleId}.MyClass"), "T"), (new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), "T2")]),
                         Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                             [
                                 new BasicBlock(
                                     BB0,
                                     [],
                                     AllocateMethodCall(
                                         FunctionObject([], Unit),
                                         Local0,
                                         BB1)),
                                 new BasicBlock(
                                     BB1,
                                     [
                                         new Assign(
                                             new Deref(Local0),
                                             new CreateObject(FunctionObject([], Unit))),
                                         new Assign(
                                             new Field(new Deref(Local0), "FunctionReference", "_classVariant"),
                                             new Use(new FunctionPointerConstant(new LoweredFunctionReference(
                                                 new DefId(ModuleId, $"{ModuleId}.MyClass__SomeFn"), [StringT, Int64T])))),
                                     ],
                                     new GoTo(BB2)),
                                 new BasicBlock(BB2, [], new Return())
                             ],
                             Unit,
                             locals: [
                                 new MethodLocal("_local0", "a",
                                     new LoweredPointer(FunctionObject([], Unit)))
                             ])
                     ])
             }
        };

        
    }
}

