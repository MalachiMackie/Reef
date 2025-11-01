using Reef.Core.TypeChecking;

namespace Reef.Core.LoweredExpressions.New;

public interface IStatement;

public interface IOperand
{
}

public interface IRValue
{
}

public record Local(string Name, INewLoweredTypeReference Type) : IRValue;

public record FieldAccess(string LocalName, string FieldName, string VariantName) : IRValue;

public interface ITerminator;

public record BasicBlockId(string Id);
public record BasicBlock(BasicBlockId Id, IReadOnlyList<IStatement> Statements, ITerminator? Terminator);

public record SwitchInt(
    IOperand Operand,
    Dictionary<int, BasicBlockId> Cases,
    BasicBlockId Otherwise) : ITerminator;

public record MethodCall(NewLoweredFunctionReference Function, IReadOnlyList<IOperand> Arguments) : ITerminator;

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
    IReadOnlyList<NewLoweredGenericPlaceholder> TypeParameters,
    IReadOnlyList<BasicBlock> BasicBlocks,
    NewMethodLocal ReturnValue,
    List<NewMethodLocal> ParameterLocals,
    List<NewMethodLocal> Locals);


public record NewMethodLocal(string CompilerGivenName, string? UserGivenName, INewLoweredTypeReference Type);

public record NewLoweredFunctionReference(
    string Name,
    DefId DefinitionId,
    IReadOnlyList<INewLoweredTypeReference> TypeArguments)
{}

public record NewLoweredFunctionPointer(
    IReadOnlyList<INewLoweredTypeReference> ParameterTypes,
    INewLoweredTypeReference ReturnType) : INewLoweredTypeReference
{
}

public interface INewLoweredTypeReference
{ }

public record NewLoweredConcreteTypeReference(string Name, DefId DefinitionId, IReadOnlyList<INewLoweredTypeReference> TypeArguments) : INewLoweredTypeReference;
public record NewLoweredGenericPlaceholder(DefId OwnerDefinitionId, string PlaceholderName) : INewLoweredTypeReference;

file static class SignatureExtensionMethods
{
    public static NewLoweredConcreteTypeReference ToLoweredTypeReference(this TypeChecker.ClassSignature signature)
    {
        return new NewLoweredConcreteTypeReference(
            signature.Name,
            signature.Id,
            []);
    }
}

public record NewDataType(
    DefId Id,
    string Name,
    IReadOnlyList<NewLoweredGenericPlaceholder> TypeParameters,
    IReadOnlyList<NewDataTypeVariant> Variants,
    IReadOnlyList<NewStaticDataTypeField> StaticFields);


public record NewDataTypeVariant(string Name, IReadOnlyList<NewDataTypeField> Fields);

public record NewDataTypeField(string Name, INewLoweredTypeReference Type);

public record NewStaticDataTypeField(string Name, INewLoweredTypeReference Type, IReadOnlyList<BasicBlock> InitializerBasicBlocks, IReadOnlyList<MethodLocal> InitializerLocals);


public class NewLoweredProgram
{
    public required IReadOnlyList<NewDataType> DataTypes { get; init; }
    public required IReadOnlyList<NewLoweredMethod> Methods { get; init; }
}
