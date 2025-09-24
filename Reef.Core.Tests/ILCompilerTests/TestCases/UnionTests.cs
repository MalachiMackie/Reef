using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;
using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class UnionTests
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
                "Create union unit variant",
                """
                union MyUnion {
                    A
                }
                var a = MyUnion::A
                """,
                Module()
            },
            {
                "Create union second unit variant",
                """
                union MyUnion {
                    A,
                    B
                }
                var a = MyUnion::B
                """,
                Module()
            },
            {
                "create union tuple variant",
                """
                union MyUnion {
                    A,
                    B(string),
                    C(int, string),
                    
                    pub static fn SomeFn(){}
                }
                
                var a = MyUnion::C(1, "");
                """,
                Module()
            },
            {
                "create union tuple variant",
                """
                union MyUnion {
                    A,
                    B(string),
                }
                
                var a = MyUnion::B;
                var b = a("");
                """,
                Module()
            },
            {
                "union class initializer",
                """
                union MyUnion {
                    A,
                    B { field Field1: int, field Field2: string }
                }
                var a = new MyUnion::B {
                    Field1 = 1,
                    Field2 = ""
                };
                """,
                Module()
            }
        };
    }
}