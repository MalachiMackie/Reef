using System.Runtime.CompilerServices;
using FluentAssertions;

namespace NewLang.Core.Tests;

public class ExpressionTests
{
    private readonly ExpressionBuilder _expressionBuilder = new();

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Tests(IEnumerable<Token> tokens, IEnumerable<Expression> expectedExpression)
    {
        var expression = _expressionBuilder.GetExpressions(tokens)
            // clear out the source spans, we don't actually care about them
            .Select(RemoveSourceSpan);

        expression.Should().BeEquivalentTo(expectedExpression);
    }

    public static IEnumerable<object[]> TestCases()
    {
        return new (string Source, IEnumerable<Expression> ExpectedExpression)[]
        {
            // value access expressions
            ("a", [new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default)))]),
            ("1", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))]),
            ("\"my string\"", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("my string", default)))]),
            ("true", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))]),
            ("false", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.False(default)))]),
            // binary operator expressions
            ("a < 5", [new Expression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, default))),
                Token.LeftAngleBracket(default)))]),
            ("\"thing\" > true", [new Expression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default))),
                Token.RightAngleBracket(default)))]),
        }.Select(x => new object[] { new Parser().Parse(x.Source), x.ExpectedExpression });
    }
    
    private static Expression RemoveSourceSpan(Expression expression)
    {
        return expression with
        {
            ValueAccessor = RemoveSourceSpan(expression.ValueAccessor),
            UnaryOperator = RemoveSourceSpan(expression.UnaryOperator),
            BinaryOperator = RemoveSourceSpan(expression.BinaryOperator),
        };
    }

    private static StrongBox<BinaryOperator>? RemoveSourceSpan(StrongBox<BinaryOperator>? binaryOperator)
    {
        return binaryOperator is not null
            ? new StrongBox<BinaryOperator>(binaryOperator.Value with
            {
                Left = RemoveSourceSpan(binaryOperator.Value.Left),
                Right = RemoveSourceSpan(binaryOperator.Value.Right),
                OperatorToken = RemoveSourceSpan(binaryOperator.Value.OperatorToken)
            })
            : null;
    }

    private static StrongBox<UnaryOperator>? RemoveSourceSpan(StrongBox<UnaryOperator>? unaryOperator)
    {
        // todo
        return unaryOperator;
    }

    private static ValueAccessor? RemoveSourceSpan(ValueAccessor? valueAccessor)
    {
        return valueAccessor is not null
            ? valueAccessor.Value with { Token = RemoveSourceSpan(valueAccessor.Value.Token) }
            : null;
    }

    private static Token RemoveSourceSpan(Token token)
    {
        return token with { SourceSpan = default };
    }
}