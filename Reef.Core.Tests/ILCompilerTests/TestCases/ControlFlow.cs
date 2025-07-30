using Reef.IL;

using static Reef.Core.Tests.ILCompilerTests.TestHelpers;

namespace Reef.Core.Tests.ILCompilerTests.TestCases;

public static class ControlFlow
{
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
                Module(
                    methods:
                    [
                        Method("SomeFn",
                            returnType: ConcreteTypeReference(
                                "result",
                                typeArguments:
                                [
                                    ConcreteTypeReference("int"),
                                    ConcreteTypeReference("string")
                                ]),
                            isStatic: true,
                            locals:
                            [
                                Local("a", ConcreteTypeReference("int"))
                            ],
                            instructions:
                            [
                                new LoadIntConstant(Addr(0), 1),
                                new LoadTypeFunction(Addr(1), ConcreteTypeReference(
                                    "result",
                                    typeArguments:
                                    [
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("string")
                                    ]), 0),
                                new Call(Addr(2)),
                                new CopyStack(Addr(3)),
                                new LoadField(Addr(4), 0, 0),
                                new LoadIntConstant(Addr(5), 1),
                                new CompareIntEqual(Addr(6)),
                                new BranchIfFalse(Addr(7), Addr(9)),
                                new Return(Addr(8)),
                                new LoadField(Addr(9), 0, 1),
                                new StoreLocal(Addr(10), 0),
                                new LoadIntConstant(Addr(11), 1),
                                new LoadTypeFunction(Addr(12), ConcreteTypeReference(
                                    "result",
                                    typeArguments:
                                    [
                                        ConcreteTypeReference("int"),
                                        ConcreteTypeReference("string")
                                    ]), 0),
                                new Call(Addr(13)),
                                Return(14)
                            ])
                    ])
            }
        };
    }
}