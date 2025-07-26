namespace Reef.IL;

public class ReefTypeDefinition
{
    public required Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required IReadOnlyList<ReefVariant> Variants { get; set; }
    public required IReadOnlyList<ReefMethod> Methods { get; set; }
    public required bool IsValueType { get; set; } 
    public required IReadOnlyList<string> TypeParameters { get; set; }
}

public class FunctionReference
{
    public required string Name { get; set; }
    public required Guid DefinitionId { get; set; }
    public required IReadOnlyList<IReefTypeReference> TypeArguments { get; set; }
}

public interface IReefTypeReference;

public class ConcreteReefTypeReference : IReefTypeReference
{
    public required string Name { get; set; }
    public required Guid DefinitionId { get; set; }
    public required IReadOnlyList<IReefTypeReference> TypeArguments { get; set; }
}

public class GenericReefTypeReference : IReefTypeReference
{
    public required Guid DefinitionId { get; set; }
    public required string TypeParameterName { get; set; }
}

public class ReefVariant
{
    public required string DisplayName { get; set; }
    public required IReadOnlyList<ReefField> InstanceFields { get; set; }
    public required IReadOnlyList<ReefField> StaticFields { get; set; }
}

public class ReefField
{
    public required string DisplayName { get; set; }
    public required IReefTypeReference Type { get; set; }
    public required bool IsStatic { get; set; }
    public required bool IsPublic { get; set; }
    public required IReadOnlyList<IInstruction> StaticInitializerInstructions { get; set; }
}