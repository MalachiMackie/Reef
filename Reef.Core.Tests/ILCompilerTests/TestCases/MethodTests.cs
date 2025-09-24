using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class MethodTests
{
    
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var loweredProgram = ProgramAbseil.Lower(program.ParsedProgram); 

        var module = ILCompile.CompileToIL(loweredProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            ConfigureEquivalencyCheck,
            description);
    }
    
    private static EquivalencyOptions<T> ConfigureEquivalencyCheck<T>(EquivalencyOptions<T> options)
    {
        return options
            .Excluding(memberInfo => memberInfo.Type == typeof(Guid));
    }
    
    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new()
        {
            {
                "access parameter",
                """
                static fn SomeFn(a: int, b: int) {
                    var foo = a;
                    var bar = b;
                }
                """,
                Module()
            },
            {
                "call global method",
                """
                static fn FirstFn(){}
                FirstFn();
                """,
                Module()
            },
            {
                "call instance and static methods",
                """
                class MyClass {
                    static fn Ignore() {}
                    pub static fn StaticFn() {}
                    pub fn InstanceFn() {}
                }
                new MyClass{}.InstanceFn();
                MyClass::StaticFn();
                """,
                Module()
            },
            {
                "functions in inner block",
                """
                static fn SomeFn() {
                    { 
                        fn InnerFn() {
                        }
                        
                        InnerFn();
                    }
                }
                """,
                Module()
            },
            {
                "assign global function to variable",
                """
                fn SomeFn(param: int) {
                }
                var a = SomeFn;
                a(1);
                """,
                Module()
            },
            {
                "assign instance function to variable",
                """
                class MyClass {
                    pub fn MyFn() {
                    }
                }
                var a = new MyClass{};
                var b = a.MyFn;
                b();
                """,
                Module()
            },
            {
                "assign static type function to variable",
                """
                class MyClass { 
                    pub static fn MyFn() {}
                }
                var a = MyClass::MyFn;
                a();
                """,
                Module()
            },
            {
                "assign static type function with parameters and return type to variable",
                """
                class MyClass { 
                    pub static fn MyFn(a: string, b: int): bool { return true; }
                }
                var a = MyClass::MyFn;
                a("", 1);
                """,
                Module()
            },
            {
                "assign instance function to variable from within method",
                """
                class MyClass {
                    fn MyFn() {
                        var a = MyFn;
                    }
                }
                """,
                Module()
            },
            {
                "call instance function from within instance function",
                """
                class MyClass {
                    fn MyFn() {
                        MyFn();
                    }
                }
                """,
                Module()
            },
            {
                "assign instance function to variable from within same instance but different method",
                """
                class MyClass {
                    fn MyFn() {}
                    fn OtherFn() {
                        var a = MyFn;
                    }
                }
                """,
                Module()
            },
            {
                "call generic method",
                """
                fn MyFn<T>() {}
                MyFn::<string>();
                """,
                Module()
            },
            {
                "assign generic method to variable",
                """
                fn MyFn<T>(){}
                var a = MyFn::<string>;
                """,
                Module()
            },
            {
                "call static type generic function",
                """
                class MyClass {
                    pub static fn MyFn<T>(){}
                }
                MyClass::MyFn::<string>();
                """,
                Module()
            },
            {
                "call instance type generic function",
                """
                class MyClass {
                    pub fn MyFn<T>(){}
                }
                var a = new MyClass{};
                a.MyFn::<string>();
                """,
                Module()
            },
            {
                "call generic method within type",
                """
                class MyClass {
                    fn MyFn<T>() {}
                    fn OtherFn() {
                        MyFn::<string>();
                    }
                }
                """,
                Module()
            }
        };
    }
}