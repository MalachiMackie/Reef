namespace NewLang.Core;

public record SourceSpan(SourcePosition Position, ushort Length)
{
    public static readonly SourceSpan Default = new(new SourcePosition(0, 0, 0), 0);
}

public class SourcePosition(uint start, ushort lineNumber, ushort linePosition)
{
    public ushort LineNumber = lineNumber;
    public ushort LinePosition = linePosition;
    public uint Start = start;
}