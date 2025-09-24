using FluentAssertions;
using FluentAssertions.Equivalency;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;
using Reef.IL;

using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public class ControlFlow
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
                "Fallout operator with result",
                """
                static fn SomeFn(): result::<int, string> {
                    var a = ok(1)?;
                    return ok(1);
                }
                """,
                Module()
            },
            {
                "empty if is last instruction",
                "if (true) {}",
                Module()
            },
            {
                "populated if is last instruction",
                "if (true) {var a = 1}",
                Module()
            },
            {
                "simple if",
                """
                var a;
                if (true) {
                    a = 1;
                }
                a = 2;
                """,
                Module()
            },
            {
                "if with else",
                """
                var a;
                if (true) {
                    a = 1;
                } else {
                    a = 2;
                }
                """,
                Module()
            },
            {
                "if else if chain",
                """
                var a;
                if (true) {
                    a = 1;
                } else if (true) {
                    a = 2;
                } else if (true) {
                    a = 3;
                }
                """,
                Module()
            },
            {
                "if else if chain with else",
                """
                var a;
                if (true) {
                    a = 1;
                } else if (true) {
                    a = 2;
                } else if (true) {
                    a = 3;
                } else {
                    a = 4;
                }
                """,
                Module()
            },
            {
                "discard pattern matches",
                """
                if (1 matches _){}
                """,
                Module()
            },
            {
                "variable declaration pattern matches",
                """
                if (1 matches var a) {}
                """,
                Module()
            },
            {
                "class pattern matches",
                """
                class MyClass
                {
                    pub field MyField: string
                }
                var a = new MyClass { MyField = "" };
                if (a matches MyClass { MyField }) {}
                """,
                Module()
            },
            {
                "class pattern matches with field pattern",
                """
                class MyClass
                {
                    pub field MyField: string
                }
                var a = new MyClass { MyField = "" };
                if (a matches MyClass { MyField: var b }) {}
                """,
                Module()
            },
            {
                "class pattern matches with field discards",
                """
                class MyClass
                {
                    pub field MyField: string
                }
                var a = new MyClass { MyField = "" };
                if (a matches MyClass { _ }) {}
                """,
                Module()
            },
            {
                "type pattern matches without variable",
                """
                if (1 matches int) {}
                """,
                Module()
            },
            {
                "type pattern matches with variable",
                """
                if (1 matches int var b) {} 
                """,
                Module()
            },
            {
                "union variant pattern without variable",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (a matches MyUnion::A){}
                """,
                Module()
            },
            {
                "union variant pattern with variable",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (a matches MyUnion::A var b) {}
                """,
                Module()
            },
            {
                "union tuple variant pattern",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("");
                if (a matches MyUnion::A(var b)) {}
                """,
                Module()
            },
            {
                "union tuple variant pattern with variable",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("");
                if (a matches MyUnion::A(var b) var c) {}
                """,
                Module()
            },
            {
                "union class variant pattern",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (a matches MyUnion::A { MyField }){}
                """,
                Module()
            },
            {
                "union class variant pattern",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (a matches MyUnion::A { MyField } var b){}
                """,
                Module()
            },
            {
                "union class variant pattern with field pattern",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (a matches MyUnion::A { MyField: var b }){}
                """,
                Module()
            },
            {
                "union class variant pattern with field discards",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (a matches MyUnion::A { _ }){}
                """,
                Module()
            },
            {
                "mutate pattern variable",
                """
                var a = 1;
                if (a matches var mut b) {
                    b = 2;
                }
                """,
                Module()
            },
            {
                "mutate nested pattern variable",
                """
                union MyUnion {
                    A(string)
                }
                var mut a = MyUnion::A(""); 
                
                if (a matches MyUnion::A(var mut str)) {
                    str = "hi";
                }
                """,
                Module()
            },
            {
                "mutate deeply nested pattern variable",
                """
                union UnionA {
                    A(string)
                }
                
                union UnionB {
                    A,
                    B(UnionA)
                }
                
                var mut a = UnionB::B(UnionA::A(""));
                
                if (a matches UnionB::B(UnionA::A(var mut str) var mut b)) {
                    str = "hi";
                    b = UnionA::A("hi");
                }
                """,
                Module()
            }
        };
    }
}
