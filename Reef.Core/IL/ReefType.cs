namespace Reef.Core.IL;

public class ReefILTypeDefinition
{
    public required DefId Id { get; set; }
    public required string DisplayName { get; set; }
    public required IReadOnlyList<ReefVariant> Variants { get; set; }
    public required IReadOnlyList<StaticReefField> StaticFields { get; set; }
    public required bool IsValueType { get; set; }
    public required IReadOnlyList<string> TypeParameters { get; set; }

    public uint StackSize
    {
        get
        {
            if (Id == DefId.Int64 || Id == DefId.UInt64 || Id == DefId.String)
            {
                return 8;
            }
            if (Id == DefId.Int32 || Id == DefId.UInt32)
            {
                return 4;
            }
            if (Id == DefId.Int16 || Id == DefId.UInt16)
            {
                return 2;
            }
            if (Id == DefId.Int8 || Id == DefId.UInt8 || Id == DefId.Boolean)
            {
                return 1;
            }

            // assume it's a pointer
            return 8;
        }
    }
}

public class FunctionDefinitionReference
{
    public required string Name { get; set; }
    public required DefId DefinitionId { get; set; }
    public required IReadOnlyList<IReefTypeReference> TypeArguments { get; set; }
}

public interface IReefTypeReference;

public class ConcreteReefTypeReference : IReefTypeReference
{
    public required string Name { get; set; }
    public required DefId DefinitionId { get; set; }
    public required IReadOnlyList<IReefTypeReference> TypeArguments { get; set; }
}

public class GenericReefTypeReference : IReefTypeReference
{
    public required DefId DefinitionId { get; set; }
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