using FluentAssertions;

namespace NewLang.Core.Tests;

public class ParserTests
{
    private readonly Parser _parser = new();

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Tests(string source, IEnumerable<Token> expectedTokens)
    {
        var result = _parser.Parse(source);

        result.Should().BeEquivalentTo(expectedTokens);
    }

    public static IEnumerable<object[]> TestCases() =>
    [
        // empty source
        ["", Array.Empty<Token>()],
        [" ", Array.Empty<Token>()],
        ["  ", Array.Empty<Token>()],
        ["\t\t", Array.Empty<Token>()],
        
        // single tokens
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
        
        // single token padding tests
        [" fn ", new [] { Token.Fn() }],
        ["fn ", new [] { Token.Fn() }],
        [" fn", new [] { Token.Fn() }],
        ["\tfn\t", new [] { Token.Fn() }],
        
        // two tokens separated by whitespace
        ["pub fn", new [] { Token.Pub(), Token.Fn() }],
        ["pub 5", new [] { Token.Pub(), Token.IntLiteral(5) }],
        ["5 pub", new [] { Token.IntLiteral(5), Token.Pub() }],
        ["\"string\" pub", new [] { Token.StringLiteral("string"), Token.Pub() }],
        
        // multiple tokens without separation
        ["int)", new[] { Token.IntKeyword(), Token.RightParenthesis()}],
        ["a)", new[] { Token.Identifier("a"), Token.RightParenthesis()}],
        ["i)", new[] { Token.Identifier("i"), Token.RightParenthesis()}],
        ["a)?;", new[] { Token.Identifier("a"), Token.RightParenthesis(), Token.QuestionMark(), Token.Semicolon()}],
        
        // two tokens padding tests
        [" pub fn ", new [] { Token.Pub(), Token.Fn() }],
        ["pub fn ", new [] { Token.Pub(), Token.Fn() }],
        [" pub fn", new [] { Token.Pub(), Token.Fn() }],
        [" pub  fn", new [] { Token.Pub(), Token.Fn() }],
        ["\tpub\tfn\t", new [] { Token.Pub(), Token.Fn() }],
        
        // full source
        [
            """
            pub fn DoSomething(a: int): result<int, string> {
                var b = 2;
                
                if (a == b) {
                    return ok(a);
                }
            }
            """,
            new []
            {
                Token.Pub(), Token.Fn(), Token.Identifier("DoSomething"), Token.LeftParenthesis(), Token.Identifier("a"),
                Token.Colon(), Token.IntKeyword(), Token.RightParenthesis(), Token.Colon(), Token.Result(), Token.LeftAngleBracket(),
                Token.IntKeyword(), Token.Comma(), Token.StringKeyword(), Token.RightAngleBracket(), Token.LeftBrace(),
                Token.Var(), Token.Identifier("b"), Token.Equals(), Token.IntLiteral(2), Token.Semicolon(), Token.If(), Token.LeftParenthesis(),
                Token.Identifier("a"), Token.DoubleEquals(), Token.Identifier("b"), Token.RightParenthesis(), Token.LeftBrace(),
                Token.Return(), Token.Ok(), Token.LeftParenthesis(), Token.Identifier("a"), Token.RightParenthesis(), Token.Semicolon(),
                Token.RightBrace(), Token.RightBrace()
            }
        ]
    ];
    
    public static IEnumerable<object[]> SomeTestCases() => 
    [
        [")>;", Token.RightParenthesis(), 1],
        [">;", Token.RightAngleBracket(), 1],
        [";", Token.Semicolon(), 1],
        [" ;", Token.Semicolon(), 2],
        ["int):", Token.IntKeyword(), 3],
        [" int):", Token.IntKeyword(), 4],
    ];

    [Theory]
    [MemberData(nameof(SomeTestCases))]
    public void SomeTests(string source, Token expectedToken, int expectedOffset)
    {
        var result = Parser.EatToken(source);
        result.Should().NotBeNull();
        var (token, offset) = result.Value;

        token.Should().Be(expectedToken);
        offset.Should().Be(expectedOffset);
    }
}