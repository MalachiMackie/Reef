using System.Diagnostics;
using FluentAssertions;

namespace NewLang.Core.Tests;

public class ExpressionMetaTests
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
        var testCases = ExpressionTests.TestCases()
            .Select(x => (x[^1] as IEnumerable<Expression> ?? throw new UnreachableException()).ToArray())
            // only check test cases that check for a single expression
            .Where(x => x.Length == 1)
            .Select(x => x[0])
            .ToArray();

        var checkedAccessTypes = testCases.Where(x => x.Type == ExpressionType.ValueAccess)
            .Select(x => x.ValueAccessor!.Value.AccessType);
        var checkedUnaryOperatorTypes = testCases.Where(x => x.Type == ExpressionType.UnaryOperator)
            .Where(x => x.UnaryOperator!.Value.Operand.Type == ExpressionType.ValueAccess)
            .Select(x => x.UnaryOperator!.Value.Type);
        var checkedBinaryOperatorTypes = testCases.Where(x => x.Type == ExpressionType.BinaryOperator)
            .Where(x => x.BinaryOperator!.Value.Left.Type == ExpressionType.ValueAccess
                && x.BinaryOperator!.Value.Right.Type == ExpressionType.ValueAccess)
            .Select(x => x.BinaryOperator!.Value.Type);

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
    public void Meta_Should_TestAllOperatorCombinations(BinaryOperatorType? binaryA, BinaryOperatorType? binaryB, UnaryOperatorType? unaryA, UnaryOperatorType? unaryB)
    {
        
        var testCases = ExpressionTests.TestCases()
            .Select(x => (x[^1] as IEnumerable<Expression> ?? throw new UnreachableException()).ToArray())
            .Where(x => x.Length == 1)
            .Select(x => x[0])
            .ToArray();

        if (binaryA.HasValue && binaryB.HasValue)
        {
            foreach (var x in testCases)
            {
                if (
                        //  a < (b * c)
                        x is
                        {
                            Type: ExpressionType.BinaryOperator,
                            BinaryOperator.Value:
                            {
                                Type: var a1,
                                Left.Type: ExpressionType.ValueAccess,
                                Right:
                                {
                                    Type: ExpressionType.BinaryOperator,
                                    BinaryOperator.Value:
                                    {
                                        Type: var b1,
                                        Left.Type: ExpressionType.ValueAccess,
                                        Right.Type: ExpressionType.ValueAccess
                                    }
                                }
                            }
                        } && a1 == binaryA.Value && b1 == binaryB.Value
                        // (a < b) > c
                        || x is
                        {
                            Type: ExpressionType.BinaryOperator,
                            BinaryOperator.Value:
                            {
                                Type: var b2,
                                Left:
                                {
                                    Type: ExpressionType.BinaryOperator,
                                    BinaryOperator.Value:
                                    {
                                        Type: var a2,
                                        Left.Type: ExpressionType.ValueAccess,
                                        Right.Type: ExpressionType.ValueAccess
                                    }
                                },
                                Right.Type: ExpressionType.ValueAccess
                            }
                        } && a2 == binaryA.Value && b2 == binaryB.Value)
                {
                    
                }
            }
            testCases.Count(x => 
                //  a < (b * c)
                        x is
                        {
                            Type: ExpressionType.BinaryOperator,
                            BinaryOperator.Value:
                            {
                                Type: var a1,
                                Left.Type: ExpressionType.ValueAccess,
                                Right:
                                {
                                    Type: ExpressionType.BinaryOperator,
                                    BinaryOperator.Value:
                                    {
                                        Type: var b1,
                                        Left.Type: ExpressionType.ValueAccess,
                                        Right.Type: ExpressionType.ValueAccess
                                    }
                                }
                            }
                        } && a1 == binaryA.Value && b1 == binaryB.Value
                        // (a < b) > c
                        || x is
                        {
                            Type: ExpressionType.BinaryOperator,
                            BinaryOperator.Value:
                            {
                                Type: var b2,
                                Left:
                                {
                                    Type: ExpressionType.BinaryOperator,
                                    BinaryOperator.Value:
                                    {
                                        Type: var a2,
                                        Left.Type: ExpressionType.ValueAccess,
                                        Right.Type: ExpressionType.ValueAccess
                                    }
                                },
                                Right.Type: ExpressionType.ValueAccess
                            }
                        } && a2 == binaryA.Value && b2 == binaryB.Value).Should().BeGreaterThan(0);
        }

        // a < b?
        if (binaryA.HasValue && unaryB.HasValue)
        {
            testCases.Count(x =>
                    //  a < (b?)
                        x is
                        {
                            Type: ExpressionType.BinaryOperator,
                            BinaryOperator.Value:
                            {
                                Type: var a1,
                                Left.Type: ExpressionType.ValueAccess,
                                Right:
                                {
                                    Type: ExpressionType.UnaryOperator,
                                    UnaryOperator.Value:
                                    {
                                        Type: var b1,
                                        Operand.Type: ExpressionType.ValueAccess,
                                    }
                                }
                            }
                        } && a1 == binaryA.Value && b1 == unaryB.Value
                        // (a < b)?
                        || x is
                        {
                            Type: ExpressionType.UnaryOperator,
                            UnaryOperator.Value:
                            {
                                Type: var b2,
                                Operand:
                                {
                                    Type: ExpressionType.BinaryOperator,
                                    BinaryOperator.Value:
                                    {
                                        Type: var a2,
                                        Left.Type: ExpressionType.ValueAccess,
                                        Right.Type: ExpressionType.ValueAccess
                                    }
                                }
                            }
                        } && a2 == binaryA.Value && b2 == unaryB.Value
                )
                .Should().BeGreaterThan(0);
        }
        
        // a? < b
        if (unaryA.HasValue && binaryB.HasValue)
        {
            testCases.Count(x =>
                    //  !(a < b)
                        x is
                        {
                            Type: ExpressionType.UnaryOperator,
                            UnaryOperator.Value:
                            {
                                Type: var a1,
                                Operand:
                                {
                                    Type: ExpressionType.BinaryOperator,
                                    BinaryOperator.Value:
                                    {
                                        Type: var b1,
                                        Left.Type: ExpressionType.ValueAccess,
                                        Right.Type: ExpressionType.ValueAccess,
                                    }
                                }
                            }
                        } && a1 == unaryA.Value && b1 == binaryB.Value
                        // (!a) < b
                        || x is
                        {
                            Type: ExpressionType.BinaryOperator,
                            BinaryOperator.Value:
                            {
                                Type: var b2,
                                Left:
                                {
                                    Type: ExpressionType.UnaryOperator,
                                    UnaryOperator.Value:
                                    {
                                        Type: var a2,
                                        Operand.Type: ExpressionType.ValueAccess
                                    }
                                },
                                Right.Type: ExpressionType.ValueAccess
                            }
                        } && a2 == unaryA.Value && b2 == binaryB.Value
                )
                .Should().BeGreaterThan(0);
        }
        
        if (unaryA.HasValue && unaryB.HasValue)
        {
            testCases.Should().Contain(x => x.Type == ExpressionType.UnaryOperator
                                            && x.UnaryOperator!.Value.Type == unaryA.Value
                                            && x.UnaryOperator!.Value.Operand.Type == ExpressionType.UnaryOperator
                                            && x.UnaryOperator!.Value.Operand.UnaryOperator!.Value.Type == unaryB.Value);
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