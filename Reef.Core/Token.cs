using System.Diagnostics;

namespace Reef.Core;

public record StringToken : Token
{
    public required string StringValue { get; init; }

    public override string ToString()
    {
        return Type switch
        {
            TokenType.Identifier => StringValue,
            TokenType.StringLiteral => $"\"{StringValue}\"",
            TokenType.SingleLineComment => $"//{StringValue}",
            TokenType.MultiLineComment => $"/*{StringValue}*/",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public record IntToken : Token
{
    public required int IntValue { get; init; }

    public override string ToString()
    {
        return Type switch
        {
            TokenType.IntLiteral => $"{IntValue}",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public record Token
{
    public TokenType Type { get; private init; }

    public required SourceSpan SourceSpan { get; init; }

    public override string ToString()
    {
        return Type switch
        {
            TokenType.If => "if",
            TokenType.LeftParenthesis => "(",
            TokenType.RightParenthesis => ")",
            TokenType.Semicolon => ";",
            TokenType.LeftBrace => "{",
            TokenType.RightBrace => "}",
            TokenType.Pub => "pub",
            TokenType.Fn => "fn",
            TokenType.Colon => ":",
            TokenType.LeftAngleBracket => "<",
            TokenType.RightAngleBracket => ">",
            TokenType.Var => "var",
            TokenType.Equals => "=",
            TokenType.Comma => ",",
            TokenType.DoubleEquals => "==",
            TokenType.NotEquals => "!=",
            TokenType.Else => "else",
            TokenType.QuestionMark => "?",
            TokenType.Return => "return",
            TokenType.True => "true",
            TokenType.False => "false",
            TokenType.Plus => "+",
            TokenType.Dash => "-",
            TokenType.Star => "*",
            TokenType.ForwardSlash => "/",
            TokenType.Mut => "mut",
            TokenType.Class => "class",
            TokenType.Dot => ".",
            TokenType.Turbofish => "::",
            TokenType.None => "none",
            TokenType.Field => "field",
            TokenType.New => "new",
            TokenType.DoubleColon => "::",
            TokenType.Static => "static",
            TokenType.Union => "union",
            TokenType.Underscore => "_",
            TokenType.Matches => "matches",
            TokenType.Match => "match",
            TokenType.Bang => "!",
            TokenType.Todo => "todo!",
            TokenType.EqualsArrow => "=>",
            TokenType.DoubleAmpersand => "&&",
            TokenType.DoubleBar => "||",
            TokenType.While => "while",
            TokenType.Break => "break",
            TokenType.Continue => "continue",
            TokenType.Unboxed => "unboxed",
            TokenType.Boxed => "boxed",
            _ => throw new UnreachableException(Type.ToString())
        };
    }

    // todo: should probably keep index positions

    public static Token Pub(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Pub, SourceSpan = sourceSpan };
    }

    public static Token Union(SourceSpan sourceSpan) => new() { Type = TokenType.Union, SourceSpan = sourceSpan };
    public static Token Unboxed(SourceSpan sourceSpan) => new() { Type = TokenType.Unboxed, SourceSpan = sourceSpan };
    public static Token Boxed(SourceSpan sourceSpan) => new() { Type = TokenType.Boxed, SourceSpan = sourceSpan };

    public static Token Static(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Static, SourceSpan = sourceSpan };
    }

    public static Token Matches(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Matches, SourceSpan = sourceSpan };
    }

    public static Token Match(SourceSpan sourceSpan) => new() { Type = TokenType.Match, SourceSpan = sourceSpan };
    public static Token While(SourceSpan sourceSpan) => new() { Type = TokenType.While, SourceSpan = sourceSpan };
    public static Token Break(SourceSpan sourceSpan) => new() { Type = TokenType.Break, SourceSpan = sourceSpan };
    public static Token Continue(SourceSpan sourceSpan) => new() { Type = TokenType.Continue, SourceSpan = sourceSpan };

    public static Token Underscore(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Underscore, SourceSpan = sourceSpan };
    }

    public static Token DoubleColon(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.DoubleColon, SourceSpan = sourceSpan };
    }

    public static Token Fn(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Fn, SourceSpan = sourceSpan };
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

    public static Token Turbofish(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Turbofish, SourceSpan = sourceSpan };
    }

    public static Token EqualsArrow(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.EqualsArrow, SourceSpan = sourceSpan };
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

    public static StringToken Identifier(string value, SourceSpan sourceSpan)
    {
        return new StringToken { StringValue = value, Type = TokenType.Identifier, SourceSpan = sourceSpan };
    }

    public static Token DoubleEquals(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.DoubleEquals, SourceSpan = sourceSpan };
    }

    public static Token NotEquals(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.NotEquals, SourceSpan = sourceSpan };
    }

    public static Token Else(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Else, SourceSpan = sourceSpan };
    }

    public static Token If(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.If, SourceSpan = sourceSpan };
    }

    public static Token StringLiteral(string value, SourceSpan sourceSpan)
    {
        return new StringToken { StringValue = value, Type = TokenType.StringLiteral, SourceSpan = sourceSpan };
    }

    public static Token IntLiteral(int value, SourceSpan sourceSpan)
    {
        return new IntToken { IntValue = value, Type = TokenType.IntLiteral, SourceSpan = sourceSpan };
    }

    public static Token Todo(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Todo, SourceSpan = sourceSpan };
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

    public static Token Dash(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Dash, SourceSpan = sourceSpan };
    }

    public static Token Star(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Star, SourceSpan = sourceSpan };
    }

    public static Token Bang(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Bang, SourceSpan = sourceSpan };
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

    public static Token New(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.New, SourceSpan = sourceSpan };
    }

    public static Token SingleLineComment(string contents, SourceSpan sourceSpan)
    {
        return new StringToken
        {
            SourceSpan = sourceSpan,
            StringValue = contents,
            Type = TokenType.SingleLineComment
        };
    }

    public static Token MultiLineComment(string contents, SourceSpan sourceSpan)
    {
        return new StringToken
        {
            SourceSpan = sourceSpan,
            StringValue = contents,
            Type = TokenType.MultiLineComment
        };
    }

    public static Token DoubleAmpersand(SourceSpan sourceSpan)
    {
        return new Token
        {
            Type = TokenType.DoubleAmpersand,
            SourceSpan = sourceSpan
        };
    }

    public static Token DoubleBar(SourceSpan sourceSpan)
    {
        return new Token
        {
            Type = TokenType.DoubleBar,
            SourceSpan = sourceSpan
        };
    }
}