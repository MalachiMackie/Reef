using System.Diagnostics;
using FluentAssertions;

namespace NewLang.Core.Tests;

public class ParserMetaTests
{
    public static readonly IEnumerable<object[]> TokenTypes = Enum.GetValues<TokenType>()
        .Select(x => new object[] { x });
    
    [Theory]
    [MemberData(nameof(TokenTypes))]
    public void Should_TestAllTokenTypes(TokenType tokenType)
    {
        var testCaseTokenTypes = ParserTests.TestCases()
            .Select(x => x[1] as Token[] ?? throw new UnreachableException())
            // only check test cases that have a single token
            .Where(x => x.Length == 1)
            .Select(x => x[0].Type)
            .Distinct();

        testCaseTokenTypes.Should().Contain(tokenType);
    }
}