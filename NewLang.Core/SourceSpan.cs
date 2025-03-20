namespace NewLang.Core;

public readonly record struct SourceSpan(SourcePosition Position, uint Length);
public readonly record struct SourcePosition(uint Start, uint LineNumber, uint LinePosition);