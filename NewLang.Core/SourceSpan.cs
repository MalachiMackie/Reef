namespace NewLang.Core;

public class SourceSpan(SourcePosition position, ushort length)
{
    public static SourceSpan Default() => new (new SourcePosition(0, 0, 0), 0);

    public SourcePosition Position = position;
    public ushort Length = length;
}

public class SourcePosition(uint start, ushort lineNumber, ushort linePosition)
{
    public uint Start = start;
    public ushort LineNumber = lineNumber;
    public ushort LinePosition = linePosition;
}