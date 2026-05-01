using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Reef.Core.Common;
using Reef.Core.Expressions;
using Reef.Core.Tests.IntegrationTests.Helpers;
using static Reef.Core.Tests.ExpressionHelpers;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Tests;

[TestMe]
public class TypeCheckerTests(ITestOutputHelper testOutputHelper)
{
    private readonly MockFileSystem _fileSystem = new();

    private static readonly ModuleId ModuleId = new("main");

    [Theory]
    [MemberData(nameof(SuccessfulExpressionTestCases))]
    public async Task Should_SuccessfullyTypeCheckExpressions(Dictionary<string, string> sourceFiles)
    {
        foreach (var (path, contents) in sourceFiles)
        {
            testOutputHelper.WriteLine($"{path}:");
            testOutputHelper.WriteLine(contents);

            _fileSystem.AddFile(path, new MockFileData(contents));
        }

        var (typeCheckResult, _, _, _) = await new ReefCompiler(_fileSystem, ModuleId, new TestLogger(testOutputHelper)).TypeCheck();

        foreach (var (moduleId, innerTypeCheckResult) in typeCheckResult)
        {
            innerTypeCheckResult.ParserErrors.Should().BeEmpty(because: $"{moduleId.Value} should have no parser errors");
            innerTypeCheckResult.TypeCheckerErrors.Should().BeEmpty(because: $"{moduleId.Value} should have no type checking errors");
        }
    }

    [Theory]
    [MemberData(nameof(FailedExpressionTestCases))]
    public async Task Should_FailTypeChecking_When_ExpressionsAreNotValid(
        string description,
        Dictionary<string, (string contents, IReadOnlyList<TypeCheckerError> expectedErrors)> sourceFiles)
    {
        description.Should().NotBeNull();

        foreach (var (path, (contents, _)) in sourceFiles)
        {
            testOutputHelper.WriteLine($"{path}:");
            testOutputHelper.WriteLine("");
            testOutputHelper.WriteLine(contents);
            testOutputHelper.WriteLine("");
            _fileSystem.AddFile(path, new MockFileData(contents));
        }

        sourceFiles.SelectMany(x => x.Value.expectedErrors).Should().NotBeEmpty();

        var (typeCheckResults, moduleIdToFileName, _, _) = await new ReefCompiler(_fileSystem, new ModuleId("main"), new TestLogger(testOutputHelper)).TypeCheck();
        foreach (var (moduleId, typeCheckResult) in typeCheckResults)
        {
            typeCheckResult.ParserErrors.Should().BeEmpty();
            var expectedErrors = sourceFiles.TryGetValue(moduleIdToFileName[moduleId], out var result) ? result.expectedErrors : [];
            typeCheckResult.TypeCheckerErrors.Should().BeEquivalentTo(expectedErrors,
                    opts => opts.Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        }
    }

    [Fact]
    public async Task SingleTest()
    {
        var sourceFiles = new Dictionary<string, (string, IReadOnlyList<TypeCheckerError> expectedErrors)>()
            {
                            {
                                "main.rf",
                                ("""
                                class MyClass{}
                                fn SomeFn(){}

                                var a = new otherModule:::MyClass{};
                                var b = otherModule:::MyUnion::A;
                                :::main:::otherModule:::SomeFn();
                                var d = new MyClass{};
                                SomeFn();

                                """, [])
                            },
                            {
                                "otherModule.rf",
                                ("""
                                pub class MyClass{}
                                pub union MyUnion{A}
                                pub fn SomeFn(){}
                                """, [])
                            }
        };

        foreach (var (path, (contents, _)) in sourceFiles)
        {
            _fileSystem.AddFile(path, new MockFileData(contents));
        }

        var (typeCheckResults, moduleIdToFileName, _, _) = await new ReefCompiler(_fileSystem, new ModuleId("main"), new TestLogger(testOutputHelper)).TypeCheck();
        foreach (var (moduleId, typeCheckResult) in typeCheckResults)
        {
            typeCheckResult.ParserErrors.Should().BeEmpty();
            var expectedErrors = sourceFiles.TryGetValue(moduleIdToFileName[moduleId], out var result) ? result.expectedErrors : [];
            typeCheckResult.TypeCheckerErrors.Should().BeEquivalentTo(expectedErrors,
                    opts => opts.Excluding(m => m.Type == typeof(SourceRange) || m.Type == typeof(SourceSpan)));
        }
    }

    public static TheoryData<Dictionary<string, string>> SuccessfulExpressionTestCases()
    {
        IEnumerable<Dictionary<string, string>> sources =
        [
            new()
            {
                {
                    "main.rf",
                    """
                    pub fn someFn(a: otherModule:::MyClass){}
                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub class MyClass{}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T, T2>(param: T): T2 where T2: boxed T;

                    var b: boxed i32 = some_fn(1);
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T>(): T where T : unboxed i32;

                    var b = some_fn();
                    var c = b + 2;
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T, T2>(param: T): T2 where T2: unboxed T;
                    class MyClass{}

                    var b: unboxed MyClass = some_fn(new MyClass{});
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T, T2>(param: T): T2 where T2: unboxed T;
                    class MyClass{}

                    var b: unboxed MyClass = some_fn(new unboxed MyClass{});
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T, T2>(param: T): T2 where T2: boxed T;
                    class MyClass{}

                    var b: MyClass = some_fn(new MyClass{});
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T, T2>(param: T): T2
                        where T2: boxed T
                        where T: unboxed T2;

                    class MyClass{}

                    var b: MyClass = some_fn(new unboxed MyClass{});
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    pub extern fn some_fn<T, T2>(): string where T: boxed T2;
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    var a: [string] = ["hi"];
                    var b: [string; 1] = ["hi"];
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule;

                    otherModule:::subModule:::SomeFn();
                    var a = new otherModule:::subModule:::MyClass{};
                    """
                },
                {
                    "otherModule/subModule.rf",
                    """
                    pub fn SomeFn(){}
                    pub class MyClass{}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::subModule;

                    subModule:::subSubModule:::SomeFn();
                    var a = new subModule:::subSubModule:::MyClass{};
                    """
                },
                {
                    "otherModule/subModule/subSubModule.rf",
                    """
                    pub fn SomeFn(){}
                    pub class MyClass{}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::subModule;

                    subModule:::SomeFn();
                    var a = new subModule:::MyClass{};
                    """
                },
                {
                    "otherModule/subModule.rf",
                    """
                    pub fn SomeFn(){}
                    pub class MyClass{}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::subModule;

                    otherModule:::subModule:::SomeFn();
                    var a = new otherModule:::subModule:::MyClass{};
                    """
                },
                {
                    "otherModule/subModule.rf",
                    """
                    pub fn SomeFn(){}
                    pub class MyClass{}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::{MyClass, MyUnion, SomeFn};

                    var a = new MyClass{};
                    var b = MyUnion::A;
                    var c = SomeFn();
                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub class MyClass{}
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    var a = new otherModule:::MyClass{};
                    var b = otherModule:::MyUnion::A;
                    var c = otherModule:::SomeFn();
                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub class MyClass{}
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    class MyClass{}
                    fn SomeFn(){}

                    var a = new otherModule:::MyClass{};
                    var b = otherModule:::MyUnion::A;
                    :::main:::otherModule:::SomeFn();
                    var d = new MyClass{};
                    SomeFn();

                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub class MyClass{}
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use someModule:::subModule:::{MyClass, MyUnion, SomeFn};

                    var a = new MyClass{};
                    var b = MyUnion::A;
                    var c = SomeFn();
                    """
                },
                {
                    "someModule/subModule.rf",
                    """
                    pub class MyClass{}
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use someModule:::{MyUnion, SomeFn, otherModule:::MyClass};

                    var a = new MyClass{};
                    var b = MyUnion::A;
                    SomeFn();
                    """
                },
                {
                    "someModule/someModule.rf",
                    """
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                },
                {
                    "someModule/otherModule.rf",
                    """
                    pub class MyClass{}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use someModule:::otherModule:::SomeFn;

                    pub class MyClass{};

                    SomeFn();
                    """
                },
                {
                    "someModule/otherModule.rf",
                    """
                    use :::main:::MyClass;

                    pub fn SomeFn() {
                        var a = new MyClass{};
                    }
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::*;

                    var a = new MyClass{};
                    var b = MyUnion::A;
                    var c = SomeFn();
                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub class MyClass{}
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::{MyClass, MyUnion};

                    var a = new MyClass{};
                    var b = MyUnion::A;
                    {
                        use otherModule:::SomeFn;
                        SomeFn();
                    }
                    """
                },
                {
                    "otherModule.rf",
                    """
                    pub class MyClass{}
                    pub union MyUnion{A}
                    pub fn SomeFn(){}
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::MyClass;

                    pub fn SomeFn(): MyClass {
                        return new MyClass{};
                    }

                    """
                },
                {
                    "otherModule.rf",
                    """
                    use :::main:::SomeFn;

                    pub class MyClass{};

                    fn OtherFn() {
                        var a = SomeFn();
                    }

                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use someModule:::{subModule:::{SomeFn, subSubModule:::MyClass}};

                    var a = new MyClass{};
                    SomeFn();
                    """
                },
                {
                    "someModule/subModule/subSubModule.rf",
                    """
                    pub class MyClass{};
                    """
                },
                {
                    "someModule/subModule/subModule.rf",
                    """
                    use subSubModule:::MyClass;

                    pub fn SomeFn() {
                        var a = new MyClass{};
                    }
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use someModule:::SomeFn;

                    var a = SomeFn();
                    print_string(a.Something);
                    """
                },
                {
                    "someModule.rf",
                    """
                    pub class MyClass{pub field Something: string}
                    pub fn SomeFn(): MyClass {
                        return new MyClass{Something = "hi"};
                    }
                    """
                }
            },
            new()
            {
                {
                    "main.rf",
                    """
                    use otherModule:::SomeFn;

                    pub class MyClass{};

                    var a = SomeFn();

                    """
                },
                {
                    "otherModule.rf",
                    """
                    use :::main:::MyClass;

                    pub fn SomeFn(): MyClass
                    {
                        return new MyClass{};
                    }
                    """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class FirstClass{ pub mut field FirstClassField: string }
                               class MyClass{pub mut field MyField: FirstClass}
                               var mut firstClass = new FirstClass{FirstClassField = ""};
                               var secondClass = new MyClass{MyField = firstClass}
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass { pub mut field MyField: string, }
                               var mut a = [new MyClass{MyField = "hi"}];
                               a[0].MyField = "bye";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn someFn(): mut string { return ""; }
                               var a: Fn(): mut string = someFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn someFn(): mut string { return ""; }
                               var a: Fn(): string = someFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn someFn(): string { return ""; }
                               var a: Fn(): string = someFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var mut a = [""];
                               a[0] = "x";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: [string; 4] = [""; 4];
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: [i32; 3] = [1, 2, 3];
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: unboxed [i32; 2] = [unboxed; 1, 2];
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = [1, 4];
                               var b: u32 = a[0];
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: [boxed i32; 2] = [box(2), box(3)];
                               var b: boxed i32 = a[0];
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: i32 = 1;
                               var b = -a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b = -a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: boxed i32 = box(1);
                               var b = unbox(a) == 1;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass{}
                               var a = unbox(new MyClass{});
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass{}
                               var a: unboxed MyClass = unbox(new MyClass{});
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: boxed i32 = box(2);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: boxed i32 = box(1);
                               var b: unboxed i32 = unbox(a);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = box(1);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion{A}
                               var a: unboxed MyUnion = unboxed MyUnion::A;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: unboxed i32 = 1;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass{}
                               var b: boxed MyClass = new MyClass{};
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               while (true) {
                                   continue;
                                   break;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               while (true) {
                                   if (true) {
                                       continue;
                                       break;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               while (true) {
                                   while (true) {
                                       if (true) {
                                           continue;
                                           break;
                                       }
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(param: T): T { return param; }
                               var a: i64 = SomeFn(1);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(param: T): T { return param; }
                               var a = SomeFn(1);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(param: T): T { return param; }
                               var a = SomeFn(1);
                               var c: u8 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b = a;
                               var c: i64 = b;
                               var d: i64 = a * c;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: i32 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: i64 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: i16 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: i8 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: u32 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: u64 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: u16 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: u8 = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion{A(string)}
                               var a = MyUnion::A;
                               var b = a("");
                               """
                }
            },
            new() { { "main.rf", "var a = if (true) {} else {};" } },
            new() { { "main.rf", "var a = if (true) {} else if (true) {} else {}; " } },
            new()
            {
                {
                    "main.rf", """
                               fn OtherFn(): result::<i64, i64>
                               {
                                   return ok(1);
                               }
                               fn SomeFn(): result::<string, i64>
                               {
                                   var a = OtherFn()?;
                                   return ok("");
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
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
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               fn SomeFn()
                               {
                                   fn InnerFn()
                                   {
                                       var b = a;
                                   }
                                   InnerFn();
                               }
                               SomeFn();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   static mut field MyField: string = "",

                                   // SomeFn doesn't need to be marked as mutable because it mutates a static field, not an instance field
                                   fn SomeFn()
                                   {
                                       MyField = "hi";
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               SomeFn();
                               fn SomeFn()
                               {
                                   var b = a;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass
                               {
                                   fn SomeFn()
                                   {
                                       TopLevelFn();
                                   }
                               }
                               fn TopLevelFn()
                               {
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A(string)
                               }
                               var mut a = MyUnion::A("");
                               if (a matches MyUnion::A(var mut str)) {
                                   str = "hi";
                               }
                               """
                }
            },
            new() { { "main.rf", "var a: bool = true && true" } },
            new() { { "main.rf", "var a: bool = true || true" } },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   fn MyFn<T>() {}
                                   fn OtherFn() {
                                       MyFn::<string>();
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   pub static fn SomeFn(){}
                               }
                               MyUnion::SomeFn();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   field MyField: string,

                                   fn MyFn() {
                                       var a = MyField;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   pub static fn SomeFn(){}
                               }
                               var a = MyUnion::SomeFn;
                               a();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, pub fn MyFn(){}}
                               var a = MyUnion::A;
                               a.MyFn();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub mut fn MyFn() {
                                   }
                               }
                               var mut a = new MyClass{};
                               var b: Fn() = a.MyFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var mut a: Fn() = todo!;
                               a();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn NonMutFn() {}
                               var a: Fn() = NonMutFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(){}
                               var a: () = SomeFn();
                               """
                }
            },
            new() { { "main.rf", "fn SomeFn(): () {}" } },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(){}
                               fn OtherFn(){}
                               var mut a = SomeFn;
                               a = OtherFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass{
                                   pub fn InstanceFn(mut a: i64): string { return ""; }
                                   pub static fn StaticFn(mut a: i64): string { return ""; }
                               }
                               fn GlobalFn(mut a: i64): string { return ""; }
                               var instance = new MyClass{};
                               var mut a = GlobalFn;
                               a = instance.InstanceFn;
                               a = MyClass::StaticFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(){}
                               var a: Fn() = SomeFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(a: i64){}
                               var a: Fn(i64) = SomeFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(a: i64): string {return "";}
                               var a: Fn(i64): string = SomeFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(mut a: i64){}
                               var a: Fn(mut i64) = SomeFn;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var mut a = (1, "");
                               a = (3, "hi");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: (i64, string);
                               a = (1, "");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {
                                   fn MyFn() {
                                       fn InnerFn(param: T): T {
                                           return param;
                                       }

                                       var a = InnerFn(todo!);
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {
                                   fn MyFn() {
                                       var a = new MyClass::<string>{};
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn<T>() {
                                   MyFn::<string>();
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   fn MyFn() {
                                       MyFn();
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   fn OtherFn() {}
                                   fn MyFn() {
                                       OtherFn();
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               static fn Outer() {
                                   var mut a = "";
                                   fn InnerFn() {
                                       a = "";
                                   }
                                   InnerFn();
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub mut fn DoSomething() {}
                               }
                               var mut a = new MyClass{};
                               var b = a.DoSomething;
                               b();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass { pub field Ignore: i64, pub field InstanceField: string, pub static field StaticField: i64 = 2 }
                               var a = new MyClass {Ignore = 1, InstanceField = ""};
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               static fn First(a: string) {
                                   fn Second() {
                                       fn Third() {
                                           var c = 1;
                                           fn Fourth() {
                                               var b = a;
                                               var d = c;
                                           }

                                           Fourth();
                                       }
                                       Third();
                                   }
                                   Second();
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union SomeUnion {A, B, C}
                               static fn SomeFn(): i64 {
                                   var a = SomeUnion::A;
                                   match (a) {
                                       SomeUnion::A => return 1,
                                       SomeUnion::B => return 2,
                                       _ => return 3
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               static fn SomeFn(): i64 {
                                   if (true) {
                                       match (true) {
                                           _ => return 3,
                                       }
                                   }
                                   else {
                                       if (true) {
                                           return 1;
                                       } else if (true) {
                                           return 2;
                                       } else {
                                           return 3;
                                       }
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B}
                               class MyClass { pub field MyField: MyUnion }

                               var a = new MyClass { MyField = MyUnion::A };
                               match (a) {
                                   MyClass { MyField: MyUnion } => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union OtherUnion {A, B}
                               union MyUnion {
                                   A(OtherUnion)
                               }
                               var a = MyUnion::A(OtherUnion::A);
                               match (a) {
                                   MyUnion::A(OtherUnion::A) => 1,
                                   MyUnion::A(OtherUnion::B) => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union OtherUnion {A, B}
                               union MyUnion {
                                   A(OtherUnion)
                               }
                               var a = MyUnion::A(OtherUnion::A);
                               match (a) {
                                   MyUnion::A(OtherUnion::A) => 1,
                                   MyUnion::A(var b) => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union OtherUnion {A, B}
                               union MyUnion {
                                   A(OtherUnion)
                               }
                               var a = MyUnion::A(OtherUnion::A);
                               match (a) {
                                   MyUnion::A(OtherUnion::A) => 1,
                                   MyUnion::A(var b) => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union OtherUnion {A, B}
                               union MyUnion {
                                   A(OtherUnion)
                               }
                               var a = MyUnion::A(OtherUnion::A);
                               match (a) {
                                   MyUnion::A(OtherUnion::A) => 1,
                                   MyUnion::A(_) => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A(string)
                               }
                               var a = MyUnion::A("");
                               match (a) {
                                   MyUnion::A(string) => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                                   pub field OtherField: i64
                               }
                               var a = new MyClass { MyField = "", OtherField = 2 };
                               match (a) {
                                   MyClass => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                                   pub field OtherField: i64
                               }
                               var a = new MyClass { MyField = "", OtherField = 2 };
                               match (a) {
                                   MyClass {_} => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                                   pub field OtherField: i64
                               }
                               var a = new MyClass { MyField = "", OtherField = 2 };
                               match (a) {
                                   MyClass {MyField: _, OtherField: _} => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                                   pub field OtherField: i64
                               }
                               var a = new MyClass { MyField = "", OtherField = 2 };
                               match (a) {
                                   MyClass {MyField: _, _} => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                                   pub field OtherField: i64
                               }
                               var a = new MyClass { MyField = "", OtherField = 2 };
                               match (a) {
                                   MyClass {MyField: string, OtherField: i64} => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                               }
                               var a = new MyClass { MyField = "" };
                               match (a) {
                                   MyClass { MyField: string } => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               // double field match fully exhausted
                               union OtherUnion {A, B}
                               class MyClass {
                                   pub field MyField: OtherUnion,
                                   pub field OtherField: OtherUnion
                               }
                               var a = new MyClass { MyField = OtherUnion::A, OtherField = OtherUnion::B };
                               match (a) {
                                   MyClass { MyField: OtherUnion::A, OtherField: OtherUnion::A } => 1,
                                   MyClass { MyField: OtherUnion::B, OtherField: OtherUnion::A } => 1,
                                   MyClass { MyField: OtherUnion::A, OtherField: OtherUnion::B } => 1,
                                   MyClass { MyField: OtherUnion::B, OtherField: OtherUnion::B } => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union OtherUnion {A, B}
                               class MyClass {
                                   pub field MyField: OtherUnion,
                                   pub field OtherField: OtherUnion
                               }
                               var a = new MyClass { MyField = OtherUnion::A, OtherField = OtherUnion::B };
                               match (a) {
                                   MyClass { MyField: OtherUnion::A, OtherField: _} => 1,
                                   MyClass { MyField: OtherUnion::B, OtherField: OtherUnion::A } => 1,
                                   MyClass { MyField: OtherUnion::B, OtherField: OtherUnion::B } => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union OtherUnion {A, B}
                               class MyClass {
                                   pub field MyField: OtherUnion,
                                   pub field OtherField: OtherUnion
                               }
                               var a = new MyClass { MyField = OtherUnion::A, OtherField = OtherUnion::B };
                               match (a) {
                                   MyClass { MyField: OtherUnion::A, OtherField: _ } => 1,
                                   MyClass { MyField: OtherUnion::B, OtherField: _ } => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union ThirdUnion { A, B }
                               union OtherUnion {
                                   A { field MyField: ThirdUnion },
                                   B
                               }
                               union MyUnion {
                                   A { field MyField: OtherUnion },
                                   B
                               }
                               var a = new MyUnion::A { MyField = OtherUnion::B };
                               match (a) {
                                   MyUnion::A { MyField: OtherUnion::A { MyField: ThirdUnion::A } } => 1,
                                   MyUnion::A { MyField: OtherUnion::A { MyField: ThirdUnion::B } } => 1,
                                   MyUnion::A { MyField: OtherUnion::B } => 1,
                                   MyUnion::B => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               // single field match fully exhaustive
                               union MyUnion {A, B}
                               class MyClass { pub field MyField: MyUnion }

                               var a = new MyClass { MyField = MyUnion::A };
                               match (a) {
                                   MyClass { MyField: MyUnion::A } => 1,
                                   MyClass { MyField: MyUnion::B } => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a;
                               if (true) {
                                   a = "";
                               }
                               else if (true) {
                                   a = "";
                               }
                               else {
                                   a = "";
                               }
                               var b = a;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a;
                               if (true) {
                                   a = "";
                               }
                               else {
                                   a = "";
                               }
                               var b: string;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a;
                               a = "";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyEnum {
                                   A(i64)
                               }

                               var a = MyEnum::A(1);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A(i64)
                               }

                               var a = MyUnion::A(1);
                               if (a matches MyUnion::A(_) var b) {
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(param: T){}

                               SomeFn::<>("");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               var z = a matches MyUnion::A(string var b);
                               // b is never used
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = "";
                               // match that exactly matches type is exhaustive if it's not a union
                               match (a) {
                                   string => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B}
                               class MyClass {pub field MyField: MyUnion}
                               var a = new MyClass {MyField = MyUnion::A};
                               match (a) {
                                   MyClass { MyField: MyUnion::A } => 1,
                                   MyClass { MyField: MyUnion::B } => 1,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B}
                               class MyClass {pub field MyField: MyUnion}
                               var a = new MyClass {MyField = MyUnion::A};
                               match (a) {
                                   MyClass => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B}
                               class MyClass {pub field MyField: MyUnion}
                               var a = new MyClass {MyField = MyUnion::A};
                               match (a) {
                                   MyClass { MyField: _ } => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B}
                               class MyClass {pub field MyField: MyUnion}
                               var a = new MyClass {MyField = MyUnion::A};
                               match (a) {
                                   MyClass { MyField: var b } => b
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B}
                               class MyClass {pub field MyField: MyUnion}
                               var a = new MyClass {MyField = MyUnion::A};
                               match (a) {
                                   var b => b
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B, C}
                               class MyClass {pub field MyField: MyUnion}
                               var a = new MyClass {MyField = MyUnion::A};
                               match (a) {
                                   MyClass { MyField: MyUnion::A } => 1,
                                   MyClass { MyField: var b } => 1
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn() {
                               }

                               // type parameters can have the same name as Functions
                               class MyClass<MyFn> {}
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               // type parameters can have the same name as Functions
                               class MyClass<MyFn> {
                                   fn MyFn() {}
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub field MyField: string,
                                   field OtherField: bool,

                                   pub static fn Create(): MyClass {
                                       return new MyClass {
                                           MyField = "",
                                           OtherField = true
                                       };
                                   }
                               }
                               var a = MyClass::Create();
                               var b: bool = a matches MyClass { MyField: string };
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var mut a = ok(1);
                               a = ok(2);
                               a = error("");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = SomeFn;

                               var b: string = a();
                               var c: string = a();

                               fn SomeFn<T>(): T {
                                   return todo!;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn OtherFn<T2>(param2: T2): T2 {
                                   return ThirdFn(param2);

                                   fn ThirdFn<T3>(param3: T3): T3 {
                                       return param3;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(param: T) {
                                   fn OtherFn<T2>(param2: T2) {
                                       fn ThirdFn<T3>(param3: T3) {
                                           var a: T = param;
                                           var b: T2 = param2;
                                           var c: T3 = param3;
                                       }
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(): i64 {
                                   var a = OtherFn();
                                   return a;
                               }

                               fn OtherFn<T>(): T {
                                   return todo!;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = SomeFn();
                               var a2 = SomeFn();
                               var b = a;
                               var b2 = a2;

                               var c = OtherFn(b);
                               var c2 = OtherFn(b2);

                               var d: string = c;
                               var d2: i64 = c2;

                               fn OtherFn<T>(param: T): T {
                                   return param;
                               }

                               fn SomeFn<T>(): T {
                                   return todo!;
                               }
                               """
                }
            },
            new() { { "main.rf", "var a: string = todo!" } },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn() {
                                   return todo!;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(): string {
                                   return todo!;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn<T>(param: T): T {
                                   return param;
                               }

                               var a = SomeFn("");
                               var b = SomeFn(2);
                               var c = b + b;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {
                                   fn SomeFn(param: T): result::<T, string> {
                                       if (true) {
                                           return ok(param);
                                       }
                                       return error("some error");
                                   }
                               }
                               """
                }
            },
            new() { { "main.rf", "var a: result::<i64, bool> = error(false);" } },
            new() { { "main.rf", "var a: result::<i64, bool> = ok(1);" } },
            new()
            {
                {
                    "main.rf", """
                               var a = match ("") {
                                   var b => ok(1),
                                   _ => error("")
                               };
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(): result::<i64, bool> {
                                   if (true) {
                                       return ok(1);
                                   }

                                   return error(false);
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn SomeFn(param: result::<i64, bool>) {
                               }

                               var a = ok(1);
                               SomeFn(a);
                               var b = error(false);
                               SomeFn(b);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   fn SomeFn() {
                                       var a: MyClass = this;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A,

                                   fn SomeFn() {
                                       var a: MyUnion = this;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A,
                                   B(i64),
                                   C { field MyField: i64 }
                               }
                               var a = MyUnion::A;
                               match (a) {
                                   MyUnion::A => 1,
                                   MyUnion::B(var b) => b,
                                   MyUnion::C { MyField } => MyField,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A,
                                   B(i64),
                                   C { field MyField: i64 }
                               }
                               var a = MyUnion::A;
                               match (a) {
                                   MyUnion::A => 1,
                                   MyUnion::B(var b) => b,
                                   _ => 2,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A,
                                   B(i64),
                                   C { field MyField: i64 }
                               }
                               var a = MyUnion::A;
                               var d: i64 = match (a) {
                                   MyUnion::A => 1,
                                   MyUnion::B(var b) => b,
                                   MyUnion::C { MyField } => MyField,
                               };
                               """
                }
            },
            new() { { "main.rf", "var a: bool = !(true);" } },
            new()
            {
                {
                    "main.rf", """
                               var a = (1, true, "");
                               var b: i64 = a.Item0;
                               var c: bool = a.Item1;
                               var d: string = a.Item2;
                               """
                }
            },
            new() { { "main.rf", "var a: i64 = (1 + 2) * 3;" } },
            new() { { "main.rf", "var a: bool = !true;" } },
            new()
            {
                {
                    "main.rf", """
                               var a = "";
                               var b: bool = a matches string;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a = 1;
                               var b: bool = a matches i64;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A}
                               var a = MyUnion::A;
                               var b: bool = a matches MyUnion;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A}
                               var a = MyUnion::A;
                               var b: bool = a matches _;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A, B(string)}
                               var a = MyUnion::B("");
                               var b: bool = a matches MyUnion::B(_);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A { field MyField: string }}
                               var a = new MyUnion::A { MyField = "" };
                               var b: bool = a matches MyUnion::A { MyField: _ };
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A { field MyField: string, field OtherField: bool }}
                               var a = new MyUnion::A { MyField = "", OtherField = true };
                               var b: bool = a matches MyUnion::A { MyField: _, _ };
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {pub field MyField: string}
                               var a = new MyClass { MyField = "" };
                               var b: bool = a matches MyClass { MyField: _ };
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {pub field MyField: string}
                               var a = new MyClass { MyField = "" };
                               var b: bool = a matches MyClass;
                               """
                }
            },
            new() { { "main.rf", ";;;;;;;;" } },
            new()
            {
                {
                    "main.rf", """
                               var a: string;
                               a = "";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   mut field MyField: string,

                                   mut fn MyFn() {
                                       MyField = "";
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   mut field MyField: string,

                                   mut fn MyFn() {
                                       mut fn InnerFn() {
                                           MyField = "";
                                       }
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   static field MyField: string = "",

                                   // instance functions have access to static fields
                                   fn MyFn(): string {
                                       return MyField;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   field MyField: string,

                                   // instance functions have access to instance fields
                                   fn MyFn(): string {
                                       return MyField;
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub mut field MyField: string
                               }

                               var mut a = new MyClass {
                                   MyField = ""
                               };

                               a.MyField = "";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass { pub mut field MyField: string }
                               var mut a = new MyClass { MyField = "" };

                               // a is not marked as mutable
                               a.MyField = "";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var mut a = "";
                               a = "";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub static mut field MyField: string = ""
                               }

                               MyClass::MyField = "";
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   pub mut field MyField: string
                               }
                               fn MyFn(mut param: MyClass) {
                                   param.MyField = "";
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn(mut param: string) {
                                  param = "";
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn(mut param: string) {
                               }
                               var mut a = "";

                               MyFn(a);
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {A}

                               var a: MyUnion = MyUnion::A;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion<T> {
                                   A {
                                       field MyField: T,
                                   }
                               }
                               var a: MyUnion::<string> = new MyUnion::<string>::A {
                                   MyField = ""
                               };
                               var b: MyUnion::<i64> = new MyUnion::<i64>::A {
                                   MyField = 1
                               };
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A {
                                       field MyField: string,
                                   }
                               }
                               var a: MyUnion = new MyUnion::A {
                                   MyField = ""
                               };
                               """
                }
            },
            new() { { "main.rf", "union MyUnion {}" } },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A,
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union myUnion {
                                   A(string)
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {}
                               union MyUnion {
                                   A(MyClass, string)
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A {
                                       field myField: string
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A
                               }
                               var a: MyUnion;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A(string)
                               }
                               var a = MyUnion::A("");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn(): result::<string, i64> {
                                   var a: string = OtherFn()?;
                                   return ok(a);
                               }

                               fn OtherFn(): result::<string, i64> {
                                   return result::<string, i64>::Error(1);
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               MyClass::StaticMethod();

                               class MyClass {
                                   pub static fn StaticMethod() {}
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: string = MyClass::<string>::StaticMethod("");

                               class MyClass<T> {
                                   pub static fn StaticMethod(param: T): T { return param; }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: i64 = MyClass::<string>::StaticMethod::<i64>("", 1);

                               class MyClass<T> {
                                   pub static fn StaticMethod<T2>(param: T, param2: T2): T2 { return param2; }
                               }
                               """
                }
            },
            new() { { "main.rf", "var a = 2" } },
            new() { { "main.rf", "var a: i64 = 2" } },
            new() { { "main.rf", "var b: string = \"somestring\"" } },
            new() { { "main.rf", "var a = 2; var b: i64 = a" } },
            new() { { "main.rf", "fn MyFn(): i64 { return 1; }" } },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn<T>(param: T): T {return param;}
                               var a: string = MyFn::<string>("");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {
                                   fn MyFn<T2>(param1: T, param2: T2) {
                                   }
                               }

                               var a = new MyClass::<i64>{};
                               a.MyFn::<string>(1, "");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn(){}
                               MyFn();
                               """
                }
            },
            new() { { "main.rf", "var a = 2;{var b = a;}" } },
            new() { { "main.rf", "fn Fn1(){Fn2();} fn Fn2(){}" } },
            new() { { "main.rf", "fn MyFn() {fn InnerFn() {OuterFn();}} fn OuterFn() {}" } },
            new() { { "main.rf", "fn MyFn() {fn InnerFn() {} InnerFn();}" } },
            new() { { "main.rf", "fn MyFn(param: i64) {var a: i64 = param;}" } },
            new() { { "main.rf", "fn MyFn(param1: string, param2: i64) {} MyFn(\"value\", 3);" } },
            new() { { "main.rf", "fn MyFn(param: result::<string, i64>) {}" } },
            new() { { "main.rf", "fn Fn1<T1>(){} Fn1::<string>();" } },
            new() { { "main.rf", "fn Fn1<T1, T2>(){} Fn1::<string, i64>();" } },
            new()
            {
                {
                    "main.rf", """
                               fn Fn1<T1>(param: T1): T1 { return param; }
                               var a: string = Fn1::<string>("");
                               var b: i64 = Fn1::<i64>(1);
                               """
                }
            },
            new() { { "main.rf", "if (true) {}" } },
            new() { { "main.rf", "if (false) {}" } },
            new() { { "main.rf", "var a = true; if (a) {}" } },
            new() { { "main.rf", "if (true) {} else {}" } },
            new()
            {
                {
                    "main.rf",
                    "if (true) {var a = 2} else if (true) {var a = 3} else if (true) {var a = 4} else {var a = 5}"
                }
            },
            new() { { "main.rf", "if (true) var a = 2" } },
            new() { { "main.rf", "var a: result::<i64, string>" } },
            new()
            {
                {
                    "main.rf", """
                               class Class1 { field someField: Class1,}
                               class Class2 { }
                               """
                }
            },
            // less than

            new() { { "main.rf", "var a: bool = 1 < 2;" } },
            // GreaterThan,
            new() { { "main.rf", "var a: bool = 2 > 2;" } },
            // Plus,
            new() { { "main.rf", "var a: i64 = 2 + 2;" } },
            // Minus,
            new() { { "main.rf", "var a: i64 = 2 - 2;" } },
            // Multiply,
            new() { { "main.rf", "var a: i64 = 2 * 2;" } },
            // Divide,
            new() { { "main.rf", "var a: i64 = 2 / 2;" } },
            // EqualityCheck,
            new() { { "main.rf", "var a: bool = 2 == 2;" } },
            // NegativeEqualityCheck,
            new() { { "main.rf", "var a: bool = 2 != 2;" } },
            // ValueAssignment,
            new() { { "main.rf", "var mut a = 2; a = 3;" } },
            // Object Initializers
            new()
            {
                {
                    "main.rf", """
                               class MyClass {pub field myField: i64, pub field otherField: string,}
                               var a = new MyClass { myField = 1, otherField = "" };
                               """
                }
            },
            new() { { "main.rf", "class MyClass {} var a: MyClass = new MyClass {};" } },
            new() { { "main.rf", "unboxed class MyClass {} var a: unboxed MyClass = new MyClass {};" } },
            new() { { "main.rf", "boxed class MyClass {} var a: boxed MyClass = new MyClass {};" } },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {pub field someField: i64,}
                               var a = new MyClass { someField = 1 };
                               var b: i64 = a.someField;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass { static field someField: i64 = 3, }
                               var a: i64 = MyClass::someField;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {
                                   fn MyFn(param: T): T {
                                       return param;
                                   }
                               }

                               var a = new MyClass::<string>{};

                               var b = a.MyFn;

                               var c = b("");
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> { static field someField: i64 = 1, }
                               var a = MyClass::<string>::someField;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> { pub field someField: T, }
                               var a = new MyClass::<i64> {someField = 1};
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> { pub field someField: T, }
                               var a = new MyClass::<string> {someField = ""};
                               var b: string = a.someField;
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion {
                                   A,

                                   fn SomeMethod() {
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   field someField: string,

                                   pub static fn New(): MyClass {
                                       return new MyClass {
                                           someField = ""
                                       };
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {}
                               class OtherClass<T> {}
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass<T> {
                                    fn MyFn<T2>() {
                                    }
                               }

                               var a = new MyClass::<string>{};
                               var b = a.MyFn::<i64>();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn OuterFn() {
                                  var a = new MyClass{};
                                  a.MyFn();
                               }
                               class MyClass {
                                   fn MyFn() {
                                       OuterFn();
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               class MyClass {
                                   fn MyFn() {
                                       var a = new MyClass{};
                                   }
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn(): string {
                                   fn InnerFn(): i64 { return 1; }
                                   return "";
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               fn MyFn(): result::<string, i64> {
                                   if (true) {
                                       return result::<string, i64>::Ok("someValue");
                                   }

                                   return result::<string, i64>::Error(1);
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               var a = MyUnion::A;
                               if (a matches MyUnion::A var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               if (a matches MyUnion::A(_) var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               if (a matches MyUnion::A(string var b)) {
                                   var c: string = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               if (a matches MyUnion::A(string var b)) {
                                   var c: string = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A { field MyField: string } }
                               var a = new MyUnion::A { MyField = "" };
                               if (a matches MyUnion::A { MyField }) {
                                   var c: string = MyField;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A { field MyField: string } }
                               var a = new MyUnion::A { MyField = "" };
                               if (a matches MyUnion::A { MyField: var b }) {
                                   var c: string = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               var a = MyUnion::A;
                               if (a matches var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               var a = MyUnion::A;
                               if (a matches MyUnion var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               union OtherUnion { B(MyUnion) }
                               var a = OtherUnion::B(MyUnion::A);
                               if (a matches OtherUnion::B(MyUnion::A var c) var b) {
                                   var d: OtherUnion = b;
                                   var e: MyUnion = c;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               var a = MyUnion::A;
                               if (false) {}
                               else if (a matches MyUnion::A var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               if (false) {}
                               else if (a matches MyUnion::A(_) var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               if (false) {}
                               else if (a matches MyUnion::A(string var b)) {
                                   var c: string = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A(string) }
                               var a = MyUnion::A("hi");
                               if (false) {}
                               else if (a matches MyUnion::A(string var b)) {
                                   var c: string = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A { field MyField: string } }
                               var a = new MyUnion::A { MyField = "" };
                               if (false) {}
                               else if (a matches MyUnion::A { MyField }) {
                                   var c: string = MyField;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A { field MyField: string } }
                               var a = new MyUnion::A { MyField = "" };
                               if (false) {}
                               else if (a matches MyUnion::A { MyField: var b }) {
                                   var c: string = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               var a = MyUnion::A;
                               if (false) {}
                               else if (a matches var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               var a = MyUnion::A;
                               if (false) {}
                               else if (a matches MyUnion var b) {
                                   var c: MyUnion = b;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               union MyUnion { A }
                               union OtherUnion { B(MyUnion) }
                               var a = OtherUnion::B(MyUnion::A);
                               if (false) {}
                               else if (a matches OtherUnion::B(MyUnion::A var c) var b) {
                                   var d: OtherUnion = b;
                                   var e: MyUnion = c;
                               }
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: i64;
                               fn SomeFn() {
                                   var b = a;
                               }
                               a = 1;
                               // SomeFn can be called safely now because a has been initialized
                               SomeFn();
                               """
                }
            },
            new()
            {
                {
                    "main.rf", """
                               var a: i64;
                               fn SomeFn() {
                                   var b = a;
                               }
                               a = 1;
                               // SomeFn can safely be accessed because a has been initialized
                               var c = SomeFn;
                               """
                }
            },
            new() { { "main.rf", Mvp } }
        ];

        return [.. sources];
    }

    public static TheoryData<string, Dictionary<string, (string contents, IReadOnlyList<TypeCheckerError> expectedErrors)>>
        FailedExpressionTestCases()
    {
        return new TheoryData<string, Dictionary<string, (string contents, IReadOnlyList<TypeCheckerError> expectedErrors)>>
        {
            {
                "non type parameters referenced in type constraint",
                new()
                {
                    {
                        "main.rf",
                        (
                            """
                            pub extern fn some_fn<T>() where T: unboxed [string]
                            """,
                            [TypeCheckerError.BoxedOnlyTypeCannotBeUnboxed(new ArrayType(String), SourceRange.Default)]
                        )
                    }
                }
            },
            {
                "non type parameters referenced in type constraint",
                new()
                {
                    {
                        "main.rf",
                        (
                            """
                            pub extern fn some_fn<T>() where int: unboxed T;
                            """,
                            [TypeCheckerError.NonTypeParameterConstrained(NamedTypeIdentifier("int"))]
                        )
                    }
                }
            },
            {
                "Extern function defines body",
                new()
                {
                    {
                        "main.rf",
                        (
                            """
                            pub extern fn some_fn<T>() where T: unboxed T2;
                            """,
                            [TypeCheckerError.SymbolNotFound(Token.Identifier("T2", SourceSpan.Default))]
                        )
                    }
                }
            },
            {
                "Extern function defines body",
                new()
                {
                    {
                        "main.rf",
                        (
                            """
                            pub extern fn some_fn(){}
                            """,
                            [TypeCheckerError.ExternFunctionDefinesBody(Token.Identifier("some_fn", SourceSpan.Default))]
                        )
                    }
                }
            },
            {
                "Non Extern function does not define body",
                new()
                {
                    {
                        "main.rf",
                        (
                            """
                            pub fn some_fn()
                            """,
                            [TypeCheckerError.NonExternFunctionDoesNotDefineBody(Token.Identifier("some_fn", SourceSpan.Default))]
                        )
                    }
                }
            },
            {
                "Top level statements in non main module",
                new()
                {
                    {
                        "main.rf",
                        (
                            """
                            var a = "";
                            """,
                            []
                        )
                    },
                    {
                        "other.rf",
                        (
                            """
                            var b = 2;
                            """,
                            [TypeCheckerError.TopLevelStatementsInNonMainModule(SourceRange.Default, new ModuleId("main:::other"))]
                        )
                    }
                }
            },
            {
                "Referencing Symbols not imported",
                new()
                {
                    {
                        "main.rf",
                        ("""
                        var a = new MyClass{};
                        var b = MyUnion::A;
                        SomeFn();
                        """,
                        [
                            TypeCheckerError.SymbolNotFound(Identifier("MyClass")),
                            TypeCheckerError.SymbolNotFound(Identifier("MyUnion")),
                            TypeCheckerError.SymbolNotFound(Identifier("SomeFn")),
                        ])
                    },
                    {
                        "otherModule.rf",
                        ("""
                        pub class MyClass{}
                        pub union MyUnion{A}
                        pub fn SomeFn(){}
                        """, [])
                    }
                }
            },
            {
                "relative import not relative to current module",
                new()
                {
                    {
                        "main.rf",
                        ("""
                        use subModule:::*;
                        """,
                        [TypeCheckerError.SymbolNotFound(Identifier("subModule"))])
                    },
                    {
                        "otherModule/otherModule.rf",
                        ("""
                        use subModule:::*;
                        """,
                        [])
                    },
                    {
                        "otherModule/subModule.rf",
                        ("""
                        pub fn SomeFn(){}
                        """,
                        [])
                    },
                }
            },
            {
                "Import non existent item",
                new()
                {
                    {
                        "main.rf",
                        ("""
                        use missing;
                        use otherModule:::foo;
                        {
                            use otherModule:::bar;
                        }
                        """,
                        [
                            TypeCheckerError.SymbolNotFound(Identifier("missing")),
                            TypeCheckerError.SymbolNotFound(Identifier("foo")),
                            TypeCheckerError.SymbolNotFound(Identifier("bar")),
                        ])
                    },
                    {
                        "otherModule.rf",
                        ("""

                        """,
                        [])
                    },
                }

            },
            {
                "Imported item not public",
                new()
                {
                    {
                        "main.rf",
                        ("""
                        use otherModule:::{MyClass, MyUnion, SomeFn};
                        """, [
                                            TypeCheckerError.ImportedItemNotPublic(Identifier("MyClass")),
                                            TypeCheckerError.ImportedItemNotPublic(Identifier("MyUnion")),
                                            TypeCheckerError.ImportedItemNotPublic(Identifier("SomeFn")),
                                        ])
                    },
                    {
                        "otherModule.rf",
                        ("""
                        class MyClass{}
                        union MyUnion{A}
                        fn SomeFn(){}
                        """, [])
                    }
                }
            },
            {
                "Reference non public item when using star import",
                new()
                {
                    {
                        "main.rf",
                        ("""
                        use otherModule:::*;
                        var a = new MyClass{};
                        var b = MyUnion::A;
                        SomeFn();
                        """,
                        [
                            TypeCheckerError.SymbolNotFound(Identifier("SomeFn")),
                        ])
                    },
                    {
                        "otherModule.rf",
                        ("""
                        pub class MyClass{}
                        pub union MyUnion{A}
                        fn SomeFn(){}
                        """, [])
                    }
                }
            },
            {
                "Reference non public item when using star import",
                new()
                {
                    {
                        "main.rf",
                        ("""
                        use otherModule:::*;
                        var a = new MyClass{};
                        var b = MyUnion::A;
                        SomeFn();
                        """, [
                                            TypeCheckerError.SymbolNotFound(Identifier("SomeFn")),
                                        ])
                    },
                    {
                        "otherModule.rf",
                        ("""
                        pub class MyClass{}
                        pub union MyUnion{A}
                        fn SomeFn(){}
                        """, [])
                    }
                }
            },
            {
                "Function object return type mutability mismatch",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn someFn(): string { return ""; }
                                   var a: Fn(): mut string = someFn;
                                   """, [TypeCheckerError.FunctionObjectReturnTypeMutabilityMismatch(SourceRange.Default)])
                    }
                }

            },
            {
                "assign non mutable variable to mutable field",
                new()
                {
                    {
                        "main.rf", ("""
                                   class FirstClass{ pub mut field FirstClassField: string }
                                   class MyClass{pub mut field MyField: FirstClass}
                                   var firstClass = new FirstClass{FirstClassField = ""};
                                   var secondClass = new MyClass{MyField = firstClass}
                                   """, [TypeCheckerError.NonMutableAssignment("firstClass", SourceRange.Default)])
                    }
                }

            },
            {
                "return non mutable variable through mutable return",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub mut field MyField: string, }
                                   fn someFn(myClass: MyClass): mut MyClass
                                   {
                                       return myClass;
                                   }

                                   var outer = new MyClass{MyField = ""};
                                   var mut a = someFn(outer);
                                   a.MyField = "bye";
                                   """, [TypeCheckerError.NonMutableExpressionPassedToMutableReturn(SourceRange.Default)])
                    }
                }

            },
            {
                "assign non mutable variable to mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{pub mut field MyField: string, }
                                   var a = new MyClass{MyField = ""};
                                   var mut b = a;
                                   b.MyField = "bye";
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "assign to field of object inside non mutable array",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { pub mut field MyField: string, }
                                   var a = [new MyClass{MyField = "hi"}];
                                   a[0].MyField = "bye";
                                   """, [
                                                       TypeCheckerError.NonMutableMemberOwnerAssignment(
                                                           IndexExpression(
                                                               VariableAccessor("a"),
                                                               Literal(0)))
                                                   ])
                    }
                }

            },
            {
                "assign to non mutable array",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = [""];
                                   a[0] = "hi";
                                   """, [
                                   TypeCheckerError.NonMutableMemberOwnerAssignment(VariableAccessor("a"))
                                   ])
                    }
                }

            },
            {
                "Incorrect array element type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: [string; 3] = [1, 2, 3];
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "Incorrect array length",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: [string; 3] = ["hi", "bye"];
                                   """, [TypeCheckerError.ArrayLengthMismatch(3, 2, SourceRange.Default)])
                    }
                }

            },
            {
                "Incorrect array length - fill collection expression",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: [string; 3] = ["hi"; 2];
                                   """, [TypeCheckerError.ArrayLengthMismatch(3, 2, SourceRange.Default)])
                    }
                }

            },
            {
                "different array element types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = ["", 1];
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "Incorrect array boxing - boxed",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: unboxed [string; 2] = ["", ""];
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(
                                   SourceRange.Default,
                                   new ArrayType(String, false, 2),
                                   false,
                                   new ArrayType(String, true, 2),
                                   true)
                                   ])
                    }
                }

            },
            {
                "Incorrect array boxing - unboxed",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: [string; 2] = [unboxed; "", ""];
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(
                                   SourceRange.Default,
                                   new ArrayType(String, true, 2),
                                   true,
                                   new ArrayType(String, false, 2),
                                   false)
                                   ])
                    }
                }

            },
            {
                "Incorrect array element boxing - unboxed",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a: [MyClass; 1] = [new unboxed MyClass{}];
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(
                                   SourceRange.Default,
                                   new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []),
                                   true,
                                   new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), [], false),
                                   false)
                                   ])
                    }
                }

            },
            {
                "Incorrect array element boxing - boxed",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: [i32; 1] = [box(1)];
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(
                                   SourceRange.Default,
                                   Int32,
                                   false,
                                   new UnspecifiedSizedIntType { Boxed = true },
                                   true)
                                   ])
                    }
                }

            },
            {
                "mismatched boxing for unbox method return value",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a: boxed MyClass = unbox(new MyClass{});
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), [], true),
                                   true, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), [], false), false)
                                   ])
                    }
                }

            },
            {
                "mismatched boxing for unbox method parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a: unboxed MyClass = unbox(new unboxed MyClass{});
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), [], true),
                                   true, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), [], false), false)
                                   ])
                    }
                }

            },
            {
                "mismatched boxing for box method return value",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: unboxed i32 = box(1);
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, Int32,
                                   false, UnspecifiedSizedIntType, true)
                                   ])
                    }
                }

            },
            {
                "mismatched boxing for box method parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   var number: boxed i32 = todo!;
                                   var a: boxed i32 = box(number);
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, Int32,
                                   false, Int32_Boxed, true)
                                   ])
                    }
                }

            },
            {
                "incorrect while loop check expression type",
                new()
                {
                    {
                        "main.rf", ("""
                                   while (1) {
                                   }
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "break outside of loop",
                new()
                {
                    {
                        "main.rf", ("""
                                   break;
                                   """, [TypeCheckerError.BreakUsedOutsideOfLoop(new BreakExpression(SourceRange.Default))])
                    }
                }

            },
            {
                "continue outside of loop",
                new()
                {
                    {
                        "main.rf", ("""
                                   continue;
                                   """, [TypeCheckerError.ContinueUsedOutsideOfLoop(new ContinueExpression(SourceRange.Default))])
                    }
                }

            },
            {
                "incompatible int types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64 = 1;
                                   var b: i32 = a;
                                   """, [MismatchedTypes(Int32, Int64)])
                    }
                }

            },
            {
                "incompatible inferred int types through generic",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   fn SomeFn<T>(param: T): T { return param; }
                                   var b: i32 = SomeFn(a);
                                   var c: u8 = a;
                                   """, [MismatchedTypes(UInt8, Int32)])
                    }
                }

            },
            {
                "incompatible inferred int types through int operation",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   var b = 2;
                                   var c: u8 = a * b;
                                   var d: u16 = a;
                                   var e: i32 = b;
                                   """, [
                                   MismatchedTypes(UInt16, UInt8),
                                   MismatchedTypes(Int32, UInt8)
                                   ])
                    }
                }

            },
            {
                "too many arguments for function object from tuple variant",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A(string)}
                                   var a = MyUnion::A;
                                   var b = a("", "");
                                   """, [
                                   TypeCheckerError.IncorrectNumberOfMethodArguments(
                                   MethodCall(VariableAccessor("a"), Literal(""), Literal("")), 1)
                                   ])
                    }
                }

            },
            {
                "not enough arguments for function object from tuple variant",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A(string)}
                                   var a = MyUnion::A;
                                   var b = a();
                                   """, [TypeCheckerError.IncorrectNumberOfMethodArguments(MethodCall(VariableAccessor("a")), 1)])
                    }
                }

            },
            {
                "incorrect type argument for function object from tuple variant",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A(string)}
                                   var a = MyUnion::A;
                                   var b = a(1);
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "union tuple variant with no members",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A()}
                                   """, [TypeCheckerError.EmptyUnionTupleVariant("MyUnion", Identifier("A"))])
                    }
                }

            },
            {
                "assign else if to variable without else",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = if (true) {} else if (true) {};
                                   """, [TypeCheckerError.IfExpressionValueUsedWithoutElseBranch(SourceRange.Default)])
                    }
                }

            },
            {
                "assign if to variable without else",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = if (true) {};
                                   """, [TypeCheckerError.IfExpressionValueUsedWithoutElseBranch(SourceRange.Default)])
                    }
                }

            },
            {
                "duplicate variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = "";
                                   var a = "";
                                   """, [TypeCheckerError.DuplicateVariableDeclaration(Identifier("a"))])
                    }
                }

            },
            {
                "assigning to 'this'",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass
                                   {
                                       mut fn SomeFn()
                                       {
                                           this = new MyClass{};
                                       }
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("this", SourceRange.Default)])
                    }
                }

            },
            {
                "closure accesses variable before declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn()
                                   {
                                       var b = a;
                                   }
                                   MyFn();

                                   var a = 1;
                                   """, [
                                   TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(Identifier("MyFn"),
                                   [Identifier("a")])
                                   ])
                    }
                }

            },
            {
                "closure accesses variable before declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn()
                                   {
                                       var b = a;
                                   }
                                   var c = MyFn;

                                   var a = 1;
                                   """, [
                                   TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(Identifier("MyFn"),
                                   [Identifier("a")])
                                   ])
                    }
                }

            },
            {
                "accessing closure before captured variables have been declared",
                new()
                {
                    {
                        "main.rf", ("""
                                   MyFn();
                                   var a = 1;
                                   fn MyFn()
                                   {
                                       var b = a;
                                   }
                                   """, [
                                   TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(Identifier("MyFn"),
                                   [Identifier("a")])
                                   ])
                    }
                }

            },
            {
                "matches - mutable variable declaration on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   if (a matches var mut b) {}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - type pattern mutable variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   if (a matches i64 var mut b) {}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - class pattern mutable variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a = new MyClass{};
                                   if (a matches MyClass{} var mut b) {}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - class pattern field mutable variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub field MyField: string}
                                   var a = new MyClass { MyField = "" };
                                   if (a matches MyClass{MyField: var mut b}) {}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - mutable union variant pattern on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A}
                                   var a = MyUnion::A;
                                   if (a matches MyUnion::A var mut b) {
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - mutable tuple variant pattern variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("");
                                   if (a matches MyUnion::A(_) var mut b) {
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - mutable variable declaration tuple member pattern on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A(string)
                                   }
                                   var a = MyUnion::A("");
                                   if (a matches MyUnion::A(var mut str)) {
                                       str = "hi";
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - mutable class variant pattern variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A{field MyField: string}}
                                   var a = new MyUnion::A { MyField = "" };
                                   if (a matches MyUnion::A{_} var mut b) {}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "matches - mutable class variant field pattern on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A { field MyField: string } }
                                   var a = new MyUnion::A { MyField = "" };
                                   if (a matches MyUnion::A { MyField: var mut b }) {}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - mutable variable declaration on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   match (a) { var mut b => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - type pattern mutable variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   match (a) { i64 var mut b => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - class pattern mutable variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a = new MyClass{};
                                   match (a) { MyClass{} var mut b => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - class pattern field mutable variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub field MyField: string}
                                   var a = new MyClass { MyField = "" };
                                   match (a) { MyClass{MyField: var mut b} => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - mutable union variant pattern on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A}
                                   var a = MyUnion::A;
                                   match (a) { MyUnion::A var mut b => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - mutable tuple variant pattern variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("");
                                   match (a) { MyUnion::A(_) var mut b => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - mutable variable declaration tuple member pattern on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A(string)
                                   }
                                   var a = MyUnion::A("");
                                   match (a) { MyUnion::A(var mut str) => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - mutable class variant pattern variable on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A{field MyField: string}}
                                   var a = new MyUnion::A { MyField = "" };
                                   match (a) { MyUnion::A{_} var mut b => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "match - mutable class variant field pattern on non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A { field MyField: string } }
                                   var a = new MyUnion::A { MyField = "" };
                                   match (a) {MyUnion::A { MyField: var mut b } => {}}
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "mutating non mutable pattern variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A(string)
                                   }
                                   var a = MyUnion::A("");
                                   if (a matches MyUnion::A(var str)) {
                                       str = "hi";
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("str", SourceRange.Default)])
                    }
                }

            },
            {
                "static field used in class pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub static field MyField: string = ""
                                   }
                                   var a = new MyClass{};
                                   if (a matches MyClass { MyField }){}
                                   """, [TypeCheckerError.StaticFieldInClassPattern(Identifier("MyField"))])
                    }
                }

            },
            {
                "non bool used in or",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1 || true
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "non bool used in or",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true || 1
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "non bool used in and",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1 && true
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "non bool used in and",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true && 1
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "missing field in instance method",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       field MyField: string,

                                       fn MyFn() {
                                           var a = MyField_;
                                       }
                                   }
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("MyField_"))])
                    }
                }

            },
            {
                "closure mutates non mutable variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   fn MyFn() {
                                       a = 2;
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "missing class static member",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a = MyClass::A;
                                   """, [TypeCheckerError.UnknownTypeMember(Identifier("A"), "MyClass")])
                    }
                }

            },
            {
                "missing class instance member",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   var a = new MyClass{};
                                   var b = a.b;
                                   """, [TypeCheckerError.UnknownTypeMember(Identifier("b"), "MyClass")])
                    }
                }

            },
            {
                "missing union static member",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{}
                                   var a = MyUnion::A;
                                   """, [TypeCheckerError.UnknownTypeMember(Identifier("A"), "MyUnion")])
                    }
                }

            },
            {
                "missing union instance member",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A}
                                   var a = MyUnion::A;
                                   var b = a.b;
                                   """, [TypeCheckerError.UnknownTypeMember(Identifier("b"), "MyUnion")])
                    }
                }

            },
            {
                "union class variant missing initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A {field MyField: string}
                                   }
                                   var a = MyUnion::A;
                                   """, [TypeCheckerError.UnionClassVariantWithoutInitializer(SourceRange.Default)])
                    }
                }

            },
            {
                "static class function accessed through member access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{pub static fn MyFn(){}}
                                   var a = new MyClass{};
                                   a.MyFn();
                                   """, [TypeCheckerError.InstanceMemberAccessOnStaticMember(SourceRange.Default)])
                    }
                }

            },
            {
                "static class field accessed through member access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{pub static field MyField: string = ""}
                                   var a = new MyClass{};
                                   a.MyField;
                                   """, [TypeCheckerError.InstanceMemberAccessOnStaticMember(SourceRange.Default)])
                    }
                }

            },
            {
                "static union function accessed through member access",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A, pub static fn MyFn(){} }
                                   var a = MyUnion::A;
                                   a.MyFn();
                                   """, [TypeCheckerError.InstanceMemberAccessOnStaticMember(SourceRange.Default)])
                    }
                }

            },
            {
                "instance class function accessed through static access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub fn MyFn(){}}
                                   MyClass::MyFn();
                                   """, [TypeCheckerError.StaticMemberAccessOnInstanceMember(SourceRange.Default)])
                    }
                }

            },
            {
                "instance class field accessed through static access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub field MyField: string}
                                   var a = MyClass::MyField;
                                   """, [TypeCheckerError.StaticMemberAccessOnInstanceMember(SourceRange.Default)])
                    }
                }

            },
            {
                "instance union function accessed through static access",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, pub fn MyFn(){}}
                                   MyUnion::MyFn();
                                   """, [TypeCheckerError.StaticMemberAccessOnInstanceMember(SourceRange.Default)])
                    }
                }

            },
            {
                "non function variable has type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1;
                                   var b = a::<string>;
                                   """, [TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(SourceRange.Default)])
                    }
                }

            },
            {
                "function variable has type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T>(){}
                                   var a = MyFn::<string>;
                                   var b = a::<string>;
                                   """, [TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(SourceRange.Default)])
                    }
                }

            },
            {
                "non function class member access has type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{pub field MyField: string};
                                   var a = new MyClass{MyField = ""};
                                   var b = a.MyField::<string>;
                                   """, [TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(SourceRange.Default)])
                    }
                }

            },
            {
                "union variant has type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A}
                                   var a = MyUnion::A::<>;
                                   """, [TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(SourceRange.Default)])
                    }
                }

            },
            {
                "union tuple variant has type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A(string)}
                                   var a = MyUnion::A::<>("");
                                   """, [TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(SourceRange.Default)])
                    }
                }

            },
            {
                "union unit variant has type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A}
                                   var a = MyUnion::A::<>;
                                   """, [TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(SourceRange.Default)])
                    }
                }

            },
            {
                "creating mutable function object",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub mut field MyField: string,

                                       pub mut fn MyFn() {
                                           MyField = "";
                                       }
                                   }
                                   var a = new MyClass {MyField = ""};
                                   var b = a.MyFn;
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "assigning value to unit type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: () = 1;
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Unit, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect tuple types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: (i64, string) = ("", 1);
                                   """, [
                                   TypeCheckerError.MismatchedTypes(SourceRange.Default, TupleType(null, Int64, String),
                                   TupleType(null, String, UnspecifiedSizedIntType))
                                   ])
                    }
                }

            },
            {
                "too many tuple members",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: (i64, string) = (1, "", 2);
                                   """, [
                                   TypeCheckerError.MismatchedTypes(SourceRange.Default, TupleType(null, Int64, String),
                                   TupleType(null, UnspecifiedSizedIntType, String, UnspecifiedSizedIntType))
                                   ])
                    }
                }

            },
            {
                "not enough tuple members",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: (i64, string, string) = (1, "");
                                   """, [
                                   TypeCheckerError.MismatchedTypes(SourceRange.Default, TupleType(null, Int64, String, String),
                                   TupleType(null, UnspecifiedSizedIntType, String))
                                   ])
                    }
                }

            },
            {
                "function type too many parameters",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64){}
                                   var a: Fn() = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit),
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "function type incorrect parameter type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64){}
                                   var a: Fn(string) = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit, [(isMut: false, parameterType: String)]),
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "function type not enough parameters",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(){}
                                   var a: Fn(i64) = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]),
                                   FunctionObject(Unit))
                                   ])
                    }
                }

            },
            {
                "function type incorrect return type when expected unit",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64): string {return "";}
                                   var a: Fn(i64) = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]),
                                   FunctionObject(String, [(isMut: false, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "function type expected return type but used void",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64) {}
                                   var a: Fn(i64): string = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(String, [(isMut: false, parameterType: Int64)]),
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "function type incorrect return type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64): i64 {return 1;}
                                   var a: Fn(i64): string = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(String, [(isMut: false, parameterType: Int64)]),
                                   FunctionObject(Int64, [(isMut: false, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "function type incorrect parameter mutability",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(mut a: i64){}
                                   var a: Fn(i64) = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]),
                                   FunctionObject(Unit, [(isMut: true, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "function type incorrect parameter mutability",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64){}
                                   var a: Fn(mut i64) = SomeFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit, [(isMut: true, parameterType: Int64)]),
                                   FunctionObject(Unit, [(isMut: false, parameterType: Int64)]))
                                   ])
                    }
                }

            },
            {
                "reassigning incompatible inferred function type ",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(){}
                                   fn OtherFn(): i64{return 1}
                                   var mut a = SomeFn;
                                   a = OtherFn;
                                   """, [
                                   TypeCheckerError.MismatchedTypes(
                                   SourceRange.Default,
                                   FunctionObject(Unit),
                                   FunctionObject(returnType: Int64))
                                   ])
                    }
                }

            },
            {
                "assign unknown variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   a = 2;
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("a"))])
                    }
                }

            },
            {
                "unresolved generic type when referencing the same generic type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T>() {
                                       MyFn();
                                   }
                                   """, [
                                   TypeCheckerError.UnresolvedInferredGenericType(MethodCall(VariableAccessor("MyFn")), "T")
                                   ])
                    }
                }

            },
            {
                "access instance method in static method - class",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       static fn StaticFn() {
                                           InstanceFn();
                                       }

                                       fn InstanceFn() {}
                                   }
                                   """, [TypeCheckerError.AccessInstanceMemberInStaticContext(Identifier("InstanceFn"))])
                    }
                }

            },
            {
                "access instance method in static method - union",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       static fn StaticFn() {
                                           InstanceFn();
                                       }

                                       fn InstanceFn() {}
                                   }
                                   """, [TypeCheckerError.AccessInstanceMemberInStaticContext(Identifier("InstanceFn"))])
                    }
                }

            },
            {
                "global function marked as mutable",
                new()
                {
                    {
                        "main.rf", ("""
                                   mut fn MyFn() {
                                   }
                                   """, [TypeCheckerError.GlobalFunctionMarkedAsMutable(Identifier("MyFn"))])
                    }
                }

            },
            {
                "deeply nested if branch doesn't return",
                new()
                {
                    {
                        "main.rf", ("""
                                   static fn SomeFn(): i64 {
                                       if (true) {
                                           match (true) {
                                               _ => return 3,
                                           }
                                       }
                                       else {
                                           if (true) {
                                               return 1;
                                           } else if (true) {
                                           } else {
                                               return 3;
                                           }
                                       }
                                   }
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Int64, Unit)])
                    }
                }

            },
            {
                "static method doesn't return value",
                new()
                {
                    {
                        "main.rf", ("""
                                   static fn SomeFn(): i64 {}
                                   """, [TypeCheckerError.MismatchedTypes(SourceRange.Default, Int64, Unit)])
                    }
                }

            },
            {
                "static class method doesn't return value",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { static fn SomeFn(): i64 {}}
                                   """, [MismatchedTypes(Int64, Unit)])
                    }
                }

            },
            {
                "static union method doesn't return value",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { static fn SomeFn(): i64 {}}
                                   """, [MismatchedTypes(Int64, Unit)])
                    }
                }

            },
            {
                "method doesn't return from all if branches",
                new()
                {
                    {
                        "main.rf", ("""
                                   static fn SomeFn(): i64 {
                                       if (true) {
                                           return 1;
                                       }
                                   }
                                   """, [MismatchedTypes(Int64, Unit)])
                    }
                }

            },
            {
                "method doesn't return from all if branches",
                new()
                {
                    {
                        "main.rf", ("""
                                   static fn SomeFn(): i64 {
                                       if (true) {
                                           return 1;
                                       } else if (true) {
                                           return 1;
                                       }
                                   }
                                   """, [MismatchedTypes(Int64, Unit)])
                    }
                }

            },
            {
                "method doesn't return from all if branches",
                new()
                {
                    {
                        "main.rf", ("""
                                   static fn SomeFn(): i64 {
                                       if (true) {
                                       }
                                       else {
                                           return 1;
                                       }
                                   }
                                   """, [MismatchedTypes(Int64, Unit)])
                    }
                }

            },
            {
                "method doesn't return from all if branches",
                new()
                {
                    {
                        "main.rf", ("""
                                   static fn SomeFn(): i64 {
                                       if (true) {
                                           return 1;
                                       }
                                       else if (true) {
                                       }
                                       else {
                                           return 1;
                                       }
                                   }
                                   """, [MismatchedTypes(Int64, Unit)])
                    }
                }

            },
            {
                "mutating instance field from non mutable inner function",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       mut field MyField: string,

                                       mut fn MyFn() {
                                           fn InnerFn() {
                                               MyField = "";
                                           }
                                       }
                                   }
                                   """, [TypeCheckerError.MutatingInstanceInNonMutableFunction("InnerFn", SourceRange.Default)])
                    }
                }

            },
            {
                "creating mutable inner function from non mutable parent function",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       mut field MyField: string,

                                       fn MyFn() {
                                           mut fn InnerFn() {
                                               MyField = "";
                                           }
                                       }
                                   }
                                   """, [TypeCheckerError.MutableFunctionWithinNonMutableFunction(SourceRange.Default)])
                    }
                }

            },
            {
                "static method marked as mutable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub static mut fn SomeFn() {}
                                   }
                                   """, [TypeCheckerError.StaticFunctionMarkedAsMutable("SomeFn", SourceRange.Default)])
                    }
                }

            },
            {
                "Calling mut instance function with non mut variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub mut field SomeField: string,

                                       pub mut fn DoSomething() {
                                           SomeField = "";
                                       }
                                   }
                                   var a = new MyClass { SomeField = "" };
                                   a.DoSomething();
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "negate non integer",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = -"";
                                   """, [MismatchedTypes([Int8, Int16, Int32, Int64], String)])
                    }
                }

            },
            {
                "negate unsigned value",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: u16 = 1;
                                   var b = -a;
                                   """, [MismatchedTypes([Int8, Int16, Int32, Int64], UInt16)])
                    }
                }

            },
            {
                "Assigning mut instance reference with non mut variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub mut fn DoSomething() {}
                                   }
                                   var a = new MyClass{};
                                   var b = a.DoSomething;
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "Mutating from non mutable instance function",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub mut field SomeField: string,

                                       pub fn DoSomething() {
                                           SomeField = "";
                                       }
                                   }

                                   var a = new MyClass { SomeField = "" };
                                   a.DoSomething();
                                   """, [TypeCheckerError.MutatingInstanceInNonMutableFunction("DoSomething", SourceRange.Default)])
                    }
                }

            },
            {
                "Single field class exhaustive fail",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, B}
                                   class MyClass { pub field MyField: MyUnion }

                                   var a = new MyClass { MyField = MyUnion::A };
                                   match (a) {
                                       MyClass { MyField: MyUnion::A } => 1,
                                   }
                                   """, [TypeCheckerError.MatchNonExhaustive(SourceRange.Default)])
                    }
                }

            },
            {
                "double field class not all combinations of union variants are matched",
                new()
                {
                    {
                        "main.rf", ("""
                                   union OtherUnion {A, B}
                                   class MyClass {
                                       pub field MyField: OtherUnion,
                                       pub field OtherField: OtherUnion
                                   }
                                   var a = new MyClass { MyField = OtherUnion::A, OtherField = OtherUnion::B };
                                   match (a) {
                                       MyClass { MyField: OtherUnion::A, OtherField: OtherUnion::A } => 1,
                                       MyClass { MyField: OtherUnion::B, OtherField: OtherUnion::A } => 1,
                                       MyClass { MyField: OtherUnion::A, OtherField: OtherUnion::B } => 1,
                                   }
                                   """, [TypeCheckerError.MatchNonExhaustive(SourceRange.Default)])
                    }
                }

            },
            {
                "Deeply nested non exhaustive match",
                new()
                {
                    {
                        "main.rf", ("""
                                   union ThirdUnion { A, B }
                                   union OtherUnion {
                                       A { field MyField: ThirdUnion },
                                       B
                                   }
                                   union MyUnion {
                                       A { field MyField: OtherUnion },
                                       B
                                   }
                                   var a = new MyUnion::A { MyField = OtherUnion::B };
                                   match (a) {
                                       MyUnion::A { MyField: OtherUnion::A { MyField: ThirdUnion::B } } => 1,
                                       MyUnion::A { MyField: OtherUnion::B } => 1,
                                       MyUnion::B => 1,
                                   }
                                   """, [TypeCheckerError.MatchNonExhaustive(SourceRange.Default)])
                    }
                }

            },
            {
                "tuple pattern match not exhaustive",
                new()
                {
                    {
                        "main.rf", ("""
                                   union OtherUnion {A, B}
                                   union MyUnion {
                                       A(OtherUnion)
                                   }
                                   var a = MyUnion::A(OtherUnion::A);
                                   match (a) {
                                       MyUnion::A(OtherUnion::A) => 1,
                                   }
                                   """, [TypeCheckerError.MatchNonExhaustive(SourceRange.Default)])
                    }
                }

            },
            {
                "type inference from same function variable into two variables",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = SomeFn;

                                   var b: string = a();
                                   var c: i64 = a();

                                   fn SomeFn<T>(): T {
                                       return todo!;
                                   }
                                   """, [MismatchedTypes(Int64, String)])
                    }
                }

            },
            {
                "Static local function cannot access local variables",
                new()
                {
                    {
                        "main.rf", ("""
                                   pub static fn SomeFn() {
                                       var a = "";
                                       static fn InnerFn(): string {
                                           return a;
                                       }
                                   }
                                   """, [TypeCheckerError.StaticLocalFunctionAccessesOuterVariable(Identifier("a"))])
                    }
                }

            },
            {
                "nested function parameter mismatched types return types",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn<T>(param: T) {
                                       fn OtherFn<T2>(param2: T2) {
                                           fn ThirdFn<T3>(param3: T3): T2 {
                                               var a: T = param;
                                               var b: T2 = param2;
                                               var c: T3 = param3;

                                               return a;
                                           }
                                       }
                                   }
                                   """, [MismatchedTypes(GenericPlaceholder("T2"), GenericPlaceholder("T"))])
                    }
                }

            },
            {
                "incompatible inferred types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var mut a = ok(1);
                                   a = ok(true);
                                   a = error("");
                                   """, [
                                   MismatchedTypes(Result(UnspecifiedSizedIntType, null),
                                   Result(Boolean, null))
                                   ])
                    }
                }

            },
            {
                "incompatible inferred result type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: result::<i64, string> = error(1);
                                   """, [MismatchedTypes(Result(Int64, String), Result(Int64, UnspecifiedSizedIntType))])
                    }
                }

            },
            {
                "this used outside of class instance",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = this;
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("this"))])
                    }
                }

            },
            {
                "this used in static class function",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       static fn SomeFn() {
                                           var a = this;
                                       }
                                   }
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("this"))])
                    }
                }

            },
            {
                "this used in static union function",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       static fn SomeFn() {
                                           var a = this;
                                       }
                                   }
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("this"))])
                    }
                }

            },
            {
                "pattern variable used in wrong match arm",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A,
                                       B(i64),
                                       C { field MyField: i64 }
                                   }
                                   var a = MyUnion::A;
                                   match (a) {
                                       MyUnion::A => 1,
                                       MyUnion::B(var b) => b,
                                       MyUnion::C { MyField } => b,// b not available in this arm
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "match expression not exhaustive",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A,
                                       B(i64),
                                       C { field MyField: i64 }
                                   }
                                   var a = MyUnion::A;
                                   match (a) {
                                       MyUnion::A => 1,
                                       MyUnion::B(var b) => b,
                                       // non exhaustive
                                   }
                                   """, [TypeCheckerError.MatchNonExhaustive(SourceRange.Default)])
                    }
                }

            },
            {
                "match expression not exhaustive",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, B}
                                   class MyClass {pub field MyField: MyUnion}
                                   var a = new MyClass {MyField = MyUnion::A};
                                   match (a) {
                                       MyClass { MyField: MyUnion::A } => 1
                                   }
                                   """, [TypeCheckerError.MatchNonExhaustive(SourceRange.Default)])
                    }
                }

            },
            {
                "incompatible pattern type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = "";
                                   match (a) {
                                       i64 => 1, // incompatible pattern
                                       _ => 2
                                   }
                                   """, [MismatchedTypes(String, Int64)])
                    }
                }

            },
            {
                "Unknown type used in pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = "";
                                   match (a) {
                                       SomeType => 1, // missing type
                                       _ => 1
                                   }
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("SomeType"))])
                    }
                }

            },
            {
                "match arms provide incompatible types",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, B}
                                   var a = MyUnion::A;
                                   var b = match (a) {
                                       MyUnion::A => 1,
                                       MyUnion::B => "" // mismatched arm expression types
                                   }
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, String)])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   var a = MyUnion::A;
                                   var z = a matches MyUnion::A var b;

                                   var c: MyUnion = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("hi");
                                   var z = a matches MyUnion::A(_) var b;
                                   var c: MyUnion = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("hi");
                                   var z = a matches MyUnion::A(string var b);
                                   var c: string = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("hi");
                                   var z = a matches MyUnion::A(string var b);
                                   var c: string = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "union class pattern field used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A { field MyField: string } }
                                   var a = new MyUnion::A { MyField = "" };
                                   var z = a matches MyUnion::A { MyField };
                                   var c: string = MyField;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("MyField"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A { field MyField: string } }
                                   var a = new MyUnion::A { MyField = "" };
                                   var z = a matches MyUnion::A { MyField: var b };
                                   var c: string = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   var a = MyUnion::A;
                                   var z = a matches var b;
                                   var c: MyUnion = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   var a = MyUnion::A;
                                   var z = a matches MyUnion var b;
                                   var c: MyUnion = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used outside of true if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   union OtherUnion { B(MyUnion) }
                                   var a = OtherUnion::B(MyUnion::A);
                                   var z = a matches OtherUnion::B(MyUnion::A var c) var b;
                                   var d: OtherUnion = b;
                                   var e: MyUnion = c;
                                   """, [
                                   TypeCheckerError.AccessUninitializedVariable(Identifier("b")),
                                   TypeCheckerError.AccessUninitializedVariable(Identifier("c"))
                                   ])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   var a = MyUnion::A;
                                   if (!(a matches MyUnion::A var b)) {
                                       var c: MyUnion = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("hi");
                                   if (!(a matches MyUnion::A(_) var b)) {
                                       var c: MyUnion = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("hi");
                                   if (!(a matches MyUnion::A(string var b))) {
                                       var c: string = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A(string) }
                                   var a = MyUnion::A("hi");
                                   if (!(a matches MyUnion::A(string var b))) {
                                       var c: string = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A { field MyField: string } }
                                   var a = new MyUnion::A { MyField = "" };
                                   if (!(a matches MyUnion::A { MyField })) {
                                       var c: string = MyField;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("MyField"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A { field MyField: string } }
                                   var a = new MyUnion::A { MyField = "" };
                                   if (!(a matches MyUnion::A { MyField: var b })) {
                                       var c: string = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   var a = MyUnion::A;
                                   if (!(a matches var b)) {
                                       var c: MyUnion = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   var a = MyUnion::A;
                                   if (!(a matches MyUnion var b)) {
                                       var c: MyUnion = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "matches pattern variable used in false if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion { A }
                                   union OtherUnion { B(MyUnion) }
                                   var a = OtherUnion::B(MyUnion::A);
                                   if (!(a matches OtherUnion::B(MyUnion::A var c) var b)) {
                                       var d: OtherUnion = b;
                                       var e: MyUnion = c;
                                   }
                                   """, [
                                   TypeCheckerError.AccessUninitializedVariable(Identifier("b")),
                                   TypeCheckerError.AccessUninitializedVariable(Identifier("c"))
                                   ])
                    }
                }

            },
            {
                "mismatched variable declaration assignment in union method",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A,

                                       fn SomeMethod() {
                                           var a: bool = 1;
                                       }
                                   }
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect not operator expression type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = !1
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "missing type in pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   var b: bool = 1 matches MissingType;
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("MissingType"))])
                    }
                }

            },
            {
                "extra patterns in union tuple pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, B(string)}
                                   var a = MyUnion::B("");
                                   var b: bool = a matches MyUnion::B(_, _);
                                   """, [
                                   TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(UnionTupleVariantPattern(
                                   NamedTypeIdentifier("MyUnion"),
                                   "B",
                                   [new DiscardPattern(SourceRange.Default), new DiscardPattern(SourceRange.Default)]),
                                   1)
                                   ])
                    }
                }

            },
            {
                "missing patterns in union tuple pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, B(string, bool, i64)}
                                   var a = MyUnion::B("", true, 1);
                                   var b: bool = a matches MyUnion::B(_, _);
                                   """, [
                                   TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(UnionTupleVariantPattern(
                                   NamedTypeIdentifier("MyUnion"),
                                   "B",
                                   [new DiscardPattern(SourceRange.Default), new DiscardPattern(SourceRange.Default)]),
                                   3)
                                   ])
                    }
                }

            },
            {
                "Unknown variant used in matches union variant pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, B(string)}
                                   var a = MyUnion::B("");
                                   var b: bool = a matches MyUnion::C;
                                   """, [TypeCheckerError.UnknownTypeMember(Identifier("C"), "MyUnion")])
                    }
                }

            },
            {
                "mismatched type used for field in class variant pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A { field MyField: string }}
                                   var a = new MyUnion::A { MyField = "" };
                                   var b: bool = a matches MyUnion::A { MyField: i64 };
                                   """, [MismatchedTypes(String, Int64)])
                    }
                }

            },
            {
                "variant name not specified for class variant pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A { field MyField: string }}
                                   var a = new MyUnion::A { MyField = "" };
                                   var b: bool = a matches MyUnion { MyField: i64 };
                                   """, [TypeCheckerError.NonClassUsedInClassPattern(NamedTypeIdentifier("MyUnion"))])
                    }
                }

            },
            {
                "class variant pattern does not list all fields",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A { field MyField: string, field OtherField: bool }}
                                   var a = new MyUnion::A { MyField = "", OtherField = true };
                                   var b: bool = a matches MyUnion::A { MyField: _ };
                                   """, [
                                   TypeCheckerError.MissingFieldsInUnionClassVariantPattern(UnionClassVariantPattern(
                                   NamedTypeIdentifier("MyUnion"),
                                   "A"),
                                   ["OtherField"])
                                   ])
                    }
                }

            },
            {
                "incompatible field type used in class pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub field MyField: string}
                                   var a = new MyClass { MyField = "" };
                                   var b: bool = a matches MyClass { MyField: i64 };
                                   """, [MismatchedTypes(String, Int64)])
                    }
                }

            },
            {
                "class pattern does not list all fields",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub field MyField: string, pub field OtherField: bool}
                                   var a = new MyClass { MyField = "", OtherField = true };
                                   var b: bool = a matches MyClass { MyField: string };
                                   """, [TypeCheckerError.MissingFieldsInClassPattern(["OtherField"], NamedTypeIdentifier("MyClass"))])
                    }
                }

            },
            {
                "non public field in class pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub field MyField: string,
                                       field OtherField: bool,

                                       pub static fn Create(): MyClass {
                                           return new MyClass {
                                               MyField = "",
                                               OtherField = true
                                           };
                                       }
                                   }
                                   var a = MyClass::Create();
                                   var b: bool = a matches MyClass { MyField: string, OtherField: _ };
                                   """, [TypeCheckerError.PrivateFieldReferenced(Identifier("OtherField"))])
                    }
                }

            },
            {
                "mismatched pattern type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {pub field MyField: string, pub field OtherField: bool}
                                   var a = new MyClass { MyField = "", OtherField = true };
                                   var b: bool = a matches string;
                                   """, [MismatchedTypes(new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), String)])
                    }
                }

            },
            {
                "class initializer sets private field",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       field MyField: string
                                   }

                                   // MyField is not accessible
                                   var a = new MyClass { MyField = "" };
                                   """, [TypeCheckerError.PrivateFieldReferenced(Identifier("MyField"))])
                    }
                }

            },
            {
                "non mutable field assigned in class method",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       field MyField: string,

                                       mut fn MyFn() {
                                           MyField = "";
                                       }
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("MyField", SourceRange.Default)])
                    }
                }

            },
            {
                "instance field used in static class method",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       field MyField: string,

                                       static fn MyFn() {
                                           var a = MyField;
                                       }
                                   }
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("MyField"))])
                    }
                }

            },
            {
                "non mutable variable assigned twice",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: string;
                                   // initial assignment succeeds
                                   a = "";
                                   // second assignment fails because it's not mutable
                                   a = ";";
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "non mutable field assigned through member access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub field MyField: string
                                   }

                                   var mut a = new MyClass {
                                       MyField = ""
                                   };

                                   // MyField is not mutable
                                   a.MyField = "";
                                   """, [TypeCheckerError.NonMutableMemberAssignment(MemberAccess(VariableAccessor("a"), "MyField"))])
                    }
                }

            },
            {
                "mutable field assigned from non mutable instance variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { pub mut field MyField: string }
                                   var a = new MyClass { MyField = "" };

                                   // a is not marked as mutable
                                   a.MyField = "";
                                   """, [TypeCheckerError.NonMutableMemberOwnerAssignment(VariableAccessor("a"))])
                    }
                }

            },
            {
                "non mutable variable assigned",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = "";
                                   // a is not marked as mutable
                                   a = "";
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "non mutable static field assigned through static member access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub static field MyField: string = ""
                                   }

                                   // MyField is not marked as mutable
                                   MyClass::MyField = "";
                                   """, [
                                   TypeCheckerError.NonMutableMemberAssignment(StaticMemberAccess(NamedTypeIdentifier("MyClass"),
                                   "MyField"))
                                   ])
                    }
                }

            },
            {
                "non mutable param member assigned",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub mut field MyField: string
                                   }
                                   fn MyFn(param: MyClass) {
                                       // param is not marked as mutable
                                       param.MyField = "";
                                   }
                                   """, [TypeCheckerError.NonMutableMemberOwnerAssignment(VariableAccessor("param"))])
                    }
                }

            },
            {
                "not mutable param assigned",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(param: string) {
                                      // param is not marked as mutable
                                      param = "";
                                   }
                                   """, [TypeCheckerError.NonMutableAssignment("param", SourceRange.Default)])
                    }
                }

            },
            {
                "non mutable variable passed to mutable function parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(mut param: string) {
                                   }
                                   var a = "";

                                   // param is mut, but a is not marked as mutable
                                   MyFn(a);
                                   """, [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)])
                    }
                }

            },
            {
                "mismatched type when assigning field to generic field",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion<T> {
                                       A {
                                           field MyField: T,
                                       }
                                   }
                                   var a: MyUnion::<string> = new MyUnion::<string>::A {
                                       MyField = 2
                                   };
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "unknown field assigned in class variant initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A {
                                           field MyField: string
                                       }
                                   }
                                   var a: MyUnion = new MyUnion::A {
                                       MyField_ = ""
                                   };
                                   """, [TypeCheckerError.UnknownField(Identifier("MyField_"), "union variant MyUnion::A")])
                    }
                }

            },
            {
                "Unknown variant name used in union class variant initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A {
                                           field MyField: string
                                       }
                                   }
                                   var a: MyUnion = new MyUnion::B {
                                       MyField = ""
                                   };
                                   """, [TypeCheckerError.UnknownTypeMember(Identifier("B"), "MyUnion")])
                    }
                }

            },
            {
                "incorrect expression type used in union class variant initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A {
                                           field MyField: string
                                       }
                                   }
                                   var a: MyUnion = new MyUnion::A {
                                       MyField = 2
                                   };
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "Union class variant initializer used for tuple union variant",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A(string)
                                   }
                                   var a: MyUnion = new MyUnion::A {
                                       MyField = 2
                                   };
                                   """, [TypeCheckerError.UnionClassVariantInitializerNotClassVariant(Identifier("A"))])
                    }
                }

            },
            {
                "incorrect expression type used in union class variant initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A {
                                           field MyField: string,
                                       }
                                   }
                                   var a: MyUnion = new MyUnion::A {
                                       MyField = 2
                                   };
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect expression type used in union tuple",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A(string)
                                   }
                                   var a = MyUnion::A(1);
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "duplicate union variant name in union declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A,
                                       A
                                   }
                                   """, [TypeCheckerError.DuplicateVariantName(Identifier("A"))])
                    }
                }

            },
            {
                "duplicate union names",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {} union MyUnion {}
                                   """, [TypeCheckerError.ConflictingTypeName(Identifier("MyUnion"))])
                    }
                }

            },
            {
                "duplicate union names with type errors",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {A, A} union MyUnion {B, B}
                                   """, [
                                   TypeCheckerError.ConflictingTypeName(Identifier("MyUnion")),
                                   TypeCheckerError.DuplicateVariantName(Identifier("A")),
                                   TypeCheckerError.DuplicateVariantName(Identifier("B"))
                                   ])
                    }
                }

            },
            {
                "union name conflicts with class name",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {} class MyUnion {}
                                   """, [TypeCheckerError.ConflictingTypeName(Identifier("MyUnion"))])
                    }
                }

            },
            {
                "duplicate class name with inner errors",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       fn MyFn() {},
                                       fn MyFn() {}
                                   }
                                   class MyClass {
                                       fn OtherFn() {},
                                       fn OtherFn() {}
                                   }
                                   """, [
                                   TypeCheckerError.ConflictingTypeName(Identifier("MyClass")),
                                   TypeCheckerError.ConflictingFunctionName(Identifier("MyFn")),
                                   TypeCheckerError.ConflictingFunctionName(Identifier("OtherFn"))
                                   ])
                    }
                }

            },
            {
                "duplicate field in union class variant",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {
                                       A {
                                           field MyField: string,
                                           field MyField: string,
                                       }
                                   }
                                   """, [
                                   TypeCheckerError.DuplicateFieldInUnionClassVariant(
                                   Identifier("MyUnion"),
                                   Identifier("A"),
                                   Identifier("MyField"))
                                   ])
                    }
                }

            },
            {
                "incorrect type arguments for return value of same type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(): result::<i64, string> {
                                       if (true) {
                                           return result::<string, i64>::Ok("someValue");
                                       }

                                       return result::<string, i64>::Error(1);
                                   }
                                   """, [
                                   MismatchedTypes(Result(Int64, String), Result(String, Int64)),
                                   MismatchedTypes(Result(Int64, String), Result(String, Int64))
                                   ])
                    }
                }

            },
            {
                "incorrect expression type for generic type in union tuple variant",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = result::<string, i64>::Ok(1);
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect expression type for generic type in generic class and generic method call",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T> {
                                       fn MyFn<T2>(param1: T, param2: T2) {
                                       }
                                   }

                                   var a = new MyClass::<i64>{};
                                   a.MyFn::<string>("", 1);
                                   """, [MismatchedTypes(Int64, String), MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type used in generic class and generic method call",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T> {
                                       fn MyFn<T2>(param1: T, param2: T2) {
                                       }
                                   }

                                   var a = new MyClass::<i64>{};
                                   a.MyFn::<string>("", "");
                                   """, [MismatchedTypes(Int64, String)])
                    }
                }

            },
            {
                "incorrect expression type used in generic class and generic method",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T> {
                                       fn MyFn<T2>(param1: T, param2: T2) {
                                       }
                                   }

                                   var a = new MyClass::<i64>{};
                                   a.MyFn::<string>(1, 1);
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect expression type in variable assignment",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: string = 2
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect expression type in variable assignment",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64 = "somestring"
                                   """, [MismatchedTypes(Int64, String)])
                    }
                }

            },
            {
                "variable declaration without type or assignment never inferred",
                new()
                {
                    {
                        "main.rf", ("""
                                   var b;
                                   """, [TypeCheckerError.UnresolvedInferredVariableType(Identifier("b"))])
                    }
                }

            },
            {
                "variable declaration without type or assignment never inferred",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a;
                                   if (true) {
                                       a = "";
                                   }
                                   else if (true) {
                                       a = "";
                                   }
                                   var b = a;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("a"))])
                    }
                }

            },
            {
                "variable declaration without type or assignment never inferred",
                new()
                {
                    {
                        "main.rf", ("""
                                   var b;
                                   if (true) {
                                       b = "";
                                   }
                                   var a: string = b;
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "variable declaration without type or assignment never inferred",
                new()
                {
                    {
                        "main.rf", ("""
                                   var b;
                                   if (true) {
                                       b = "";
                                   }
                                   else {
                                       var c = b;
                                   }
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))])
                    }
                }

            },
            {
                "incorrect type in return value",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(): i64 { return "something"; }
                                   """, [MismatchedTypes(Int64, String)])
                    }
                }

            },
            {
                "return value for function without return type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn() { return 1; }
                                   """, [MismatchedTypes(Unit, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "duplicate function declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn() {} fn MyFn() {}
                                   """, [TypeCheckerError.ConflictingFunctionName(Identifier("MyFn"))])
                    }
                }

            },
            {
                "duplicate function declaration in union",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion {fn MyFn() {} fn MyFn() {}}
                                   """, [TypeCheckerError.ConflictingFunctionName(Identifier("MyFn"))])
                    }
                }

            },
            {
                "duplicate function declaration in class",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {fn MyFn() {} fn MyFn() {}}
                                   """, [TypeCheckerError.ConflictingFunctionName(Identifier("MyFn"))])
                    }
                }

            },
            {
                "function contains duplicate parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn SomeFn(a: i64, a: string) {
                                       // verify first duplicate parameter is accepted
                                       var b: i64 = a;
                                       var c: string = a;
                                   }
                                   """, [
                                   TypeCheckerError.DuplicateFunctionParameter(Identifier("a"), Identifier("SomeFn")),
                                   MismatchedTypes(String, Int64)
                                   ])
                    }
                }

            },
            {
                "no return value provided when return value expected",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(): string { return; }
                                   """, [MismatchedTypes(String, Unit)])
                    }
                }

            },
            {
                "incorrect expression type in variable assignment",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2; var b: string = a
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "variable used before initialization",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64; var b = a
                                   """, [TypeCheckerError.AccessUninitializedVariable(Identifier("a"))])
                    }
                }

            },
            {
                "function used outside of declaration scope",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(){fn InnerFn() {}} InnerFn();
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("InnerFn"))])
                    }
                }

            },
            {
                "call missing method",
                new()
                {
                    {
                        "main.rf", ("""
                                   CallMissingMethod(b);
                                   """, [
                                   TypeCheckerError.SymbolNotFound(Identifier("CallMissingMethod")),
                                   TypeCheckerError.SymbolNotFound(Identifier("b"))
                                   ])
                    }
                }

            },
            {
                "object initializer for unknown type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = new MyClass::<i64> {someField = true};
                                   """, [TypeCheckerError.SymbolNotFound(Identifier("MyClass"))])
                    }
                }

            },
            {
                "object initializer for unknown type",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = new MyClass::<i64> {someField = b};
                                   """, [
                                   TypeCheckerError.SymbolNotFound(Identifier("MyClass")),
                                   TypeCheckerError.SymbolNotFound(Identifier("b"))
                                   ])
                    }
                }

            },
            {
                "incorrect expression types in method call",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(param1: string, param2: i64) {} MyFn(3, "value");
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType), MismatchedTypes(Int64, String)])
                    }
                }

            },
            {
                "missing function arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn(param1: string, param2: i64) {} MyFn();
                                   """, [TypeCheckerError.IncorrectNumberOfMethodArguments(MethodCall(VariableAccessor("MyFn")), 2)])
                    }
                }

            },
            {
                "too many function arguments",
                new()
                {
                    {
                            "main.rf", ("""fn MyFn(param1: string, param2: i64) {} MyFn("value", 3, 2);""", [
                                TypeCheckerError.IncorrectNumberOfMethodArguments(
                                MethodCall(VariableAccessor("MyFn"), Literal("value"), Literal(3), Literal(2)), 2)
                                ])
                    }
                }

            },
            {
                "member accessed on generic instance variable",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T1>(param: T1) {var a = param.something;}
                                   """, [TypeCheckerError.MemberAccessOnGenericExpression(MemberAccess(VariableAccessor("param"), "something"))])
                    }
                }

            },
            {
                "static member accessed on generic type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T1>() {var a = T1::something;}
                                   """, [
                                   TypeCheckerError.StaticMemberAccessOnGenericReference(StaticMemberAccess(NamedTypeIdentifier("T1"),
                                   "something"))
                                   ])
                    }
                }

            },
            {
                "generic variable returned as concrete class",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T1>(param: T1): i64 { return param; }
                                   """, [MismatchedTypes(Int64, GenericPlaceholder("T1"))])
                    }
                }

            },
            {
                "duplicate function generic parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T, T>() {}
                                   """, [TypeCheckerError.DuplicateTypeParameter(Identifier("T"))])
                    }
                }

            },
            {
                "duplicate function generic parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T, T, T1, T1>() {}
                                   """, [
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T")),
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T1"))
                                   ])
                    }
                }

            },
            {
                "duplicate function generic parameter",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn MyFn<T, T, T>() {}
                                   """, [
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T")),
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T"))
                                   ])
                    }
                }

            },
            {
                "incorrect type in if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   if (1) {}
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type in else if check",
                new()
                {
                    {
                        "main.rf", ("""
                                   if (true) {} else if (1) {}
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type in variable declaration in if body",
                new()
                {
                    {
                        "main.rf", ("""
                                   if (true) {var a: string = 1;}
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type in variable declaration in else if body",
                new()
                {
                    {
                        "main.rf", ("""
                                   if (true) {} else if (true) {var a: string = 1}
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type in variable declaration in else body",
                new()
                {
                    {
                        "main.rf", ("""
                                   if (true) {} else if (true) {} else {var a: string = 1}
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type in second else if body",
                new()
                {
                    {
                        "main.rf", ("""
                                   if (true) {} else if (true) {} else if (true) {var a: string = 1}
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "unresolved inferred types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: result::<>
                                   """, [
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   VariableDeclaration("a", type: NamedTypeIdentifier("result")), "TValue"),
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   VariableDeclaration("a", type: NamedTypeIdentifier("result")), "TError")
                                   ])
                    }
                }

            },
            {
                "unresolved inferred types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: result
                                   """, [
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   VariableDeclaration("a", type: NamedTypeIdentifier("result")), "TValue"),
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   VariableDeclaration("a", type: NamedTypeIdentifier("result")), "TError")
                                   ])
                    }
                }

            },
            {
                "unresolved inferred types",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = ok(1)
                                   """, [
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   VariableDeclaration("a",
                                   MethodCall(VariableAccessor("ok"), Literal(1))), "TError")
                                   ])
                    }
                }

            },
            {
                "incorrect number of type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: result::<string>
                                   """, [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 2)])
                    }
                }

            },
            {
                "incorrect number of type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   union MyUnion{A} var a = MyUnion::<string>::A
                                   """, [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 0)])
                    }
                }

            },
            {
                "incorrect number of type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{} var a = new MyClass::<string>{}
                                   """, [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 0)])
                    }
                }

            },
            {
                "incorrect number of type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T, T2>{} var a = new MyClass::<string>{}
                                   """, [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 2)])
                    }
                }

            },
            {
                "too many type arguments",
                new()
                {
                    {
                        "main.rf", ("""var a: result::<string, string, string>""", [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 3, 2)])
                    }
                }

            },
            {
                "unresolved global function generic type",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn Fn1<T1>(){} Fn1::<>();
                                   """, [TypeCheckerError.UnresolvedInferredGenericType(MethodCall(VariableAccessor("Fn1")), "T1")])
                    }
                }

            },
            {
                "unresolved instance function generic type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass
                                   {
                                       pub fn MyFn<T>(){}
                                   }
                                   var a = new MyClass{};
                                   a.MyFn();
                                   """, [
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   MethodCall(MemberAccess(VariableAccessor("a"), "MyFn")), "T")
                                   ])
                    }
                }

            },
            {
                "unresolved static function generic type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass
                                   {
                                       pub static fn MyFn<T>(){}
                                   }
                                   MyClass::MyFn();
                                   """, [
                                   TypeCheckerError.UnresolvedInferredGenericType(
                                   MethodCall(StaticMemberAccess(NamedTypeIdentifier("MyClass"), "MyFn")), "T")
                                   ])
                    }
                }

            },
            {
                "too many function type arguments",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn Fn1<T1>(){} Fn1::<string, bool>();
                                   """, [
                                   TypeCheckerError.IncorrectNumberOfTypeArguments(
                                   SourceRange.Default,
                                   2,
                                   1)
                                   ])
                    }
                }

            },
            {
                "unresolved function type argument",
                new()
                {
                    {
                        "main.rf", ("""
                                   fn Fn1<T1>(){} Fn1();
                                   """, [TypeCheckerError.UnresolvedInferredGenericType(MethodCall(VariableAccessor("Fn1")), "T1")])
                    }
                }

            },
            {
                "incorrect type for class initializer field assignment",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub field someField: string,
                                   }
                                   var a = new MyClass { someField = 1 };
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "field assigned twice in object initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub field someField: string,
                                   }
                                   var a = new MyClass { someField = "value", someField = "value" };
                                   """, [TypeCheckerError.ClassFieldSetMultipleTypesInInitializer(Identifier("someField"))])
                    }
                }

            },
            {
                "unknown field assigned in object initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub field someField: string,
                                   }
                                   var a = new MyClass { someField = "value", extraField = 1 };
                                   """, [TypeCheckerError.UnknownField(Identifier("extraField"), "class MyClass")])
                    }
                }

            },
            {
                "field not assigned in object initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       pub field someField: string,
                                   }
                                   var a = new MyClass {};
                                   """, [
                                   TypeCheckerError.FieldsLeftUnassignedInClassInitializer(new ObjectInitializerExpression(
                                   new ObjectInitializer(NamedTypeIdentifier("MyClass"), []), SourceRange.Default),
                                   ["someField"])
                                   ])
                    }
                }

            },
            {
                "incorrect expression type in static field initializer",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { static field someField: string = 1, }
                                   """, [MismatchedTypes(String, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "function generic type conflict with parent class",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T> {
                                       fn MyFn<T>(){}
                                   }
                                   """, [TypeCheckerError.ConflictingTypeParameter(Identifier("T"))])
                    }
                }

            },
            {
                "duplicate generic type in class definition",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T, T>{}
                                   """, [TypeCheckerError.DuplicateTypeParameter(Identifier("T"))])
                    }
                }

            },
            {
                "duplicate generic type in class definition",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T, T, T1, T1>{}
                                   """, [
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T")),
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T1"))
                                   ])
                    }
                }

            },
            {
                "duplicate generic type in class definition",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass<T, T, T>{}
                                   """, [
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T")),
                                   TypeCheckerError.DuplicateTypeParameter(Identifier("T"))
                                   ])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   class OtherClass<MyClass>{}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   fn MyFn<MyClass>(){}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   class SomeClass {fn MyFn<MyClass>(){}}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   union SomeUnion {fn MyFn<MyClass>(){}}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   fn SomeFn() {fn MyFn<MyClass>(){}}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass{}
                                   union MyUnion<MyClass>{}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   union OtherUnion{}
                                   union MyUnion<OtherUnion>{}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("OtherUnion"))])
                    }
                }

            },
            {
                "Generic type conflicts with existing type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class OtherClass<MyClass>{}
                                   class MyClass{}
                                   """, [TypeCheckerError.TypeParameterConflictsWithType(Identifier("MyClass"))])
                    }
                }

            },
            {
                "incorrect return type",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {
                                       fn MyFn(): i64 { return ""; }
                                   }
                                   """, [MismatchedTypes(Int64, String)])
                    }
                }

            },
            // binary operators
            // less than
            {
                "incorrect type for less than",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 1 < true;
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for less than",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true < 1;
                                   """, [MismatchedTypes(IntTypes, Boolean)])
                    }
                }

            },
            {
                "incorrect type for less than variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64 = 1 < 2
                                   """, [MismatchedTypes(Int64, Boolean)])
                    }
                }

            },
            {
                // GreaterThan,
                "incorrect type for greater than",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true > 1;
                                   """, [MismatchedTypes(IntTypes, Boolean)])
                    }
                }

            },
            {
                "incorrect type for greater than",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 > true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for greater than in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64 = 2 > 2
                                   """, [MismatchedTypes(Int64, Boolean)])
                    }
                }

            },
            {
                // Plus,
                "incorrect type for plus",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true + 1;
                                   """, [MismatchedTypes(IntTypes, Boolean)])
                    }
                }

            },
            {
                "incorrect type for plus",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 + true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for plus in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: bool = 2 + 2
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                // Minus,
                "incorrect type for minus",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true - 1;
                                   """, [MismatchedTypes(IntTypes, Boolean)])
                    }
                }

            },
            {
                "incorrect type for minus",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 - true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for minus in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: bool = 2 - 2
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                // Multiply,
                "incorrect type for multiply",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true * 1;
                                   """, [MismatchedTypes(IntTypes, Boolean)])
                    }
                }

            },
            {
                "incorrect type for multiply",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 * true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for multiply in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: bool = 2 * 2
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                // Divide,
                "incorrect type for divide",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true / 1;
                                   """, [MismatchedTypes(IntTypes, Boolean)])
                    }
                }

            },
            {
                "incorrect type for divide",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 / true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for divide in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: bool = 2 / 2
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                // Equality Check
                "incorrect type for equality check",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true == 1;
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type for negative equality check",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = true != 1;
                                   """, [MismatchedTypes(Boolean, UnspecifiedSizedIntType)])
                    }
                }

            },
            {
                "incorrect type for equality check",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 == true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for negative equality check",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a = 2 != true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "incorrect type for equality check in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64 = 2 == 2
                                   """, [MismatchedTypes(Int64, Boolean)])
                    }
                }

            },
            {
                "incorrect type for negative equality check in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64 = 2 != 2
                                   """, [MismatchedTypes(Int64, Boolean)])
                    }
                }

            },
            {
                // ValueAssignment,
                "incompatible type used for variable assignment",
                new()
                {
                    {
                        "main.rf", ("""
                                   var mut a = 2; a = true
                                   """, [MismatchedTypes(UnspecifiedSizedIntType, Boolean)])
                    }
                }

            },
            {
                "assignment to literal",
                new()
                {
                    {
                        "main.rf", ("""
                                   true = false
                                   """, [
                                   TypeCheckerError.ExpressionNotAssignable(Literal(true))
                                   ])
                    }
                }

            },
            {
                // MemberAccess,
                "incorrect type in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { pub static field someField: i64 = 3, }
                                   var a: string = MyClass::someField;
                                   """, [MismatchedTypes(String, Int64)])
                    }
                }

            },
            {
                // StaticMemberAccess
                "incorrect type in variable declaration",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { pub field someField: i64, }
                                   var a: MyClass = new MyClass { someField = 3 };
                                   var b: string = a.someField;
                                   """, [MismatchedTypes(String, Int64)])
                    }
                }

            },
            {
                "no fields provided in class pattern",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass { pub field someField: i64, }
                                   var a: MyClass = new MyClass { someField = 3 };
                                   var b = a matches MyClass {};
                                   """, [TypeCheckerError.MissingFieldsInClassPattern(["someField"], NamedTypeIdentifier("MyClass"))])
                    }
                }

            },
            {
                "calling closure when variable is uninitialized",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64;
                                   fn SomeFn() {
                                       var b = a;
                                   }
                                   SomeFn();
                                   a = 1;
                                   """, [
                                   TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(Identifier("SomeFn"),
                                   [Identifier("a")])
                                   ])
                    }
                }

            },
            {
                "calling deep closure when variable is uninitialized",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64;
                                   fn SomeFn()
                                   {
                                       fn InnerFn()
                                       {
                                           var b = a;
                                       }
                                       InnerFn();
                                   }
                                   SomeFn();
                                   a = 1;
                                   """, [
                                   TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(Identifier("SomeFn"),
                                   [Identifier("a")])
                                   ])
                    }
                }

            },
            {
                "assigning closure to variable when variable is uninitialized",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i64;
                                   fn SomeFn() {
                                       var b = a;
                                   }
                                   var c = SomeFn;
                                   a = 1;
                                   """, [
                                   TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(Identifier("SomeFn"),
                                   [Identifier("a")])
                                   ])
                    }
                }

            },
            {
                "Mismatched type boxing",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: boxed i32 = todo!;
                                   var b: unboxed i32 = a;
                                   """, [TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, Int32, false, Int32, true)])
                    }
                }

            },
            {
                "Mismatched type boxing",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: unboxed i32 = todo!;
                                   var b: boxed i32 = a;
                                   """, [TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, Int32, true, Int32, false)])
                    }
                }

            },
            {
                "Mismatched type boxing",
                new()
                {
                    {
                        "main.rf", ("""
                                   var a: i32 = todo!;
                                   var b: boxed i32 = a;
                                   """, [TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, Int32, true, Int32, false)])
                    }
                }

            },
            {
                "Mismatched type boxing",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {}
                                   var a: unboxed MyClass = todo!;
                                   var b: boxed MyClass = a;
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), true,
                                   new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), false)
                                   ])
                    }
                }

            },
            {
                "Mismatched type boxing",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {}
                                   var a: MyClass = todo!;
                                   var b: unboxed MyClass = a;
                                   """, [
                                   TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), false,
                                   new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), true)
                                   ])
                    }
                }

            },
            {
                "Mismatched type boxing",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {}
                                   var a: unboxed MyClass = todo!;
                                   var b: MyClass = a;
                                   """, [
                                       TypeCheckerError.MismatchedTypeBoxing(SourceRange.Default, new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), true,
                                   new TestClassReference(new DefId(ModuleId, $"{ModuleId}:::MyClass"), []), false)
                                   ])
                    }
                }

            },
            {
                "TypeIdentifier without static access",
                new()
                {
                    {
                        "main.rf", ("""
                                   class MyClass {}
                                   unboxed MyClass
                                   """, [
                                   TypeCheckerError.TypeIsNotExpression(SourceRange.Default,
                                   NamedTypeIdentifier("MyClass", boxedSpecifier: Token.Unboxed(SourceSpan.Default)))
                                   ])
                    }
                }

            }
        };
    }

    private const string Mvp =
        """
        pub fn DoSomething(inner_a: i64): result::<i64, string> {
            var mut inner_b: i64 = 2;

            if (inner_a > inner_b) {
                return ok(inner_a);
            }
            else if (inner_a == inner_b) {
                return result::<i64, string>::Ok(inner_b);
            }

            inner_b = 3;

            var thing = new Class2 {
                A = "thing"
            };

            MyClass::StaticMethod();

            PrivateFn::<string>();

            if (false) {
                // lowercase error keyword
                return error("something wrong");
            }

            // Capital Error for fully resolved variant
            return result::<i64, string>::Error("something wrong");
        }

        fn PrivateFn<T>() {
        }

        pub fn SomethingElse(inner_a: i64): result::<i64, string> {
            var inner_b = DoSomething(inner_a)?;
            var mut inner_d = 2;

            return result::<i64, string>::Ok(inner_b);
        }

        pub class MyClass {
            pub fn PublicMethod() {
            }

            pub static fn StaticMethod() {

            }

            field FieldA: string,
            mut field FieldB: string,
            pub mut field FieldC: string,
            pub field FieldD: string,
            pub static field FieldE: string = "something",
            pub static mut field FieldF: string = "something",
        }

        pub class GenericClass<T> {
            pub fn PublicMethod<T1>() {
            }
        }

        // a class
        pub class Class2 {
            pub field A: string,
        }

        /*
            A union
        */
        pub union MyUnion {
            A,
            B { field MyField: string, },
            C(string),

            fn SomeMethod() {
                var foo = match (this) {
                    MyUnion::A => "",
                    MyUnion::B { MyField } => MyField,
                    MyUnion::C(var value) => value
                };

                var bar = match (this) {
                    MyUnion::A => 1,
                    MyUnion::B => 2,
                    MyUnion::C => 3
                }
            }
        }

        fn AnotherMethod(param: MyUnion) {
            if (param matches MyUnion::A) {
            }
            else if (param matches MyUnion::B { MyField }) {
            }

            var inner_a = match (param) {
                MyUnion::A => 1,
                MyUnion::B { MyField } => 2,
                MyUnion::C(var value) => 3
            };
        }

        var a = MyUnion::A;

        var c = new MyUnion::B{ MyField = ""};
        """;

    private static TypeCheckerError MismatchedTypes(ITypeReference expected, ITypeReference actual)
    {
        return TypeCheckerError.MismatchedTypes(SourceRange.Default, expected, actual);
    }

    private static TypeCheckerError MismatchedTypes(IReadOnlyList<ITypeReference> expected, ITypeReference actual)
    {
        return TypeCheckerError.MismatchedTypes(SourceRange.Default, expected, actual);
    }

    private static readonly ITypeReference Int64 = new TestClassReference(DefId.Int64, [], false);
    private static readonly ITypeReference Int32 = new TestClassReference(DefId.Int32, [], false);
    private static readonly ITypeReference Int32_Boxed = new TestClassReference(DefId.Int32, [], true);
    private static readonly ITypeReference Int16 = new TestClassReference(DefId.Int16, [], false);
    private static readonly ITypeReference Int8 = new TestClassReference(DefId.Int8, [], false);
    private static readonly ITypeReference UInt64 = new TestClassReference(DefId.UInt64, [], false);
    private static readonly ITypeReference UInt32 = new TestClassReference(DefId.UInt32, [], false);
    private static readonly ITypeReference UInt16 = new TestClassReference(DefId.UInt16, [], false);
    private static readonly ITypeReference UInt8 = new TestClassReference(DefId.UInt8, [], false);
    private static readonly ITypeReference String = new TestClassReference(DefId.String, [], false);
    private static readonly ITypeReference Boolean = new TestClassReference(DefId.Boolean, [], false);
    private static readonly ITypeReference Unit = new TestClassReference(DefId.Unit, [], false);
    private static readonly ITypeReference UnspecifiedSizedIntType = new UnspecifiedSizedIntType() { Boxed = false };

    private static readonly IReadOnlyList<ITypeReference> IntTypes =
    [
        Int64,
        Int32,
        Int16,
        Int8,
        UInt64,
        UInt32,
        UInt16,
        UInt8
    ];


    private static ITypeReference TupleType(bool? boxed, params IReadOnlyList<ITypeReference> members)
    {
        return new TestClassReference(DefId.Tuple(members.Count), [.. members.Select((x, i) => ($"T{i}", x))], boxed ?? false);
    }

    private static ITypeReference Result(
        ITypeReference? value,
        ITypeReference? error,
        bool? boxed = null)
    {
        return new TestUnionReference(DefId.Result, [("TValue", value), ("TError", error)], boxed ?? true);
    }

    private sealed record TestUnionReference(DefId Id, IReadOnlyList<(string name, ITypeReference? argument)> TypeArguments, bool Boxed = true)
        : ITypeReference, IEquatable<ITypeReference>
    {
        public bool Equals(ITypeReference? other)
        {
            return other is not null && AreTypeReferencesEqual(this, other);
        }

        public override string ToString()
        {
            if (TypeArguments.Count == 0)
            {
                return Id.FullName;
            }

            var sb = new StringBuilder($"{Id.FullName}::<");
            sb.AppendJoin(",", TypeArguments.Select(x => $"{x.name}=[{x.argument?.ToString() ?? "??"}]"));
            sb.Append('>');
            return sb.ToString();
        }
    }

    public static bool AreTypeReferencesEqual(ITypeReference? left, ITypeReference? right)
    {
        switch (left, right)
        {
            case (InstantiatedClass leftClass, TestClassReference rightClass):
                {
                    return leftClass.Signature.Id == rightClass.Id
                        && leftClass.TypeArguments.Count == rightClass.TypeArguments.Count
                        && leftClass.TypeArguments.Zip(rightClass.TypeArguments).All(x => AreTypeReferencesEqual(x.First.ResolvedType.NotNull(), x.Second.argument));
                }
            case (TestClassReference leftClass, InstantiatedClass rightClass):
                {
                    return leftClass.Id == rightClass.Signature.Id
                        && leftClass.TypeArguments.Count == rightClass.TypeArguments.Count
                        && leftClass.TypeArguments.Zip(rightClass.TypeArguments).All(x => AreTypeReferencesEqual(x.First.argument, x.Second.ResolvedType.NotNull()));
                }
            case (TestClassReference leftClass, TestClassReference rightClass):
                {
                    return leftClass.Id == rightClass.Id
                        && leftClass.TypeArguments.Count == rightClass.TypeArguments.Count
                        && leftClass.TypeArguments.Zip(rightClass.TypeArguments).All(x => AreTypeReferencesEqual(x.First.argument, x.Second.argument));
                }
            case (InstantiatedUnion leftUnion, TestUnionReference rightUnion):
                {
                    return leftUnion.Signature.Id == rightUnion.Id
                        && leftUnion.TypeArguments.Count == rightUnion.TypeArguments.Count
                        && leftUnion.TypeArguments.Zip(rightUnion.TypeArguments).All(x => AreTypeReferencesEqual(x.First.ResolvedType.NotNull(), x.Second.argument));
                }
            case (TestUnionReference leftUnion, InstantiatedUnion rightUnion):
                {
                    return leftUnion.Id == rightUnion.Signature.Id
                        && leftUnion.TypeArguments.Count == rightUnion.TypeArguments.Count
                        && leftUnion.TypeArguments.Zip(rightUnion.TypeArguments).All(x => AreTypeReferencesEqual(x.First.argument, x.Second.ResolvedType.NotNull()));
                }
            case (TestUnionReference leftUnion, TestUnionReference rightUnion):
                {
                    return leftUnion.Id == rightUnion.Id
                        && leftUnion.TypeArguments.Count == rightUnion.TypeArguments.Count
                        && leftUnion.TypeArguments.Zip(rightUnion.TypeArguments).All(x => AreTypeReferencesEqual(x.First.argument, x.Second.argument));
                }
            case (GenericPlaceholder leftPlaceholder, GenericPlaceholder rightPlaceholder):
                {
                    return leftPlaceholder.OwnerType.Id == rightPlaceholder.OwnerType.Id
                        && leftPlaceholder.GenericName == rightPlaceholder.GenericName;
                }
            case (GenericTypeReference leftReference, _):
                {
                    return AreTypeReferencesEqual(leftReference.ResolvedType.NotNull(), right);
                }
            case (_, GenericTypeReference rightReference):
                {
                    return AreTypeReferencesEqual(left, rightReference.ResolvedType.NotNull());
                }
            case (InstantiatedClass, InstantiatedUnion):
            case (InstantiatedClass, TestUnionReference):
            case (InstantiatedClass, TypeChecking.TypeChecker.GenericPlaceholder):
            case (InstantiatedUnion, InstantiatedClass):
            case (InstantiatedUnion, TestClassReference):
            case (InstantiatedUnion, TypeChecking.TypeChecker.GenericPlaceholder):
            case (null, not null):
            case (not null, null):
                return false;
            default:
                throw new InvalidOperationException($"{left?.GetType()} --- {right?.GetType()}");
        }
    }

    private sealed record TestClassReference(DefId Id, IReadOnlyList<(string name, ITypeReference argument)> TypeArguments, bool Boxed = true)
        : ITypeReference, IEquatable<ITypeReference>
    {
        public bool Equals(ITypeReference? other)
        {
            return other is not null && AreTypeReferencesEqual(this, other);
        }

        public override string ToString()
        {
            if (TypeArguments.Count == 0)
            {
                return Id.FullName;
            }

            var sb = new StringBuilder($"{Id.FullName}::<");
            sb.AppendJoin(",", TypeArguments.Select(x => $"{x.name}=[{x.argument}]"));
            sb.Append('>');
            return sb.ToString();
        }
    }

    private static GenericPlaceholder GenericPlaceholder(string name)
    {
        return new GenericPlaceholder
        {
            GenericName = name,
            OwnerType = null!,
            Constraints = []
        };
    }

    private static GenericTypeReference GenericTypeReference(string name)
    {
        return new GenericTypeReference
        {
            GenericName = name,
            OwnerType = null!,
            InstantiatedFrom = null!
        };
    }
}
