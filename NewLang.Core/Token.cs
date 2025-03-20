namespace NewLang.Core;

public readonly struct Token
{
    public TokenType Type { get; init; }
    
    // todo: can we 'overlap' these fields like an rust enum would?

    public string? StringValue { get; init; }
    public int? IntValue { get; init; }
    
    public required SourceSpan SourceSpan { get; init; }

    // todo: should probably keep index positions

    public static Token Pub(SourceSpan sourceSpan) => new() { Type = TokenType.Pub, SourceSpan = sourceSpan };
    public static Token Fn(SourceSpan sourceSpan) => new() { Type = TokenType.Fn, SourceSpan = sourceSpan };
    public static Token IntKeyword(SourceSpan sourceSpan) => new() { Type = TokenType.IntKeyword, SourceSpan = sourceSpan };
    public static Token LeftParenthesis(SourceSpan sourceSpan) => new() { Type = TokenType.LeftParenthesis, SourceSpan = sourceSpan };
    public static Token RightParenthesis(SourceSpan sourceSpan) => new() { Type = TokenType.RightParenthesis, SourceSpan = sourceSpan };
    public static Token LeftBrace(SourceSpan sourceSpan) => new() { Type = TokenType.LeftBrace, SourceSpan = sourceSpan };
    public static Token RightBrace(SourceSpan sourceSpan) => new() { Type = TokenType.RightBrace, SourceSpan = sourceSpan };
    public static Token Colon(SourceSpan sourceSpan) => new() { Type = TokenType.Colon, SourceSpan = sourceSpan };
    public static Token Semicolon(SourceSpan sourceSpan) => new() { Type = TokenType.Semicolon, SourceSpan = sourceSpan };
    public static Token LeftAngleBracket(SourceSpan sourceSpan) => new() { Type = TokenType.LeftAngleBracket, SourceSpan = sourceSpan };
    public static Token RightAngleBracket(SourceSpan sourceSpan) => new() { Type = TokenType.RightAngleBracket, SourceSpan = sourceSpan };
    public static Token Comma(SourceSpan sourceSpan) => new() { Type = TokenType.Comma, SourceSpan = sourceSpan };
    public static Token Equals(SourceSpan sourceSpan) => new() { Type = TokenType.Equals, SourceSpan = sourceSpan };
    public static Token Var(SourceSpan sourceSpan) => new() { Type = TokenType.Var, SourceSpan = sourceSpan };
    public static Token Identifier(string value, SourceSpan sourceSpan) => new() { StringValue = value, Type = TokenType.Identifier, SourceSpan = sourceSpan };
    public static Token DoubleEquals(SourceSpan sourceSpan) => new() { Type = TokenType.DoubleEquals, SourceSpan = sourceSpan };
    public static Token Else(SourceSpan sourceSpan) => new() { Type = TokenType.Else, SourceSpan = sourceSpan };
    public static Token If(SourceSpan sourceSpan) => new() { Type = TokenType.If, SourceSpan = sourceSpan };
    public static Token StringKeyword(SourceSpan sourceSpan) => new() { Type = TokenType.StringKeyword, SourceSpan = sourceSpan };
    public static Token StringLiteral(string value, SourceSpan sourceSpan) => new() { StringValue = value, Type = TokenType.StringLiteral, SourceSpan = sourceSpan };
    public static Token IntLiteral(int value, SourceSpan sourceSpan) => new() { IntValue = value, Type = TokenType.IntLiteral, SourceSpan = sourceSpan };
    public static Token Result(SourceSpan sourceSpan) => new() { Type = TokenType.Result, SourceSpan = sourceSpan };
    public static Token Ok(SourceSpan sourceSpan) => new() { Type = TokenType.Ok, SourceSpan = sourceSpan };
    public static Token Error(SourceSpan sourceSpan) => new() { Type = TokenType.Error, SourceSpan = sourceSpan };
    public static Token QuestionMark(SourceSpan sourceSpan) => new() { Type = TokenType.QuestionMark, SourceSpan = sourceSpan };
    public static Token Return(SourceSpan sourceSpan) => new() { Type = TokenType.Return, SourceSpan = sourceSpan };
    public static Token True(SourceSpan sourceSpan) => new() { Type = TokenType.True, SourceSpan = sourceSpan };
    public static Token False(SourceSpan sourceSpan) => new() { Type = TokenType.False, SourceSpan = sourceSpan };
    public static Token Bool(SourceSpan sourceSpan) => new() { Type = TokenType.Bool, SourceSpan = sourceSpan };
}