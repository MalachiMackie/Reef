namespace Reef.Core.IL;

public class ReefILModule
{
    public required IReadOnlyList<ReefILTypeDefinition> Types { get; set; }
    public required IReadOnlyList<ReefMethod> Methods { get; set; }
    public required ReefMethod? MainMethod { get; set; }
}
