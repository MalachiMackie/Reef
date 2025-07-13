namespace Reef.IL;

public class ReefMethod
{
    public required IReadOnlyList<IInstruction> Instructions { get; set; }
    public required IReadOnlyList<Local> Locals { get; set; }
    public required ReefType ReturnType { get; set; }
    public required IReadOnlyList<ReefType> TypeParameters { get; set; }

    public class Local
    {
        public required ReefType Type { get; set; }
        public required string DisplayName { get; set; }
    }
}