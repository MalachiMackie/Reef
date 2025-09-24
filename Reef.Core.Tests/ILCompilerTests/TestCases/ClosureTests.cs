using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ClosureTests
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
        return new TheoryData<string, string, ReefModule>
        {
            {
                "fn closure",
                """
                static fn SomeFn(param: int, param2: string) {
                    fn InnerFn(): int {
                        var a = param2;
                        return param;
                    }
                }
                """,
                Module()
            },
            {
                "access parameter in closure",
                """
                var a: int;
                fn SomeMethod(param: string) {
                    var b = param;
                    var c = a;
                }
                """,
                Module()
            },
            {
                "access outer parameter in closure",
                """
                static fn OuterFn(outerParam: string) {
                    fn SomeMethod() {
                        var a = outerParam;
                    }
                }
                """,
                Module()
            },
            {
                "call closure",
                """
                var a = "";
                SomeFn();

                fn SomeFn() {
                    var b = a;
                }
                """,
                Module()
            },
            {
                "call closure that references parameter",
                """
                static fn Outer(a: string) {
                    fn SomeFn() {
                        var b = a;
                    }
                    SomeFn();
                }
                """,
                Module()
            },
            {
                "call closure with parameter",
                """
                var a = "";
                SomeFn(1);

                fn SomeFn(c: int) {
                    var b = a;
                }
                """,
                Module()
            },
            {
                "closure references two functions out",
                """
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
                """,
            Module()
            },
            {
                "accessing variable in and out of closure",
                """
                var a = 1;
                var c = a;
                InnerFn();
                fn InnerFn() {
                    var b = a;
                }
                """,
                Module()
            },
            {
                "multiple closures",
                """
                var a = 1;
                var b = 2;
                var c = 3;
                
                InnerFn1();
                InnerFn1();
                InnerFn2();
                
                fn InnerFn1() {
                    var d = a;
                }
                fn InnerFn2() {
                    var e = b;
                }
                """,
                Module()
            },
            {
                "parameter referenced in closure",
                """
                static fn SomeFn(a: int) { 
                    fn InnerFn() {
                        var b = a;
                    }
                    var c = 2;
                    InnerFn();
                    var d = a;
                }
                """,
                Module()
            },
            {
                "closure references variables from multiple functions",
                """
                static fn Outer(a: int) {
                    var d = a;
                    Inner1();
                    fn Inner1() {
                        var b = 2;
                        Inner2();
                        
                        fn Inner2() {
                            var aa = a;
                            var bb = b;
                        }
                    }
                }
                """,
                Module()
            },
            {
                "mutating referenced value",
                """
                var mut a = 1;
                fn InnerFn() {
                    var b = a;
                    a = 2;
                }
                """,
                Module()
            },
            {
                "assign closure to variable",
                """
                var a = 1;
                fn Inner() {
                    var b = a;
                }
                var c = Inner;
                c();
                """,
                Module()
            },
            {
                "call function variable from closure",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    a();
                }
                OtherFn();
                """,
                Module()
            },
            {
                "reference function variable from closure",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    var b = a;
                }
                OtherFn();
                """,
                Module()
            },
            {
                "reference function variable from closure and call from locals",
                """
                fn MyFn() {}
                var a = MyFn;
                fn OtherFn() {
                    var b = a;
                }
                OtherFn();
                a();
                """,
                Module()
            }
        };
    }
}