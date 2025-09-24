using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class SimpleExpressions
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
                "push int",
                "1",
                Module()
            },
            {
                "push constant string",
                "\"someString\"",
                Module()
            },
            {
                "push constant bool true",
                "true",
                Module()
            },
            {
                "push constant bool false",
                "false",
                Module()
            },
            {
                "variable declaration without initializer",
                "var a: int",
                Module()
            },
            {
                "two variable declarations without initializers",
                "var a: int;var b: string",
                Module()
            },
            {
                "variable declaration with value initializer",
                "var a = 1",
                Module()
            },
            {
                "two variable declarations with value initializers",
                "var a = 1;var b = \"hello\"",
                Module()
            },
            {
                "less than",
                "var a = 1 < 2",
                Module()
            },
            {
                "greater than",
                "var a = 1 > 2",
                Module()
            },
            {
                "access local variable",
                """
                var a = 1;
                var b = a;
                var c = b;
                """,
                Module()
            },
            {
                "plus",
                "var a = 1 + 2",
                Module()
            },
            {
                "minus",
                "var a = 1 - 2",
                Module()
            },
            {
                "multiply",
                "var a = 1 * 2",
                Module()
            },
            {
                "divide",
                "var a = 1 / 2",
                Module()
            },
            {
                "equals",
                "var a = 1 == 2",
                Module()
            },
            {
                "local assignment",
                """
                var a;
                a = 1;
                """,
                Module()
            },
            {
                "field assignment",
                """
                class MyClass{pub mut field MyField: int}
                var mut a = new MyClass{MyField = 1};
                a.MyField = 2;
                """,
                Module()
            },
            {
                "static field assignment",
                """
                class MyClass{pub static mut field MyField: int = 1}
                MyClass::MyField = 2;
                """,
                Module()
            },
            {
                "single element tuple",
                "var a = (1);",
                Module()
            },
            {
                "tuple with multiple elements",
                "var a = (1, true)",
                Module()
            },
            {
                "bool not",
                "var a = !true;",
                Module()
            },
            {
                "and",
                "var a = true && true",
                Module()
            },
            {
                "or",
                "var a = true || true",
                Module()
            }
        };
    }
}