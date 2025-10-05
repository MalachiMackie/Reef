using System.Diagnostics;

public record SourceSpan(SourcePosition Position, ushort Length)
{
    public static readonly SourceSpan Default = new(SourcePosition.Default, 0);
}

public record SourceRange(SourceSpan Start, SourceSpan End)
{
    public static readonly SourceRange Default = new(SourceSpan.Default, SourceSpan.Default);
}

public class SourcePosition(uint start, ushort lineNumber, ushort linePosition)
{
    public static readonly SourcePosition Default = new(0, 0, 0);

    public ushort LineNumber = lineNumber;
    public ushort LinePosition = linePosition;
    public uint Start = start;
}