﻿namespace Reef.IL;

public class ReefMethod
{
    public required string DisplayName { get; set; }
    public required bool IsStatic { get; set; }
    public required IReadOnlyList<IInstruction> Instructions { get; set; }
    public required IReadOnlyList<Parameter> Parameters { get; set; }
    public required IReadOnlyList<Local> Locals { get; set; }
    public required IReefTypeReference ReturnType { get; set; }
    public required IReadOnlyList<string> TypeParameters { get; set; }

    public class Local
    {
        public required IReefTypeReference Type { get; set; }
        public required string DisplayName { get; set; }
    }

    public class Parameter
    {
        public required IReefTypeReference Type { get; set; }
        public required string DisplayName { get; set; }
    }
}