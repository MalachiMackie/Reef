using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public struct Token
{
    public required TokenType Type;
    
    // todo: can we 'overlap' these fields like an rust enum would?

    public string? StringValue;
    public int? IntValue;
    
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
            _ => null
        };

        return token.HasValue;
    }

    private static readonly SearchValues<char> AlphaNumeric =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFHIJKLMNOPQRSTUVWXYZ0123456789");

    private static bool IsPotentiallyValid(TokenType type, ReadOnlySpan<char> source)
    {
        switch (type)
        {
            case TokenType.Identifier:
                return !source.ContainsAnyExcept(AlphaNumeric);
            case TokenType.If:
                return "if".AsSpan().StartsWith(source) && source.Length <= "if".Length;
            case TokenType.LeftParenthesis:
                return source is "(";
            case TokenType.RightParenthesis:
                return source is ")";
            case TokenType.Semicolon:
                return source is ";";
            case TokenType.LeftBrace:
                return source is "{";
            case TokenType.RightBrace:
                return source is "}";
            case TokenType.Pub:
                return "pub".AsSpan().StartsWith(source) && source.Length <= "pub".Length;
            case TokenType.Fn:
                return "fn".AsSpan().StartsWith(source) && source.Length <= "fn".Length;
            case TokenType.IntKeyword:
                return "int".AsSpan().StartsWith(source) && source.Length <= "int".Length;
            case TokenType.Colon:
                return source is ":";
            case TokenType.LeftAngleBracket:
                return source is "<";
            case TokenType.RightAngleBracket:
                return source is ">";
            case TokenType.Var:
                return "var".AsSpan().StartsWith(source) && source.Length <= "var".Length;
            case TokenType.Equals:
                return source is "=";
            case TokenType.Comma:
                return source is ",";
            case TokenType.DoubleEquals:
                return "==".AsSpan().StartsWith(source) && source.Length <= "==".Length;
            case TokenType.Else:
                return "else".AsSpan().StartsWith(source) && source.Length <= "else".Length;
            case TokenType.IntLiteral:
                return !source.ContainsAnyExcept(Digits);
            case TokenType.StringLiteral:
            {
                if (source[0] != '"')
                {
                    return false;
                }

                // single quote mark will be the beginning of a string literal
                if (source.Length == 1)
                {
                    return true;
                }

                var rest = source[1..];

                var nextQuoteIndex = rest.IndexOf('"');
                
                // the next quote either doesn't exist, or is at the end of the source
                return nextQuoteIndex < 0 || nextQuoteIndex == rest.Length - 1;
            }
            case TokenType.StringKeyword:
                return "string".AsSpan().StartsWith(source) && source.Length <= "string".Length;
            case TokenType.Result:
                return "result".AsSpan().StartsWith(source) && source.Length <= "result".Length;
            case TokenType.Ok:
                return "ok".AsSpan().StartsWith(source) && source.Length <= "ok".Length;
            case TokenType.Error:
                return "error".AsSpan().StartsWith(source) && source.Length <= "error".Length;
            case TokenType.QuestionMark:
                return source is "?";
            case TokenType.Return:
                return "return".AsSpan().StartsWith(source) && source.Length <= "return".Length;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}