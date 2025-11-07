using System.Diagnostics;

namespace Reef.Core.Tests;

public class TokenizerMetaTests
{
    public static readonly IEnumerable<object[]> TokenTypes = Enum.GetValues<TokenType>()
        .Except([TokenType.None])
        .Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(TokenTypes))]
    public void Should_TestAllTokenTypes(TokenType tokenType)
    {
        var testCaseTokenTypes = TokenizerTests.TestCases()
            .Select(x => x[1] as Token[] ?? throw new UnreachableException())
            // only check test cases that have a single token
            .Where(x => x.Length == 1)
            .Select(x => x[0].Type)
            .Distinct();

        testCaseTokenTypes.Should().Contain(tokenType);
    }
}