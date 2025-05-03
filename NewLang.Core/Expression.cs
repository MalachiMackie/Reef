using System.Text;

namespace NewLang.Core;

public interface IExpression
{
    ExpressionType ExpressionType { get; }
}

public readonly record struct ValueAccessorExpression(ValueAccessor ValueAccessor) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.ValueAccess;

    public override string ToString()
    {
        return ValueAccessor.ToString();
    }
}

public readonly record struct UnaryOperatorExpression(UnaryOperator UnaryOperator) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.UnaryOperator;

    public override string ToString()
    {
        return UnaryOperator.ToString();
    }
}

public readonly record struct BinaryOperatorExpression(BinaryOperator BinaryOperator) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.BinaryOperator;

    public override string ToString()
    {
        return BinaryOperator.ToString();
    }
}

public readonly record struct VariableDeclarationExpression(VariableDeclaration VariableDeclaration) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.VariableDeclaration;

    public override string ToString()
    {
        return VariableDeclaration.ToString();
    }
}

// todo: better name
public readonly record struct IfExpressionExpression(IfExpression IfExpression) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.IfExpression;
    
    public override string ToString()
    {
        return IfExpression.ToString();
    }
}

public readonly record struct BlockExpression(Block Block) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Block;

    public override string ToString()
    {
        return Block.ToString();
    }
}

public readonly record struct MethodCallExpression(MethodCall MethodCall) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MethodCall;

    public override string ToString()
    {
        return MethodCall.ToString();
    }
}

public readonly record struct MethodReturnExpression(MethodReturn MethodReturn) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MethodReturn;

    public override string ToString()
    {
        return MethodReturn.ToString();
    }
}

public readonly record struct ObjectInitializerExpression(ObjectInitializer ObjectInitializer) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.ObjectInitializer;

    public override string ToString()
    {
        return ObjectInitializer.ToString();
    }
}

public readonly record struct GenericInstantiationExpression(GenericInstantiation GenericInstantiation) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.GenericInstantiation;

    public override string ToString()
    {
        return GenericInstantiation.ToString();
    }
}

public readonly record struct VariableDeclaration(
    Token VariableNameToken, MutabilityModifier? MutabilityModifier, TypeIdentifier? Type, IExpression? Value)
{
    public override string ToString()
    {
        var sb = new StringBuilder("var ");
        if (MutabilityModifier.HasValue)
        {
            sb.Append($"{MutabilityModifier.Value} ");
        }
        sb.Append($"{VariableNameToken}");
        if (Type.HasValue)
        {
            sb.Append($": {Type.Value} ");
        }
        if (Value is not null)
        {
            sb.Append($" = {Value}");
        }

        return sb.ToString();
    }
}

public readonly record struct MutabilityModifier(Token Modifier)
{
    public override string ToString()
    {
        return Modifier.ToString();
    }
}

public readonly record struct IfExpression(
    IExpression CheckExpression,
    IExpression Body,
    IReadOnlyCollection<ElseIf> ElseIfs,
    IExpression? ElseBody)
{
    public override string ToString()
    {
        var sb = new StringBuilder($"if({CheckExpression}) {Body}");
        foreach (var elseIf in ElseIfs)
        {
            sb.Append($" {elseIf}");
        }
        if (ElseBody is not null)
        {
            sb.Append($" else {ElseBody}");
        }

        return sb.ToString();
    }
}

public readonly record struct Block(IReadOnlyList<IExpression> Expressions, IReadOnlyList<LangFunction> Functions)
{
    public override string ToString()
    {
        var sb = new StringBuilder("{\n");
        foreach (var expression in Expressions)
        {
            sb.Append($"{expression};\n");
        }

        foreach (var function in Functions)
        {
            sb.Append($"{function}\n");
        }

        sb.Append('}');

        return sb.ToString();
    }
}


public readonly record struct ValueAccessor(ValueAccessType AccessType, Token Token)
{
    public override string ToString()
    {
        return Token.ToString();
    }
}

public readonly record struct UnaryOperator(UnaryOperatorType OperatorType, IExpression Operand, Token OperatorToken)
{
    public override string ToString()
    {
        // todo: prefix and postfix
        return $"|{Operand}{OperatorToken}|";
    }
}

public readonly record struct BinaryOperator(BinaryOperatorType OperatorType, IExpression Left, IExpression Right, Token OperatorToken)
{
    public override string ToString()
    {
        return $"|{Left} {OperatorToken} {Right}|";
    }
}

public readonly record struct ElseIf(IExpression CheckExpression, IExpression Body)
{
    public override string ToString()
    {
        return $"else if ({CheckExpression}) {Body}";
    }
}

public readonly record struct MethodCall(IExpression Method, IReadOnlyCollection<IExpression> ParameterList)
{
    public override string ToString()
    {
        return $"{Method}({string.Join(", ", ParameterList)})";
    }
}

public readonly record struct GenericInstantiation(
    IExpression GenericInstance,
    IReadOnlyList<TypeIdentifier> TypeArguments)
{
    public override string ToString()
    {
        return $"{GenericInstance}::<{string.Join(", ", TypeArguments)}>";
    }
}

public readonly record struct MethodReturn(IExpression Expression)
{
    public override string ToString()
    {
        return $"return {Expression};";
    }
}

public readonly record struct ObjectInitializer(TypeIdentifier Type, IReadOnlyList<FieldInitializer> FieldInitializers)
{
    public override string ToString()
    {
        var sb = new StringBuilder($"new {Type} {{\n");

        foreach (var fieldInitializer in FieldInitializers)
        {
            sb.AppendLine($"{fieldInitializer.FieldName} = {fieldInitializer.Value},");
        }
        sb.Append('}');

        return sb.ToString();
    }
}

public readonly record struct FieldInitializer(Token FieldName, IExpression Value);

public enum BinaryOperatorType
{
    LessThan,
    GreaterThan,
    Plus,
    Minus,
    Multiply,
    Divide,
    EqualityCheck,
    ValueAssignment,
    MemberAccess,
    StaticMemberAccess
}

public enum ValueAccessType
{
    Variable,
    Literal
}

public enum UnaryOperatorType
{
    // ?
    FallOut
}

public enum ExpressionType
{
    None,
    ValueAccess,
    UnaryOperator,
    BinaryOperator,
    VariableDeclaration,
    IfExpression,
    Block,
    MethodCall,
    MethodReturn,
    ObjectInitializer,
    GenericInstantiation,
}