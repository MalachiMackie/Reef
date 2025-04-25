using System.Diagnostics;

namespace NewLang.Core;

public readonly struct Token
{
    public TokenType Type { get; private init; }

    // todo: can we 'overlap' these fields like an rust enum would?

    public string? StringValue { get; init; }
    public int? IntValue { get; init; }

    public required SourceSpan SourceSpan { get; init; }

    public override string ToString()
    {
        return Type switch
        {
            TokenType.Identifier => StringValue!,
            TokenType.If => "if",
            TokenType.LeftParenthesis => "(",
            TokenType.RightParenthesis => ")",
            TokenType.Semicolon => ";",
            TokenType.LeftBrace => "{",
            TokenType.RightBrace => "}",
            TokenType.Pub => "pub",
            TokenType.Fn => "fn",
            TokenType.IntKeyword => "int",
            TokenType.Colon => ":",
            TokenType.LeftAngleBracket => "<",
            TokenType.RightAngleBracket => ">",
            TokenType.Var => "var",
            TokenType.Equals => "=",
            TokenType.Comma => ",",
            TokenType.DoubleEquals => "==",
            TokenType.Else => "else",
            TokenType.IntLiteral => IntValue!.Value.ToString(),
            TokenType.StringLiteral => $"\"{StringValue}\"",
            TokenType.StringKeyword => "string",
            TokenType.Result => "result",
            TokenType.Ok => "ok",
            TokenType.Error => "error",
            TokenType.QuestionMark => "?",
            TokenType.Return => "return",
            TokenType.True => "true",
            TokenType.False => "false",
            TokenType.Bool => "bool",
            TokenType.Plus => "+",
            TokenType.Dash => "-",
            TokenType.Star => "*",
            TokenType.ForwardSlash => "/",
            TokenType.Mut => "mut",
            TokenType.Class => "class",
            TokenType.Dot => ".",
            _ => throw new UnreachableException()
        };
    }

    // todo: should probably keep index positions

    public static Token Pub(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Pub, SourceSpan = sourceSpan };
    }

    public static Token Fn(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Fn, SourceSpan = sourceSpan };
    }

    public static Token IntKeyword(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.IntKeyword, SourceSpan = sourceSpan };
    }

    public static Token LeftParenthesis(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.LeftParenthesis, SourceSpan = sourceSpan };
    }

    public static Token RightParenthesis(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.RightParenthesis, SourceSpan = sourceSpan };
    }

    public static Token Class(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Class, SourceSpan = sourceSpan };
    }

    public static Token Field(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Field, SourceSpan = sourceSpan };
    }

    public static Token Mut(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Mut, SourceSpan = sourceSpan };
    }

    public static Token LeftBrace(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.LeftBrace, SourceSpan = sourceSpan };
    }

    public static Token RightBrace(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.RightBrace, SourceSpan = sourceSpan };
    }

    public static Token Colon(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Colon, SourceSpan = sourceSpan };
    }

    public static Token Semicolon(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Semicolon, SourceSpan = sourceSpan };
    }

    public static Token LeftAngleBracket(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.LeftAngleBracket, SourceSpan = sourceSpan };
    }

    public static Token RightAngleBracket(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.RightAngleBracket, SourceSpan = sourceSpan };
    }

    public static Token Comma(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Comma, SourceSpan = sourceSpan };
    }

    public static Token Equals(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Equals, SourceSpan = sourceSpan };
    }

    public static Token Var(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Var, SourceSpan = sourceSpan };
    }

    public static Token Identifier(string value, SourceSpan sourceSpan)
    {
        return new Token { StringValue = value, Type = TokenType.Identifier, SourceSpan = sourceSpan };
    }

    public static Token DoubleEquals(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.DoubleEquals, SourceSpan = sourceSpan };
    }

    public static Token Else(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Else, SourceSpan = sourceSpan };
    }

    public static Token If(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.If, SourceSpan = sourceSpan };
    }

    public static Token StringKeyword(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.StringKeyword, SourceSpan = sourceSpan };
    }

    public static Token StringLiteral(string value, SourceSpan sourceSpan)
    {
        return new Token { StringValue = value, Type = TokenType.StringLiteral, SourceSpan = sourceSpan };
    }

    public static Token IntLiteral(int value, SourceSpan sourceSpan)
    {
        return new Token { IntValue = value, Type = TokenType.IntLiteral, SourceSpan = sourceSpan };
    }

    public static Token Result(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Result, SourceSpan = sourceSpan };
    }

    public static Token Ok(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Ok, SourceSpan = sourceSpan };
    }

    public static Token Error(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Error, SourceSpan = sourceSpan };
    }

    public static Token QuestionMark(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.QuestionMark, SourceSpan = sourceSpan };
    }

    public static Token Return(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Return, SourceSpan = sourceSpan };
    }

    public static Token True(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.True, SourceSpan = sourceSpan };
    }

    public static Token False(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.False, SourceSpan = sourceSpan };
    }

    public static Token Bool(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Bool, SourceSpan = sourceSpan };
    }

    public static Token Dash(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Dash, SourceSpan = sourceSpan };
    }

    public static Token Star(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Star, SourceSpan = sourceSpan };
    }

    public static Token ForwardSlash(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.ForwardSlash, SourceSpan = sourceSpan };
    }

    public static Token Plus(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Plus, SourceSpan = sourceSpan };
    }

    public static Token Dot(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Dot, SourceSpan = sourceSpan };
    }
}