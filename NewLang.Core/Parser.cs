using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public readonly struct Token
{
    public TokenType Type { get; init; }
    
    // todo: can we 'overlap' these fields like an rust enum would?

    public string? StringValue { get; init; }
    public int? IntValue { get; init; }
    
    // todo: should probably keep index positions

    public static Token Pub() => new() { Type = TokenType.Pub };
    public static Token Fn() => new() { Type = TokenType.Fn };
    public static Token IntKeyword() => new() { Type = TokenType.IntKeyword };
    public static Token LeftParenthesis() => new() { Type = TokenType.LeftParenthesis };
    public static Token RightParenthesis() => new() { Type = TokenType.RightParenthesis };
    public static Token LeftBrace() => new() { Type = TokenType.LeftBrace };
    public static Token RightBrace() => new() { Type = TokenType.RightBrace };
    public static Token Colon() => new() { Type = TokenType.Colon };
    public static Token Semicolon() => new() { Type = TokenType.Semicolon };
    public static Token LeftAngleBracket() => new() { Type = TokenType.LeftAngleBracket };
    public static Token RightAngleBracket() => new() { Type = TokenType.RightAngleBracket };
    public static Token Comma() => new() { Type = TokenType.Comma };
    public static Token Equals() => new() { Type = TokenType.Equals };
    public static Token Var() => new() { Type = TokenType.Var };
    public static Token Identifier(string value) => new() { StringValue = value, Type = TokenType.Identifier };
    public static Token DoubleEquals() => new() { Type = TokenType.DoubleEquals };
    public static Token Else() => new() { Type = TokenType.Else };
    public static Token If() => new() { Type = TokenType.If };
    public static Token StringKeyword() => new() { Type = TokenType.StringKeyword };
    
    public static Token StringLiteral(string value) => new() { StringValue = value, Type = TokenType.StringLiteral };
    public static Token IntLiteral(int value) => new() { IntValue = value, Type = TokenType.IntLiteral };
    public static Token Result() => new() { Type = TokenType.Result };
    public static Token Ok() => new() { Type = TokenType.Ok };
    public static Token Error() => new() { Type = TokenType.Error };
    public static Token QuestionMark() => new() { Type = TokenType.QuestionMark };
    public static Token Return() => new() { Type = TokenType.Return };
    public static Token True() => new() { Type = TokenType.True };
    public static Token False() => new() { Type = TokenType.False };
    public static Token Bool() => new() { Type = TokenType.Bool };
}

public class Parser
{
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");

    public IEnumerable<Token> Parse(string sourceStr)
    {
        var startIndex = 0;
        var endIndex = sourceStr.Length - 1;
        var nextToken = EatToken(sourceStr.AsSpan()[startIndex..(endIndex + 1)]);
        while (nextToken.HasValue)
        {
            var (token, offset) = nextToken.Value;
            yield return token;
            startIndex += offset;
            
            nextToken = EatToken(sourceStr.AsSpan()[startIndex..(endIndex + 1)]);
        }
    }

    public static (Token Token, int NextCharOffset)? EatToken(ReadOnlySpan<char> source)
    {
        if (source.IsWhiteSpace())
        {
            return null;
        }

        var potentialTokens = Enum.GetValues<TokenType>().ToList();

        for (var i = 0; i < source.Length; i++)
        {
            var part = source[..(i + 1)];
            if (part.IsWhiteSpace())
            {
                continue;
            }

            var trimmed = part.TrimStart();
            
            var nextPotentialTokens = new List<TokenType>();
            foreach (var type in potentialTokens)
            {
                if (IsPotentiallyValid(type, trimmed))
                {
                    nextPotentialTokens.Add(type);
                }
            }

            if (nextPotentialTokens.Count == 0)
            {
                // previously we had multiple potential tokens, now we have none. need to resolve to one.
                // this will happen in the case of int)
                return (ResolveToken(trimmed[..^1], potentialTokens), i);
            }

            potentialTokens = nextPotentialTokens;
        }

        if (potentialTokens.Count == 0)
        {
            return null;
        }

        return (ResolveToken(source.TrimStart(), potentialTokens), source.Length);
    }

    private static Token ResolveToken(ReadOnlySpan<char> source, IEnumerable<TokenType> tokenTypes)
    {
        foreach (var tokenType in tokenTypes)
        {
            if (tokenType == TokenType.Identifier)
            {
                continue;
            }

            if (TryResolveToken(source, tokenType, out var token))
            {
                return token.Value;
            }
        }

        return Token.Identifier(source.ToString());
    }
    
    private static bool TryResolveToken(ReadOnlySpan<char> source, TokenType type, [NotNullWhen(true)] out Token? token)
    {
        token = type switch
        {
            TokenType.Identifier => Token.Identifier(source.ToString()),
            TokenType.If when source is "if" => Token.If(),
            TokenType.LeftParenthesis when source is "(" => Token.LeftParenthesis(),
            TokenType.RightParenthesis when source is ")" => Token.RightParenthesis(),
            TokenType.Semicolon when source is ";" => Token.Semicolon(),
            TokenType.LeftBrace when source is "{" => Token.LeftBrace(),
            TokenType.RightBrace when source is "}" => Token.RightBrace(),
            TokenType.Pub when source is "pub" => Token.Pub(),
            TokenType.Fn when source is "fn" => Token.Fn(),
            TokenType.IntKeyword when source is "int" => Token.IntKeyword(),
            TokenType.Colon when source is ":" => Token.Colon(),
            TokenType.LeftAngleBracket when source is "<" => Token.LeftAngleBracket(),
            TokenType.RightAngleBracket when source is ">" => Token.RightAngleBracket(),
            TokenType.Var when source is "var" => Token.Var(),
            TokenType.Equals when source is "=" => Token.Equals(),
            TokenType.Comma when source is "," => Token.Comma(),
            TokenType.DoubleEquals when source is "==" => Token.DoubleEquals(),
            TokenType.Else when source is "else" => Token.Else(),
            TokenType.IntLiteral when int.TryParse(source, out var intValue) => Token.IntLiteral(intValue),
            TokenType.StringLiteral when source[0] == '"'
                                         && source[^1] == '"'
                                         && source[1..].IndexOf('"') == source.Length - 2 => Token.StringLiteral(source[1..^1].ToString()),
            TokenType.StringKeyword when source is "string" => Token.StringKeyword(),
            TokenType.Result when source is "result" => Token.Result(),
            TokenType.Ok when source is "ok" => Token.Ok(),
            TokenType.Error when source is "error" => Token.Error(),
            TokenType.QuestionMark when source is "?" => Token.QuestionMark(),
            TokenType.Return when source is "return" => Token.Return(),
            TokenType.True when source is "true" => Token.True(),
            TokenType.False when source is "false" => Token.False(),
            TokenType.Bool when source is "bool" => Token.Bool(),
            _ => null
        };

        return token.HasValue;
    }

    private static readonly SearchValues<char> AlphaNumeric =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFHIJKLMNOPQRSTUVWXYZ0123456789");

    private static bool IsPotentiallyValid(TokenType type, ReadOnlySpan<char> source)
    {
        return type switch
        {
            TokenType.Identifier => !source.ContainsAnyExcept(AlphaNumeric),
            TokenType.If => "if".AsSpan().StartsWith(source) && source.Length <= "if".Length,
            TokenType.LeftParenthesis => source is "(",
            TokenType.RightParenthesis => source is ")",
            TokenType.Semicolon => source is ";",
            TokenType.LeftBrace => source is "{",
            TokenType.RightBrace => source is "}",
            TokenType.Pub => "pub".AsSpan().StartsWith(source) && source.Length <= "pub".Length,
            TokenType.Fn => "fn".AsSpan().StartsWith(source) && source.Length <= "fn".Length,
            TokenType.IntKeyword => "int".AsSpan().StartsWith(source) && source.Length <= "int".Length,
            TokenType.Colon => source is ":",
            TokenType.LeftAngleBracket => source is "<",
            TokenType.RightAngleBracket => source is ">",
            TokenType.Var => "var".AsSpan().StartsWith(source) && source.Length <= "var".Length,
            TokenType.Equals => source is "=",
            TokenType.Comma => source is ",",
            TokenType.DoubleEquals => "==".AsSpan().StartsWith(source) && source.Length <= "==".Length,
            TokenType.Else => "else".AsSpan().StartsWith(source) && source.Length <= "else".Length,
            TokenType.IntLiteral => !source.ContainsAnyExcept(Digits),
            TokenType.StringLiteral => MatchesStringLiteral(source),
            TokenType.StringKeyword => "string".AsSpan().StartsWith(source) && source.Length <= "string".Length,
            TokenType.Result => "result".AsSpan().StartsWith(source) && source.Length <= "result".Length,
            TokenType.Ok => "ok".AsSpan().StartsWith(source) && source.Length <= "ok".Length,
            TokenType.Error => "error".AsSpan().StartsWith(source) && source.Length <= "error".Length,
            TokenType.QuestionMark => source is "?",
            TokenType.Return => "return".AsSpan().StartsWith(source) && source.Length <= "return".Length,
            TokenType.True => "true".AsSpan().StartsWith(source) && source.Length <= "true".Length,
            TokenType.False => "false".AsSpan().StartsWith(source) && source.Length <= "false".Length,
            TokenType.Bool => "bool".AsSpan().StartsWith(source) && source.Length <= "bool".Length,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        bool MatchesStringLiteral(ReadOnlySpan<char> stringSource)
        {
            if (stringSource[0] != '"')
            {
                return false;
            }

            // single quote mark will be the beginning of a string literal
            if (stringSource.Length == 1)
            {
                return true;
            }

            var rest = stringSource[1..];

            var nextQuoteIndex = rest.IndexOf('"');
            
            // the next quote either doesn't exist, or is at the end of the source
            return nextQuoteIndex < 0 || nextQuoteIndex == rest.Length - 1;
        }
    }
}