using FluentAssertions;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

using static Reef.Core.Tests.LoweredProgramHelpers;

namespace Reef.Core.Tests.AbseilTests;

public class ClosureTests : TestBase
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ClosureAbseilTest(string description, string source, LoweredProgram expectedProgram)
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
                "Closure accesses local variable",
                """
                fn MyFn()
                {
                    var a = "";
                    fn InnerFn() {
                        var b = a;
                    }
                }
                """,
                LoweredProgram(
                    types: [
                        DataType("MyFn__Locals",
                            variants: [
                                Variant("_classVariant", [Field("a", StringType)])
                            ]),
                        DataType("MyFn__InnerFn__Closure",
                            variants: [
                                Variant("_classVariant", [Field("MyFn__Locals", ConcreteTypeReference("MyFn__Locals"))])
                            ])
                    ],
                    methods: [
                        Method(
                            "MyFn",
                            [
                                VariableDeclaration(
                                    "__locals", 
                                    CreateObject(
                                        ConcreteTypeReference("MyFn__Locals"),
                                        "_classVariant",
                                        true),
                                    false),
                                FieldAssignment(
                                    LocalAccess("__locals", true, ConcreteTypeReference("MyFn__Locals")),
                                    "_classVariant",
                                    "a",
                                    StringConstant("", true),
                                    false,
                                    StringType),
                                MethodReturnUnit()
                            ],
                            locals: [
                                Local("__locals", ConcreteTypeReference("MyFn__Locals"))
                            ]),
                        Method(
                            "MyFn__InnerFn",
                            [
                                VariableDeclaration(
                                    "b",
                                    FieldAccess(
                                        FieldAccess(
                                            LoadArgument(0, true, ConcreteTypeReference("MyFn__InnerFn__Closure")),
                                            "MyFn__Locals",
                                            "_classVariant",
                                            true,
                                            ConcreteTypeReference("MyFn__Locals")),
                                        "a",
                                        "_classVariant",
                                        true,
                                        StringType),
                                    false),
                                MethodReturnUnit()
                            ],
                            parameters: [
                                ConcreteTypeReference("MyFn__InnerFn__Closure")
                            ],
                            locals: [
                                Local("b", StringType)
                            ])
                    ])
            }
        };
    }

}
