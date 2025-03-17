using FluentAssertions;

namespace NewLang.Core.Tests;

public class ParserTests
{
    private readonly Parser _parser = new();
    
    [Theory]
    [MemberData(nameof(SingleTokensData))]
    public void SingleTokens(string source, IEnumerable<Token> expectedTokens)
    {
        var result = _parser.Parse(source);

        result.Should().BeEquivalentTo(expectedTokens);
    }

    public static IEnumerable<object[]> SingleTokensData()
    {
        return [
            [ "pub", new [] { Token.Pub() } ],
            [ "fn", new [] { Token.Fn() } ],
            [ "int", new [] { Token.IntKeyword() } ],
            [ "(", new [] { Token.LeftParenthesis() } ],
            [ ")", new [] { Token.RightParenthesis() } ],
            [ "{", new [] { Token.LeftBrace() } ],
            [ "}", new [] { Token.RightBrace() } ],
            [ "<", new [] { Token.LeftAngleBracket() } ],
            [ ">", new [] { Token.RightAngleBracket() } ],
            [ ",", new [] { Token.Comma() } ],
            [ "hello", new [] { Token.Identifier("hello") } ],
            [ ":", new [] { Token.Colon() } ],
            [ ";", new [] { Token.Semicolon() } ],
            [ "result", new [] { Token.Result() } ],
            [ "ok", new [] { Token.Ok() } ],
            [ "error", new [] { Token.Error() } ],
            [ "\"hello this is a string\"", new [] { Token.StringLiteral("hello this is a string") } ],
            [ "5", new [] { Token.IntLiteral(5) } ],
            [ "?", new [] { Token.QuestionMark() } ],
        ];
    }
}