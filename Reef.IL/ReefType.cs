namespace Reef.IL;

public class ReefTypeDefinition
{
    public required Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required IReadOnlyList<ReefVariant> Variants { get; set; }
    public required IReadOnlyList<StaticReefField> StaticFields { get; set; }
    public required bool IsValueType { get; set; }
    public required IReadOnlyList<string> TypeParameters { get; set; }
}

public class FunctionDefinitionReference
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

public class FunctionPointerReefType : IReefTypeReference
{
    public required IReadOnlyList<IReefTypeReference> Parameters { get; set; }
    public required IReefTypeReference ReturnType { get; set; }
}

public class ReefVariant
{
    public required string DisplayName { get; set; }
    public required IReadOnlyList<ReefField> Fields { get; set; }
}

public class ReefField
{
    public required string DisplayName { get; set; }
    public required IReefTypeReference Type { get; set; }
}

public class StaticReefField
{
    public required string DisplayName { get; set; }
    public required IReefTypeReference Type { get; set; }
    public required InstructionList StaticInitializerInstructions { get; set; }
}