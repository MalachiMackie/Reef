using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;
using static TestHelpers;

public class ClassTests
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
                "access this in instance method",
                """
                class MyClass {
                    fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                Module()
            },
            {
                "access instance field via variable in instance method",
                """
                class MyClass {
                    field SomeField: string,

                    fn SomeFn() {
                        var a = SomeField;
                    }
                }
                """,
                Module()
            },
            {
                "access static field via variable in method",
                """
                class MyClass {
                    static field SomeField: string = "",

                    static fn SomeFn() {
                        var a = SomeField;
                    }
                }
                """,
                Module()
            },
            {
                "access parameter in instance method",
                """
                class MyClass {
                    fn SomeFn(param: int) {
                        var a = param;
                    }
                }
                """,
                Module()
            },
            {
                "create object without fields",
                """
                class MyClass{}
                var a = new MyClass{};
                """,
                Module()
            },
            {
                "create object with fields",
                """
                class MyClass{ pub field Field1: int, pub field Field2: string}
                var a = new MyClass{Field2 = "", Field1 = 2};
                """,
                Module()
            },
            {
                "call instance method",
                """
                class MyClass {
                    pub fn SomeFn(){}
                }
                var a = new MyClass {};
                a.SomeFn(); 
                """,
                Module()
            },
            {
                "call instance method with parameters",
                """
                class MyClass {
                    pub fn SomeFn(a: int, b: string){}
                }
                var a = new MyClass {};
                a.SomeFn(1, ""); 
                """,
                Module()
            },
            {
                "call static class method",
                """
                class MyClass {
                    pub static fn MyFn(a: int) {
                    }
                }
                MyClass::MyFn(1);
                """,
                Module()
            },
            {
                "get static field",
                """
                class MyClass { pub static field A: int = 1 }
                var a = MyClass::A;
                """,
                Module()
            },
            {
                "get instance field",
                """
                class MyClass { pub field MyField: int }
                var a = new MyClass { MyField = 1 };
                var b = a.MyField;
                """,
                Module()
            },
            {
                "get instance and static field",
                """
                class MyClass { pub field Ignore: int, pub field InstanceField: string, pub static field StaticField: int = 2 }
                var a = new MyClass { Ignore = 1, InstanceField = "" };
                var b = a.InstanceField;
                var c = MyClass::StaticField;
                """,
                Module()
            }
        };
    }
}