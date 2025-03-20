using FluentAssertions;

namespace NewLang.Core.Tests;

public class ExpressionTests
{
    private readonly ExpressionBuilder _expressionBuilder = new();

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Tests(IEnumerable<Token> tokens, IEnumerable<Expression> expectedExpression)
    {
        var expression = _expressionBuilder.GetExpressions(tokens);

        expression.Should().BeEquivalentTo(expectedExpression);
    }

    public static IEnumerable<object[]> TestCases()
    {
        return new (string Source, IEnumerable<Expression> ExpectedExpression)[]
        {
            ("a", [Expression.VariableAccess([Token.Identifier("a", new SourceSpan(new SourcePosition(0, 0, 0), 1))])])
        }.Select(x => new object[] { new Parser().Parse(x.Source), x.ExpectedExpression });
    }
}