namespace Reef.Core.LoweredExpressions;

public interface IStatement;

public interface IOperand
{
}

public interface IRValue
{
}

public record Local(string Name, ILoweredTypeReference Type) : IRValue;

public record FieldAccess(string LocalName, string FieldName, string VariantName) : IRValue;

public interface ITerminator;

public record BasicBlockId(string Id);
public record BasicBlock(BasicBlockId Id, IReadOnlyList<IStatement> Statements, ITerminator? Terminator);

public record SwitchInt(
    IOperand Operand,
    Dictionary<int, BasicBlockId> Cases,
    BasicBlockId Otherwise) : ITerminator;

public record MethodCall(LoweredFunctionReference Function, IReadOnlyList<IOperand> Arguments) : ITerminator;

public record Return : ITerminator;

public record LocalAlive(string Local) : IStatement;
public record LocalDead(string Local) : IStatement;
public record Assign(string Local, IRValue RValue) : IStatement;

public record BinaryOperation(IOperand LeftOperand, IOperand RightOperand, BinaryOperationKind Kind) : IRValue;

public record UnaryOperation(IOperand LeftOperand, UnaryOperationKind Kind) : IRValue;

public record Use(IOperand Operand) : IRValue;

public record StringConstant(string Value) : IOperand;

public record IntConstant(long Value, byte ByteSize) : IOperand;

public record UnitConstant : IOperand;

public record UIntConstant(ulong Value, byte ByteSize) : IOperand;

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

public record NewLoweredMethod(
    DefId Id,
    string Name,
    IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
    ILoweredTypeReference ReturnType,
    IReadOnlyList<BasicBlock> BasicBlocks,
    MethodLocal ReturnValue,
    List<MethodLocal> ParameterLocals,
    List<MethodLocal> Locals);

