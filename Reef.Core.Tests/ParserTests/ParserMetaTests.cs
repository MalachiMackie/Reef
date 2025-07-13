using FluentAssertions;
using Reef.Core.Tests.ParserTests.TestCases;

namespace Reef.Core.Tests.ParserTests;

public class ParserMetaTests
{
    public static readonly IEnumerable<object?[]> ExpressionTypes = Enum.GetValues<ValueAccessType>()
        .Select(x => new object?[] { x, null, null })
        .Concat(Enum.GetValues<UnaryOperatorType>().Select(x => new object?[] { null, x, null }))
        .Concat(Enum.GetValues<BinaryOperatorType>().Select(x => new object?[] { null, null, x }));

    [Theory]
    [MemberData(nameof(ExpressionTypes))]
    public void Meta_Should_TestAllExpressionTypes(
        ValueAccessType? valueAccessType, UnaryOperatorType? unaryOperatorType, BinaryOperatorType? binaryOperatorType)
    {
        var testCases = PopExpressionTestCases.TestCases()
            .Select(x => x[^1])
            .Cast<IExpression>()
            // only check test cases that check for a single expression
            .ToArray();

        var checkedAccessTypes = testCases.OfType<ValueAccessorExpression>()
            .Select(x => x.ValueAccessor.AccessType);
        var checkedUnaryOperatorTypes = testCases.OfType<UnaryOperatorExpression>()
            .Where(x => x.UnaryOperator.Operand?.ExpressionType == ExpressionType.ValueAccess)
            .Select(x => x.UnaryOperator.OperatorType);
        var checkedBinaryOperatorTypes = testCases.OfType<BinaryOperatorExpression>()
            .Where(x => x.BinaryOperator.Left?.ExpressionType == ExpressionType.ValueAccess
                        && x.BinaryOperator.Right?.ExpressionType == ExpressionType.ValueAccess)
            .Select(x => x.BinaryOperator.OperatorType);

        if (valueAccessType.HasValue)
        {
            checkedAccessTypes.Should().Contain(valueAccessType.Value);
        }

        if (unaryOperatorType.HasValue)
        {
            checkedUnaryOperatorTypes.Should().Contain(unaryOperatorType.Value);
        }

        if (binaryOperatorType.HasValue)
        {
            checkedBinaryOperatorTypes.Should().Contain(binaryOperatorType.Value);
        }
    }

    [Theory]
    [MemberData(nameof(OperatorCombinationMetaTestCases))]
    public void Meta_Should_TestAllOperatorCombinations(BinaryOperatorType? binaryA, BinaryOperatorType? binaryB,
        UnaryOperatorType? unaryA, UnaryOperatorType? unaryB)
    {
        var testCases = PopExpressionTestCases.TestCases()
            .Select(x => x[^1])
            .Cast<IExpression>()
            .ToArray();

        if (binaryA.HasValue && binaryB.HasValue)
        {
            foreach (var x in testCases)
            {
                if (
                    //  a < (b * c)
                    (x is BinaryOperatorExpression
                    {
                        BinaryOperator:
                        {
                            OperatorType: var a1,
                            Left.ExpressionType: ExpressionType.ValueAccess,
                            Right:
                            BinaryOperatorExpression
                            {
                                BinaryOperator:
                                {
                                    OperatorType: var b1,
                                    Left.ExpressionType: ExpressionType.ValueAccess,
                                    Right.ExpressionType: ExpressionType.ValueAccess
                                }
                            }
                        }
                    } && a1 == binaryA.Value && b1 == binaryB.Value)
                    // (a < b) > c
                    || (x is
                        BinaryOperatorExpression
                        {
                            BinaryOperator:
                            {
                                OperatorType: var b2,
                                Left:
                                BinaryOperatorExpression
                                {
                                    BinaryOperator:
                                    {
                                        OperatorType: var a2,
                                        Left.ExpressionType: ExpressionType.ValueAccess,
                                        Right.ExpressionType: ExpressionType.ValueAccess
                                    }
                                },
                                Right.ExpressionType: ExpressionType.ValueAccess
                            }
                        } && a2 == binaryA.Value && b2 == binaryB.Value))
                {
                }
            }

            testCases.Count(x =>
                //  a < (b * c)
                (x is
                    BinaryOperatorExpression
                    {
                        BinaryOperator:
                        {
                            OperatorType: var a1,
                            Left.ExpressionType: ExpressionType.ValueAccess,
                            Right:
                            BinaryOperatorExpression
                            {
                                BinaryOperator:
                                {
                                    OperatorType: var b1,
                                    Left.ExpressionType: ExpressionType.ValueAccess,
                                    Right.ExpressionType: ExpressionType.ValueAccess
                                }
                            }
                        }
                    } && a1 == binaryA.Value && b1 == binaryB.Value)
                // (a < b) > c
                || (x is
                    BinaryOperatorExpression
                    {
                        BinaryOperator:
                        {
                            OperatorType: var b2,
                            Left:
                            BinaryOperatorExpression
                            {
                                BinaryOperator:
                                {
                                    OperatorType: var a2,
                                    Left.ExpressionType: ExpressionType.ValueAccess,
                                    Right.ExpressionType: ExpressionType.ValueAccess
                                }
                            },
                            Right.ExpressionType: ExpressionType.ValueAccess
                        }
                    } && a2 == binaryA.Value && b2 == binaryB.Value)).Should().BeGreaterThan(0);
        }

        // a < b?
        if (binaryA.HasValue && unaryB.HasValue)
        {
            testCases.Count(x =>
                    //  a < (b?)
                    (x is
                        BinaryOperatorExpression
                        {
                            BinaryOperator:
                            {
                                OperatorType: var a1,
                                Left.ExpressionType: ExpressionType.ValueAccess,
                                Right:
                                UnaryOperatorExpression
                                {
                                    UnaryOperator:
                                    {
                                        OperatorType: var b1,
                                        Operand.ExpressionType: ExpressionType.ValueAccess
                                    }
                                }
                            }
                        } && a1 == binaryA.Value && b1 == unaryB.Value)
                    // (a < b)?
                    || (x is
                        UnaryOperatorExpression
                        {
                            UnaryOperator:
                            {
                                OperatorType: var b2,
                                Operand:
                                BinaryOperatorExpression
                                {
                                    BinaryOperator:
                                    {
                                        OperatorType: var a2,
                                        Left.ExpressionType: ExpressionType.ValueAccess,
                                        Right.ExpressionType: ExpressionType.ValueAccess
                                    }
                                }
                            }
                        } && a2 == binaryA.Value && b2 == unaryB.Value)
                )
                .Should().BeGreaterThan(0);
        }

        // a? < b
        if (unaryA.HasValue && binaryB.HasValue)
        {
            testCases.Count(x =>
                    //  !(a < b)
                    (x is
                        UnaryOperatorExpression
                        {
                            UnaryOperator:
                            {
                                OperatorType: var a1,
                                Operand:
                                BinaryOperatorExpression
                                {
                                    BinaryOperator:
                                    {
                                        OperatorType: var b1,
                                        Left.ExpressionType: ExpressionType.ValueAccess,
                                        Right.ExpressionType: ExpressionType.ValueAccess
                                    }
                                }
                            }
                        } && a1 == unaryA.Value && b1 == binaryB.Value)
                    // (!a) < b
                    || (x is
                        BinaryOperatorExpression
                        {
                            BinaryOperator:
                            {
                                OperatorType: var b2,
                                Left:
                                UnaryOperatorExpression
                                {
                                    UnaryOperator:
                                    {
                                        OperatorType: var a2,
                                        Operand.ExpressionType: ExpressionType.ValueAccess
                                    }
                                },
                                Right.ExpressionType: ExpressionType.ValueAccess
                            }
                        } && a2 == unaryA.Value && b2 == binaryB.Value)
                )
                .Should().BeGreaterThan(0);
        }

        if (unaryA.HasValue && unaryB.HasValue)
        {
            testCases.Count(x => x is UnaryOperatorExpression
                {
                    UnaryOperator:
                    {
                        OperatorType: var a1,
                        Operand: UnaryOperatorExpression
                        {
                            UnaryOperator.OperatorType: var b1
                        }
                    }
                } && ((a1 == unaryA.Value && b1 == unaryB.Value) || (a1 == unaryB.Value && b1 == unaryA.Value)))
                .Should().BeGreaterThan(0);
        }
    }

    public static IEnumerable<object?[]> OperatorCombinationMetaTestCases()
    {
        var binaryCombinations = Enum.GetValues<BinaryOperatorType>()
            .SelectMany(x => Enum.GetValues<BinaryOperatorType>().Select(y => (x, y)));
        foreach (var (binaryA, binaryB) in binaryCombinations)
        {
            // a + b - c
            yield return [binaryA, binaryB, null, null];
        }

        var unaryCombinations = Enum.GetValues<UnaryOperatorType>()
            .SelectMany(x => Enum.GetValues<UnaryOperatorType>().Select(y => (x, y)));
        foreach (var (unaryA, unaryB) in unaryCombinations)
        {
            // a??
            yield return [null, null, unaryA, unaryB];
        }

        var binaryToUnaryCombinations = Enum.GetValues<BinaryOperatorType>()
            .SelectMany(x => Enum.GetValues<UnaryOperatorType>().Select(y => (x, y)));
        foreach (var (binary, unary) in binaryToUnaryCombinations)
        {
            // a + b?;
            yield return [binary, null, null, unary];
            // a? + b;
            yield return [null, binary, unary, null];
        }
    }
}