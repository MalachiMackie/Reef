using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.LoweredExpressions;

public class LoweredProgram
{
    public required IReadOnlyList<DataType> DataTypes { get; init; }
    public required IReadOnlyList<LoweredMethod> Methods { get; init; }
}

public interface ILoweredExpression
{
    ILoweredTypeReference ResolvedType { get; }
    bool ValueUseful { get; }
    bool Diverges { get; } 
}

public record SwitchIntExpression(
    ILoweredExpression Check,
    Dictionary<int, ILoweredExpression> Results,
    ILoweredExpression Otherwise,
    bool ValueUseful,
    ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => Results.Values.Append(Otherwise).All(x => x.Diverges);
}

public record CastBoolToIntExpression(
        ILoweredExpression BoolExpression,
        bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record CreateObjectExpression(
        LoweredConcreteTypeReference Type,
        string Variant,
        bool ValueUseful,
        Dictionary<string, ILoweredExpression> VariantFieldInitializers) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType => Type;
    public bool Diverges => false;
}

public record UnitConstantExpression(bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Unit.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntConstantExpression(bool ValueUseful, int Value) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record MethodReturnExpression(ILoweredExpression ReturnValue) : ILoweredExpression
{
    public bool ValueUseful => true;
    public ILoweredTypeReference ResolvedType => ReturnValue.ResolvedType;
    public bool Diverges => true;
}

public record StringConstantExpression(bool ValueUseful, string Value) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.String.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record MethodCallExpression(
        LoweredFunctionReference FunctionReference,
        IReadOnlyList<ILoweredExpression> Arguments,
        bool ValueUseful,
        ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record FieldAccessExpression(
    ILoweredExpression MemberOwner,
    string FieldName,
    string VariantName,
    bool ValueUseful,
    ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record StaticFieldAccessExpression(
    LoweredConcreteTypeReference OwnerType,
    string FieldName,
    bool ValueUseful,
    ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record LoadArgumentExpression(
    uint ArgumentIndex,
    bool ValueUseful,
    ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record VariableDeclarationAndAssignmentExpression(
        string LocalName,
        ILoweredExpression Value,
        bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } =  ClassSignature.Unit.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record VariableDeclarationExpression(
        string LocalName,
        bool ValueUseful) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Unit.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record LocalAssignmentExpression(
        string LocalName,
        ILoweredExpression Value,
        ILoweredTypeReference ResolvedType,
        bool ValueUseful) : ILoweredExpression
{
    public bool Diverges => false;
}

public record NoopExpression : ILoweredExpression
{
    public bool ValueUseful => false;
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Never.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record StaticFieldAssignmentExpression(
        LoweredConcreteTypeReference OwnerType,
        string FieldName,
        ILoweredExpression FieldValue,
        bool ValueUseful,
        ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record FieldAssignmentExpression(
        ILoweredExpression FieldOwnerExpression,
        string VariantName,
        string FieldName,
        ILoweredExpression FieldValue,
        bool ValueUseful,
        ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record BlockExpression(
    IReadOnlyList<ILoweredExpression> Expressions,
    ILoweredTypeReference ResolvedType,
    bool ValueUseful) : ILoweredExpression
{
    public bool Diverges => Expressions.Count > 0 && Expressions.Reverse().Any(x => x.Diverges);
}

public record IntPlusExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntMinusExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record UnreachableExpression : ILoweredExpression
{
    public bool ValueUseful => false;
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Never.ToLoweredTypeReference();
    public bool Diverges => true;
}

public record IntMultiplyExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntDivideExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Int.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntLessThanExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntGreaterThanExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntEqualsExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record IntNotEqualsExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record BoolAndExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record BoolOrExpression(bool ValueUseful, ILoweredExpression Left, ILoweredExpression Right) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record BoolNotExpression(bool ValueUseful, ILoweredExpression Operand) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record BoolConstantExpression(bool ValueUseful, bool Value) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType { get; } = ClassSignature.Boolean.ToLoweredTypeReference();
    public bool Diverges => false;
}

public record LocalVariableAccessor(string LocalName, bool ValueUseful, ILoweredTypeReference ResolvedType) : ILoweredExpression
{
    public bool Diverges => false;
}

public record LoweredMethod(
        Guid Id,
        string Name,
        IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
        IReadOnlyList<ILoweredTypeReference> Parameters,
        ILoweredTypeReference ReturnType,
        IReadOnlyList<ILoweredExpression> Expressions,
        List<MethodLocal> Locals);

public record MethodLocal(string Name, ILoweredTypeReference Type);

public record DataType(
        Guid Id,
        string Name,
        IReadOnlyList<LoweredGenericPlaceholder> TypeParameters,
        IReadOnlyList<DataTypeVariant> Variants,
        IReadOnlyList<StaticDataTypeField> StaticFields);


public record DataTypeVariant(string Name, IReadOnlyList<DataTypeField> Fields);

public record DataTypeField(string Name, ILoweredTypeReference Type);

public record StaticDataTypeField(string Name, ILoweredTypeReference Type, ILoweredExpression StaticInitializer);

public record FunctionReferenceConstantExpression(
        LoweredFunctionReference FunctionReference,
        bool ValueUseful,
        LoweredFunctionPointer FunctionPointer) : ILoweredExpression
{
    public ILoweredTypeReference ResolvedType => FunctionPointer;
    public bool Diverges => false;
}

public record LoweredFunctionReference(
        string Name,
        Guid DefinitionId,
        IReadOnlyList<ILoweredTypeReference> TypeArguments)
{}

public record LoweredFunctionPointer(
        IReadOnlyList<ILoweredTypeReference> ParameterTypes,
        ILoweredTypeReference ReturnType) : ILoweredTypeReference
{
}

public interface ILoweredTypeReference
{ }

public record LoweredConcreteTypeReference(string Name, Guid DefinitionId, IReadOnlyList<ILoweredTypeReference> TypeArguments) : ILoweredTypeReference;
public record LoweredGenericPlaceholder(Guid OwnerDefinitionId, string PlaceholderName) : ILoweredTypeReference;

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
