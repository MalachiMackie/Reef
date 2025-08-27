using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class FunctionObjectTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void FunctionObjectAbseilTest(string description, string source, LoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(source);
        var loweredProgram = ProgramAbseil.Lower(program);
        loweredProgram.Should().BeEquivalentTo(expectedProgram, IgnoringGuids);
    }

    public static TheoryData<string, string, LoweredProgram> TestCases()
    {
        return new()
        {
            {
                "assign static function to function object",
                """
                fn SomeFn(){}
                var a = SomeFn;
                """,
                LoweredProgram()
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
                LoweredProgram()
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
                        var a = InnerFn;
                    }
                }
                """,
                LoweredProgram()
            },
            {
                "call function object without parameters",
                """
                fn SomeFn() {}
                var a = SomeFn;
                a();
                """,
                LoweredProgram()
            },
            {
                "call function object with parameters",
                """
                fn SomeFn(a: string): string { return a; }
                var a = SomeFn;
                a("");
                """,
                LoweredProgram()
            }
        };
    }
}

