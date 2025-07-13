namespace Reef.IL;

public class ReefType
{
    public required string DisplayName { get; set; }
    
    public required IReadOnlyList<ReefVariant> Variants { get; set; }
    
    public required IReadOnlyList<ReefMethod> Methods { get; set; }
    
    public required uint SizeBytes { get; set; }
    
    public required bool IsValueType { get; set; } 
}

public class ReefVariant
{
    public required string DisplayName { get; set; }
    
    public required IReadOnlyList<ReefField> Fields { get; set; }
}

public class ReefField
{
    public required string DisplayName { get; set; }
    public required ReefType Type { get; set; }
}