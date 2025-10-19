namespace Reef.Core.IL;

public class ReefMethod
{
    public required string DisplayName { get; set; }
    public required DefId Id { get; set; }
    public required InstructionList Instructions { get; set; }
    public required IReadOnlyList<IReefTypeReference> Parameters { get; set; }
    public required IReadOnlyList<Local> Locals { get; set; }
    public required IReefTypeReference ReturnType { get; set; }
    public required IReadOnlyList<string> TypeParameters { get; set; }
    public required bool Extern { get; set; }

    public class Local
    {
        public required IReefTypeReference Type { get; set; }
        public required string DisplayName { get; set; }
    }
}