namespace Reef.Core.LoweredExpressions;

public interface IStatement;

public interface IOperand;

public interface IRValue;

public interface ITerminator;

public record BasicBlockId(string Id)
{
    public string Id { get; set; } = Id;
}

public record BasicBlock(BasicBlockId Id, IReadOnlyList<IStatement> Statements, ITerminator? Terminator = null)
{
    public ITerminator? Terminator { get; set; } = Terminator;
}

public record SwitchInt(
    IOperand Operand,
    Dictionary<int, BasicBlockId> Cases,
    BasicBlockId Otherwise) : ITerminator;

public record MethodCall(LoweredFunctionReference Function, IReadOnlyList<IOperand> Arguments, IPlace PlaceDestination, BasicBlockId GoToAfter) : ITerminator;

public record Return : ITerminator;

public record GoTo(BasicBlockId BasicBlockId) : ITerminator;

public record LocalAlive(string Local) : IStatement;
public record LocalDead(string Local) : IStatement;

public record Assign(IPlace Place, IRValue RValue) : IStatement;

public interface IPlace;

public record Local(string LocalName) : IPlace;

public record Field(IPlace FieldOwner, string FieldName, string VariantName) : IPlace;

public record StaticField(LoweredConcreteTypeReference Type, string FieldName) : IPlace;

public record BinaryOperation(IOperand LeftOperand, IOperand RightOperand, BinaryOperationKind Kind) : IRValue;

public record UnaryOperation(IOperand Operand, UnaryOperationKind Kind) : IRValue;

public record Use(IOperand Operand) : IRValue;

public record CreateObject(LoweredConcreteTypeReference Type) : IRValue;

public record Copy(IPlace Place) : IOperand;

public record StringConstant(string Value) : IOperand;

public record IntConstant(long Value, byte ByteSize) : IOperand;

public record UIntConstant(ulong Value, byte ByteSize) : IOperand;

public record BoolConstant(bool Value) : IOperand;

public record FunctionPointerConstant(LoweredFunctionReference Value) : IOperand;

public record UnitConstant : IOperand;

public enum BinaryOperationKind
{
    Add,
    Subtract,
    Multiply,
    Divide,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Equal,
    NotEqual
}

public enum UnaryOperationKind
{
    Not,
    Negate
}

public interface IMethod
{
    DefId Id { get; }
    string Name { get; }
    IReadOnlyList<LoweredGenericPlaceholder> TypeParameters { get; }
    MethodLocal ReturnValue { get; }
    List<MethodLocal> ParameterLocals { get; }
}

public record LoweredMethod(
    DefId Id,
    string Name,
    IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
    IReadOnlyList<BasicBlock> BasicBlocks,
    MethodLocal ReturnValue,
    List<MethodLocal> ParameterLocals,
    List<MethodLocal> Locals) : IMethod;

public record LoweredExternMethod(
    DefId Id,
    string Name,
    IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
    MethodLocal ReturnValue,
    List<MethodLocal> ParameterLocals) : IMethod;

public record MethodLocal(string CompilerGivenName, string? UserGivenName, ILoweredTypeReference Type);

public record LoweredFunctionReference(
    DefId DefinitionId,
    IReadOnlyList<ILoweredTypeReference> TypeArguments) : ILoweredTypeReference;

public interface ILoweredTypeReference;

public record LoweredConcreteTypeReference(string Name, DefId DefinitionId, IReadOnlyList<ILoweredTypeReference> TypeArguments) : ILoweredTypeReference;
public record LoweredGenericPlaceholder(DefId OwnerDefinitionId, string PlaceholderName) : ILoweredTypeReference;

public record DataType(
    DefId Id,
    string Name,
    IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
    IReadOnlyList<DataTypeVariant> Variants,
    IReadOnlyList<StaticDataTypeField> StaticFields);


public record DataTypeVariant(string Name, IReadOnlyList<DataTypeField> Fields);

public record DataTypeField(string Name, ILoweredTypeReference Type);

public record StaticDataTypeField(
    string Name,
    ILoweredTypeReference Type,
    IReadOnlyList<BasicBlock> InitializerBasicBlocks,
    IReadOnlyList<MethodLocal> InitializerLocals,
    MethodLocal ReturnValueLocal);


public class LoweredModule
{
    public required IReadOnlyList<DataType> DataTypes { get; init; }
    public required IReadOnlyList<IMethod> Methods { get; init; }
}
