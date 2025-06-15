using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

// todo: try pool allocations 
public class Tokenizer
{
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _stringsAlternateLookup = new HashSet<string>().GetAlternateLookup<ReadOnlySpan<char>>();

    public static IEnumerable<Token> Tokenize(string sourceStr)
    {
        var tokenizer = new Tokenizer();
        return tokenizer.TokenizeInner(sourceStr);
    }
    
    private IEnumerable<Token> TokenizeInner(string sourceStr)
    {
        if (sourceStr.Length == 0)
        {
            yield break;
        }

        var endIndex = sourceStr.Length - 1;
        var chars = sourceStr.AsSpan()[..(endIndex + 1)];
        var nextToken = EatToken(chars, new SourcePosition(0, 0, 0));
        while (nextToken is not null)
        {
            var token = nextToken;
            yield return token;

            var startIndex = (int)(token.SourceSpan.Position.Start + token.SourceSpan.Length);

            var nextSource = sourceStr.AsSpan()[startIndex..(endIndex + 1)];
            if (nextSource.IsEmpty)
            {
                break;
            }

            var previousTokensChars = sourceStr.AsSpan()[(int)token.SourceSpan.Position.Start..(startIndex - 1)];

            var lastNewLine = previousTokensChars.LastIndexOf('\n');
            var newLinesInToken = lastNewLine == -1 ? 0 : previousTokensChars.Count('\n');

            var newLinePosition = lastNewLine == -1
                ? (ushort)(token.SourceSpan.Position.LinePosition + token.SourceSpan.Length)
                : (ushort)(token.SourceSpan.Length - (lastNewLine + 1));
            
            nextToken = EatToken(nextSource, new SourcePosition(
                token.SourceSpan.Position.Start + token.SourceSpan.Length,
                (ushort)(token.SourceSpan.Position.LineNumber + newLinesInToken),
                newLinePosition));
        }
    }

    private readonly List<int> _notTokens = [];
    
    private Token? EatToken(
        ReadOnlySpan<char> source,
        SourcePosition startPosition)
    {
        var sourceTrimmed = source.TrimStart();
        if (sourceTrimmed.Length == 0)
        {
            return null;
        }

        Span<TokenType?> potentialTokens = stackalloc TokenType?[MaxPotentialTokenTypes];
        var potentialTokensCount = GetPotentiallyValidTokenTypes(sourceTrimmed[0], ref potentialTokens);
        if (potentialTokensCount == 0)
        {
            return null;
        }
        
        for (ushort i = 1; i < source.Length; i++)
        {
            var part = source[..(i + 1)];
            var trimmed = part.TrimStart();
            // 1 because first char is checked above
            if (trimmed.Length <= 1)
            {
                continue;
            }

            _notTokens.Clear();
            for (var j = 0; j < potentialTokens.Length; j++)
            {
                var type = potentialTokens[j];
                if (type.HasValue && !IsPotentiallyValid(type.Value, trimmed))
                {
                    _notTokens.Add(j);
                }
            }
            
            if (_notTokens.Count == potentialTokensCount)
            {
                // only calculate new position when we are going to resolve the token
                if (trimmed.Length != part.Length)
                {
                    var trimmedCharacters = part[..^trimmed.Length];
                    SetNextPosition(startPosition, trimmedCharacters);
                }
                
                // previously we had at least one potential token, now we have none. need to resolve to one.
                // this will happen in the case of `int)`
                var tokenSource = trimmed[..^1];
                return ResolveToken(tokenSource, potentialTokens, startPosition);
            }

            foreach (var index in _notTokens)
            {
                potentialTokensCount--;
                potentialTokens[index] = null;
            }
        }

        var allNull = true;
        foreach (var token in potentialTokens)
        {
            if (token.HasValue)
            {
                allNull = false;
                break;
            }
        }
        if (allNull)
        {
            return null;
        }

        var outerTrimmedChars = source[..^sourceTrimmed.Length];

        SetNextPosition(startPosition, outerTrimmedChars);
        
        return ResolveToken(sourceTrimmed, potentialTokens, startPosition);
    }

    private static void SetNextPosition(SourcePosition start, ReadOnlySpan<char> trimmedCharacters)
    {
        var outerLinePosition = start.LinePosition;
        var outerNewLines = 0;
        var lastNewLineIndex = -1;
        for (var i = 0; i < trimmedCharacters.Length; i++)
        {
            if (trimmedCharacters[i] == '\n')
            {
                outerNewLines++;
                lastNewLineIndex = i;
            }
        }
        if (outerNewLines > 0)
        {
            if (lastNewLineIndex == trimmedCharacters.Length - 1)
            {
                // the last trimmed character was a new line, so the start of `part` is the beginning of the line
                outerLinePosition = 0;
            }
            else
            {
                // we passed over a new line, but there are more whitespace characters after the new line
                outerLinePosition = (ushort)(trimmedCharacters.Length - (lastNewLineIndex + 1));
            }
        }
        else
        {
            // didn't pass over a new line, so just increment the line position by the number of whitespace chars we trimmed
            outerLinePosition += (ushort)trimmedCharacters.Length;
        }

        start.Start += (uint)trimmedCharacters.Length;
        start.LineNumber = (ushort)(start.LineNumber + outerNewLines);
        start.LinePosition = outerLinePosition;
    }

    private Token ResolveToken(
        ReadOnlySpan<char> source,
        Span<TokenType?> tokenTypes,
        SourcePosition position)
    {
        var allNull = true;
        foreach (var tokenType in tokenTypes)
        {
            if (tokenType.HasValue)
            {
                allNull = false;
            }
            else
            {
                continue;
            }
            
            // skip identifier so that keywords take precedent
            if (tokenType == TokenType.Identifier)
            {
                continue;
            }

            if (TryResolveToken(source, tokenType.Value, position, out var token))
            {
                return token;
            }
        }

        if (allNull)
        {
            throw new UnreachableException();
        }

        if (!TryResolveToken(source, TokenType.Identifier, position, out var identifierToken))
        {
            throw new InvalidOperationException($"invalid token \"{source}\"");
        }

        return identifierToken;
    }

    private string GetString(ReadOnlySpan<char> source)
    {
        if (_stringsAlternateLookup.TryGetValue(source, out var str))
        {
            return str;
        }
        
        _stringsAlternateLookup.Add(source);

        return source.ToString();
    }

    private bool TryResolveToken(
        ReadOnlySpan<char> source,
        TokenType type,
        SourcePosition position,
        [NotNullWhen(true)] out Token? token)
    {
        token = type switch
        {
            TokenType.Identifier when source.Length > 0 && !source.ContainsAnyExcept(ValidIdentifierTokens) && !char.IsDigit(source[0]) => 
                Token.Identifier(GetString(source), new SourceSpan(position, (ushort)source.Length)),
            TokenType.If when source is "if" => Token.If(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Mut when source is "mut" => Token.Mut(new SourceSpan(position, (ushort)source.Length)),
            TokenType.DoubleColon when source is "::" => Token.DoubleColon(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Class when source is "class" => Token.Class(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Union when source is "union" => Token.Union(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Matches when source is "matches" => Token.Matches(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Match when source is "match" => Token.Match(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Field when source is "field" => Token.Field(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Static when source is "static" => Token.Static(new SourceSpan(position, (ushort)source.Length)),
            TokenType.LeftParenthesis when source is "(" => Token.LeftParenthesis(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.RightParenthesis when source is ")" => Token.RightParenthesis(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.Semicolon when source is ";" => Token.Semicolon(new SourceSpan(position, (ushort)source.Length)),
            TokenType.LeftBrace when source is "{" => Token.LeftBrace(new SourceSpan(position, (ushort)source.Length)),
            TokenType.RightBrace when source is "}" => Token.RightBrace(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Pub when source is "pub" => Token.Pub(new SourceSpan(position, (ushort)source.Length)),
            TokenType.New when source is "new" => Token.New(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Fn when source is "fn" => Token.Fn(new SourceSpan(position, (ushort)source.Length)),
            TokenType.IntKeyword when source is "int" =>
                Token.IntKeyword(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Colon when source is ":" => Token.Colon(new SourceSpan(position, (ushort)source.Length)),
            TokenType.LeftAngleBracket when source is "<" => Token.LeftAngleBracket(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.RightAngleBracket when source is ">" => Token.RightAngleBracket(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.Var when source is "var" => Token.Var(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Equals when source is "=" => Token.Equals(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Comma when source is "," => Token.Comma(new SourceSpan(position, (ushort)source.Length)),
            TokenType.DoubleEquals when source is "==" => Token.DoubleEquals(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.Turbofish when source is "::<" => Token.Turbofish(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.Else when source is "else" => Token.Else(new SourceSpan(position, (ushort)source.Length)),
            TokenType.IntLiteral when int.TryParse(source, out var intValue) => Token.IntLiteral(intValue,
                new SourceSpan(position, (ushort)source.Length)),
            TokenType.StringLiteral when source[0] == '"'
                                         && source[^1] == '"'
                                         && source[1..].IndexOf('"') == source.Length - 2 => Token.StringLiteral(
                GetString(source[1..^1]), new SourceSpan(position, (ushort)source.Length)),
            TokenType.StringKeyword when source is "string" => Token.StringKeyword(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.Result when source is "result" => Token.Result(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Ok when source is "ok" => Token.Ok(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Error when source is "error" => Token.Error(new SourceSpan(position, (ushort)source.Length)),
            TokenType.QuestionMark when source is "?" => Token.QuestionMark(new SourceSpan(position,
                (ushort)source.Length)),
            TokenType.Return when source is "return" => Token.Return(new SourceSpan(position, (ushort)source.Length)),
            TokenType.True when source is "true" => Token.True(new SourceSpan(position, (ushort)source.Length)),
            TokenType.False when source is "false" => Token.False(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Bool when source is "bool" => Token.Bool(new SourceSpan(position, (ushort)source.Length)),
            TokenType.ForwardSlash when source is "/" => Token.ForwardSlash(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Star when source is "*" => Token.Star(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Plus when source is "+" => Token.Plus(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Dash when source is "-" => Token.Dash(new SourceSpan(position, (ushort)source.Length)),
            TokenType.Dot when source is "." => Token.Dot(new SourceSpan(position, (ushort)source.Length)),
            TokenType.SingleLineComment when source.StartsWith("//") => Token.SingleLineComment(source[2..].ToString(), new SourceSpan(position, (ushort)source.Length)),
            TokenType.MultiLineComment when source.StartsWith("/*") && source.EndsWith("*/") => Token.MultiLineComment(source[2..^2].ToString(), new SourceSpan(position, (ushort)source.Length)),
            TokenType.None => throw new UnreachableException(),
            _ => null
        };

        return token is not null;
    }

    private static readonly SearchValues<char> ValidIdentifierTokens =
        // ReSharper disable once StringLiteralTypo
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    // maximum potential token types at the same time
    private const int MaxPotentialTokenTypes = 4;
    private static int GetPotentiallyValidTokenTypes(char firstChar, ref Span<TokenType?> tokens)
    {
        var i = 0;
        switch (firstChar)
        {
            case 'i':
                tokens[i++] = TokenType.IntKeyword;
                tokens[i++] = TokenType.If;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'm':
                tokens[i++] = TokenType.Mut;
                tokens[i++] = TokenType.Matches;
                tokens[i++] = TokenType.Match;
                tokens[i++] = TokenType.Identifier;
                break;
            case '(':
                tokens[i++] = TokenType.LeftParenthesis;
                break;
            case ')':
                tokens[i++] = TokenType.RightParenthesis;
                break;
            case ';':
                tokens[i++] = TokenType.Semicolon;
                break;
            case '{':
                tokens[i++] = TokenType.LeftBrace;
                break;
            case '}':
                tokens[i++] = TokenType.RightBrace;
                break;
            case 'p':
                tokens[i++] = TokenType.Pub;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'f':
                tokens[i++] = TokenType.False;
                tokens[i++] = TokenType.Fn;
                tokens[i++] = TokenType.Field;
                tokens[i++] = TokenType.Identifier;
                break;
            case ':':
                tokens[i++] = TokenType.Colon;
                tokens[i++] = TokenType.Turbofish;
                tokens[i++] = TokenType.DoubleColon;
                break;
            case '<':
                tokens[i++] = TokenType.LeftAngleBracket;
                break;
            case '>':
                tokens[i++] = TokenType.RightAngleBracket;
                break;
            case 'n':
                tokens[i++] = TokenType.New;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'v':
                tokens[i++] = TokenType.Var;
                tokens[i++] = TokenType.Identifier;
                break;
            case '=':
                tokens[i++] = TokenType.Equals;
                tokens[i++] = TokenType.DoubleEquals;
                break;
            case ',':
                tokens[i++] = TokenType.Comma;
                break;
            case 'e':
                tokens[i++] = TokenType.Else;
                tokens[i++] = TokenType.Error;
                tokens[i++] = TokenType.Identifier;
                break;
            case 's':
                tokens[i++] = TokenType.StringKeyword;
                tokens[i++] = TokenType.Identifier;
                tokens[i++] = TokenType.Static;
                break;
            case 'r':
                tokens[i++] = TokenType.Return;
                tokens[i++] = TokenType.Result;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'b':
                tokens[i++] = TokenType.Bool;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'o':
                tokens[i++] = TokenType.Ok;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'u':
                tokens[i++] = TokenType.Union;
                break;
            case '?':
                tokens[i++] = TokenType.QuestionMark;
                break;
            case 't':
                tokens[i++] = TokenType.True;
                tokens[i++] = TokenType.Identifier;
                break;
            case '*':
                tokens[i++] = TokenType.Star;
                break;
            case '/':
                tokens[i++] = TokenType.ForwardSlash;
                tokens[i++] = TokenType.SingleLineComment;
                tokens[i++] = TokenType.MultiLineComment;
                break;
            case '+':
                tokens[i++] = TokenType.Plus;
                break;
            case '-':
                tokens[i++] = TokenType.Dash;
                break;
            case 'c':
                tokens[i++] = TokenType.Class;
                tokens[i++] = TokenType.Identifier;
                break;
            case '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9':
                tokens[i++] = TokenType.IntLiteral;
                break;
            case '"':
                tokens[i++] = TokenType.StringLiteral;
                break;
            case '.':
                tokens[i++] = TokenType.Dot;
                break;
            default:
            {
                if (ValidIdentifierTokens.Contains(firstChar))
                {
                    tokens[i++] = TokenType.Identifier;
                }

                break;
            }
        }
        
        return i;
    }
    
    private static bool IsPotentiallyValid(TokenType type, ReadOnlySpan<char> source)
    {
        return type switch
        {
            TokenType.Identifier => !source.ContainsAnyExcept(ValidIdentifierTokens) && !char.IsDigit(source[0]),
            TokenType.If => Matches(source, "if"),
            TokenType.LeftParenthesis => Matches(source, "("),
            TokenType.RightParenthesis => Matches(source,")"),
            TokenType.Semicolon => Matches(source, ";"),
            TokenType.LeftBrace => Matches(source, "{"),
            TokenType.Union => Matches(source, "union"),
            TokenType.Mut => Matches(source, "mut"),
            TokenType.Match => Matches(source, "match"),
            TokenType.Matches => Matches(source, "matches"),
            TokenType.New => Matches(source, "new"),
            TokenType.Static => Matches(source, "static"),
            TokenType.Class => Matches(source, "class"),
            TokenType.RightBrace => source is "}",
            TokenType.Pub => Matches(source, "pub"),
            TokenType.Fn => Matches(source, "fn"),
            TokenType.Field => Matches(source, "field"),
            TokenType.IntKeyword => Matches(source, "int"),
            TokenType.Turbofish => Matches(source, "::<"),
            TokenType.Colon => Matches(source, ":"),
            TokenType.LeftAngleBracket => Matches(source, "<"),
            TokenType.RightAngleBracket => Matches(source, ">"),
            TokenType.Var => Matches(source, "var"),
            TokenType.Equals => Matches(source, "="),
            TokenType.Comma => Matches(source ,","),
            TokenType.DoubleEquals => Matches(source, "=="),
            TokenType.Else => Matches(source, "else"),
            TokenType.IntLiteral => !source.ContainsAnyExcept(Digits),
            TokenType.StringLiteral => MatchesStringLiteral(source),
            TokenType.StringKeyword => Matches(source, "string"),
            TokenType.Result => Matches(source, "result"),
            TokenType.Ok => Matches(source, "ok"),
            TokenType.Error => Matches(source, "error"),
            TokenType.QuestionMark => source is "?",
            TokenType.Return => Matches(source, "return"),
            TokenType.True => Matches(source, "true"),
            TokenType.False => Matches(source, "false"),
            TokenType.Bool => Matches(source, "bool"),
            TokenType.DoubleColon => Matches(source, "::"),
            TokenType.Star => Matches(source, "*"),
            TokenType.ForwardSlash => Matches(source, "/"),
            TokenType.Plus => Matches(source, "+"),
            TokenType.Dash => Matches(source, "-"),
            TokenType.Dot => Matches(source, "."),
            TokenType.SingleLineComment => source.StartsWith("//") && !source.EndsWith('\r'),
            TokenType.MultiLineComment => source.StartsWith("/*") && (source.EndsWith("*/") || !source.Contains("*/", StringComparison.Ordinal)),
            TokenType.None => throw new UnreachableException(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        static bool MatchesStringLiteral(ReadOnlySpan<char> stringSource)
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

        static bool Matches(ReadOnlySpan<char> source, ReadOnlySpan<char> expected)
            => expected.StartsWith(source) && source.Length <= expected.Length;
    }
}