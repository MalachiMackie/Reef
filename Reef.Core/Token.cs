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
            TokenType.CharLiteral => $"'{StringValue}'",
            TokenType.SingleLineComment => $"//{StringValue}",
            TokenType.MultiLineComment => $"/*{StringValue}*/",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public record IntToken : Token
{
    public long? SignedValue { get; }
    public ulong? UnsignedValue { get; }

    public IntToken(long signedValue)
    {
        SignedValue = signedValue;
    }

    public IntToken(ulong unsignedValue)
    {
        UnsignedValue = unsignedValue;
    }

    public IntToken()
    {
        throw new InvalidOperationException();
    }

    public override string ToString()
    {
        return Type switch
        {
            TokenType.IntLiteral => SignedValue?.ToString() ?? UnsignedValue.NotNull().ToString(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public record struct IntLiteral(long SignedValue, ulong UnsignedValue);

public record Token
{
    public TokenType Type { get; protected init; }

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
            TokenType.Turbofish => "::<",
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
            TokenType.VariantOf => "variantOf",
            TokenType.Break => "break",
            TokenType.Continue => "continue",
            TokenType.Unboxed => "unboxed",
            TokenType.Boxed => "boxed",
            TokenType.LeftSquareBracket => "[",
            TokenType.RightSquareBracket => "]",
            TokenType.Use => "use",
            TokenType.TripleColon => ":::",
            TokenType.Extern => "extern",
            TokenType.Hash => "#",
            TokenType.DoubleDash => "--",
            TokenType.DoublePlus => "++",
            TokenType.LeftAngleBracketEquals => "<=",
            TokenType.RightAngleBracketEquals => ">=",
            _ => throw new UnreachableException(Type.ToString())
        };
    }

    // todo: should probably keep index positions

    public static Token Pub(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Pub, SourceSpan = sourceSpan };
    }

    public static Token Union(SourceSpan sourceSpan) => new() { Type = TokenType.Union, SourceSpan = sourceSpan };
    public static Token VariantOf(SourceSpan sourceSpan) => new() { Type = TokenType.VariantOf, SourceSpan = sourceSpan };
    public static Token Unboxed(SourceSpan sourceSpan) => new() { Type = TokenType.Unboxed, SourceSpan = sourceSpan };
    public static Token Extern(SourceSpan sourceSpan) => new() { Type = TokenType.Extern, SourceSpan = sourceSpan };
    public static Token Boxed(SourceSpan sourceSpan) => new() { Type = TokenType.Boxed, SourceSpan = sourceSpan };
    public static Token Attribute(SourceSpan sourceSpan) => new() { Type = TokenType.Attribute, SourceSpan = sourceSpan };

    public static Token Static(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Static, SourceSpan = sourceSpan };
    }

    public static Token Matches(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Matches, SourceSpan = sourceSpan };
    }

    public static Token Match(SourceSpan sourceSpan) => new() { Type = TokenType.Match, SourceSpan = sourceSpan };
    public static Token Use(SourceSpan sourceSpan) => new() { Type = TokenType.Use, SourceSpan = sourceSpan };
    public static Token While(SourceSpan sourceSpan) => new() { Type = TokenType.While, SourceSpan = sourceSpan };
    public static Token Grab(SourceSpan sourceSpan) => new() { Type = TokenType.Grab, SourceSpan = sourceSpan };
    public static Token Where(SourceSpan sourceSpan) => new() { Type = TokenType.Where, SourceSpan = sourceSpan };
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

    public static Token TripleColon(SourceSpan sourceSpan) => new() { Type = TokenType.TripleColon, SourceSpan = sourceSpan };

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

    public static Token LeftAngleBracketEquals(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.LeftAngleBracketEquals, SourceSpan = sourceSpan };
    }

    public static Token RightAngleBracket(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.RightAngleBracket, SourceSpan = sourceSpan };
    }

    public static Token RightAngleBracketEquals(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.RightAngleBracketEquals, SourceSpan = sourceSpan };
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

    public static StringToken StringLiteral(string value, SourceSpan sourceSpan)
    {
        return new StringToken { StringValue = value, Type = TokenType.StringLiteral, SourceSpan = sourceSpan };
    }

    public static StringToken CharLiteral(string value, SourceSpan sourceSpan)
    {
        return new StringToken { StringValue = value, Type = TokenType.CharLiteral, SourceSpan = sourceSpan };
    }

    public static IntToken IntLiteral(long value, SourceSpan sourceSpan)
    {
        return new IntToken(value)
        {
            SourceSpan = sourceSpan,
            Type = TokenType.IntLiteral
        };
    }

    public static IntToken IntLiteral(ulong value, SourceSpan sourceSpan)
    {
        return new IntToken(value)
        {
            SourceSpan = sourceSpan,
            Type = TokenType.IntLiteral
        };
    }

    public static Token Todo(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.Todo, SourceSpan = sourceSpan };
    }

    public static Token QuestionMark(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.QuestionMark, SourceSpan = sourceSpan };
    }

    public static Token LeftSquareBracket(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.LeftSquareBracket, SourceSpan = sourceSpan };
    }

    public static Token RightSquareBracket(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.RightSquareBracket, SourceSpan = sourceSpan };
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

    public static Token DoubleDash(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.DoubleDash, SourceSpan = sourceSpan };
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

    public static Token DoublePlus(SourceSpan sourceSpan)
    {
        return new Token { Type = TokenType.DoublePlus, SourceSpan = sourceSpan };
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

    public static Token Hash(SourceSpan sourceSpan)
    {
        return new Token
        {
            Type = TokenType.Hash,
            SourceSpan = sourceSpan
        };
    }

    public static Token For(SourceSpan sourceSpan)
    {
        return new Token
        {
            Type = TokenType.For,
            SourceSpan = sourceSpan
        };
    }
}
