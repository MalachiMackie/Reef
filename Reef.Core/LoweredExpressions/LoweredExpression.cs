using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.LoweredExpressions;

public class LoweredProgram
{
    public required IReadOnlyList<DataType> DataTypes { get; init; }
    public required IReadOnlyList<IMethod> Methods { get; init; }
}

public interface ILoweredExpression
{
    ILoweredTypeReference ResolvedType { get; }
    bool ValueUseful { get; }
}

public record UnitConstantExpression(bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Unit.ToLoweredTypeReference();
}

public record IntConstantExpression(bool ValueUseful, int Value) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
}

public record MethodReturnExpression(ILoweredExpression ReturnValue) : ILoweredExpression
{
    public bool ValueUseful => true;
    public ILoweredTypeReference ResolvedType => ReturnValue.ResolvedType;
}

public record StringConstantExpression(bool ValueUseful, string Value) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.String.ToLoweredTypeReference();
}

public record VariableDeclarationAndAssignmentExpression(
        string LocalName,
        ILoweredExpression Value,
        bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } =  ClassSignature.Unit.ToLoweredTypeReference();
}

public record VariableDeclarationExpression(
        string LocalName,
        bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Unit.ToLoweredTypeReference();
}

public record LocalAssignmentExpression(
        string LocalName,
        ILoweredExpression Value,
        ILoweredTypeReference ResolvedType,
        bool ValueUseful) : ILoweredExpression
{
}

public record BlockExpression(
        IReadOnlyList<ILoweredExpression> Expressions,
        ILoweredTypeReference ResolvedType,
        bool ValueUseful) : ILoweredExpression;

public record IntPlusExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
}

public record IntMinusExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
}

public record IntMultiplyExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
}

public record IntDivideExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
}

public record IntLessThanExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record IntGreaterThanExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record IntEqualsExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record BoolAndExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record BoolOrExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record BoolNotExpression(bool ValueUseful, ILoweredExpression Operand) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record BoolConstantExpression(bool ValueUseful, bool Value) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
}

public record LocalVariableAccessor(bool ValueUseful, ILoweredTypeReference ResolvedType) : ILoweredExpression
{
}

public record LoweredMethod(
        Guid Id,
        string Name,
        IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
        IReadOnlyList<ILoweredTypeReference> Parameters,
        ILoweredTypeReference ReturnType,
        IReadOnlyList<ILoweredExpression> Expressions,
        IReadOnlyList<MethodLocal> Locals) : IMethod;

public record MethodLocal(string Name, ILoweredTypeReference Type);

public record DataType(
        Guid Id,
        string Name,
        IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
        IReadOnlyList<DataTypeVariant> Variants,
        IReadOnlyList<StaticDataTypeField> StaticFields);

public interface IMethod;

public record CompilerImplementedMethod(
        Guid Id,
        string Name,
        IReadOnlyList<ILoweredTypeReference> Parameters,
        ILoweredTypeReference ReturnType,
        CompilerImplementationType CompilerImplementationType) : IMethod;

public enum CompilerImplementationType
{
    UnionTupleVariantInit
}

public record DataTypeVariant(string Name, IReadOnlyList<DataTypeField> Fields);

public record DataTypeField(string Name, ILoweredTypeReference Type);

public record StaticDataTypeField(string Name, ILoweredTypeReference Type, ILoweredExpression StaticInitializer);

public record DataTypeStaticField(ILoweredTypeReference Type);

public record StaticFieldAccess(
        bool ValueUseful,
        ILoweredTypeReference ResolvedType,
        ITypeSignature MemberOwner,
        uint StaticFieldIndex) : ILoweredExpression
{
}

public record StaticMethodAccess(
        bool ValueUseful,
        ILoweredTypeReference ResolvedType,
        ITypeSignature MemberOwner,
        InstantiatedFunction InstantiatedFunction) : ILoweredExpression
{
}

public record StaticMemberAccessor(
        bool ValueUseful,
        ILoweredTypeReference ResolvedType,
        ITypeSignature MemberOwner,
        uint MemberIndex) : ILoweredExpression
{
}

public interface ILoweredTypeReference
{ }

public record LoweredConcreteTypeReference(string Name, Guid DefinitionId, IReadOnlyList<ILoweredTypeReference> TypeArguments) : ILoweredTypeReference;
public record LoweredGenericPlaceholder(Guid OwnerDefinitionId, string placeholderName) : ILoweredTypeReference;

file static class SignatureExtensionMethods
{
    public static LoweredConcreteTypeReference ToLoweredTypeReference(this ClassSignature signature)
    {
        return new LoweredConcreteTypeReference(
                    signature.Name,
                    signature.Id,
                    []);
    }
}
