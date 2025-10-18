namespace Reef.Core.IL;

public class ReefModule
{
    public required IReadOnlyList<ReefTypeDefinition> Types { get; set; }
    public required IReadOnlyList<ReefMethod> Methods { get; set; }
    public required ReefMethod? MainMethod { get; set; }
}
