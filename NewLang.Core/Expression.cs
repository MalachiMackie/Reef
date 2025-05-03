using System.Text;

namespace NewLang.Core;

public interface IExpression
{
    ExpressionType ExpressionType { get; }
}

public record ValueAccessorExpression(ValueAccessor ValueAccessor) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.ValueAccess;

    public override string ToString()
    {
        return ValueAccessor.ToString();
    }
}

public record UnaryOperatorExpression(UnaryOperator UnaryOperator) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.UnaryOperator;

    public override string ToString()
    {
        return UnaryOperator.ToString();
    }
}

public record BinaryOperatorExpression(BinaryOperator BinaryOperator) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.BinaryOperator;

    public override string ToString()
    {
        return BinaryOperator.ToString();
    }
}

public record VariableDeclarationExpression(VariableDeclaration VariableDeclaration) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.VariableDeclaration;

    public override string ToString()
    {
        return VariableDeclaration.ToString();
    }
}

// todo: better name
public record IfExpressionExpression(IfExpression IfExpression) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.IfExpression;
    
    public override string ToString()
    {
        return IfExpression.ToString();
    }
}

public record BlockExpression(Block Block) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Block;

    public override string ToString()
    {
        return Block.ToString();
    }
}

public record MethodCallExpression(MethodCall MethodCall) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MethodCall;

    public override string ToString()
    {
        return MethodCall.ToString();
    }
}

public record MethodReturnExpression(MethodReturn MethodReturn) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MethodReturn;

    public override string ToString()
    {
        return MethodReturn.ToString();
    }
}

public record ObjectInitializerExpression(ObjectInitializer ObjectInitializer) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.ObjectInitializer;

    public override string ToString()
    {
        return ObjectInitializer.ToString();
    }
}

public record GenericInstantiationExpression(GenericInstantiation GenericInstantiation) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.GenericInstantiation;

    public override string ToString()
    {
        return GenericInstantiation.ToString();
    }
}

public record VariableDeclaration(
    Token VariableNameToken, MutabilityModifier? MutabilityModifier, TypeIdentifier? Type, IExpression? Value)
{
    public override string ToString()
    {
        var sb = new StringBuilder("var ");
        if (MutabilityModifier is not null)
        {
            sb.Append($"{MutabilityModifier} ");
        }
        sb.Append($"{VariableNameToken}");
        if (Type is not null)
        {
            sb.Append($": {Type} ");
        }
        if (Value is not null)
        {
            sb.Append($" = {Value}");
        }

        return sb.ToString();
    }
}

public record MutabilityModifier(Token Modifier)
{
    public override string ToString()
    {
        return Modifier.ToString();
    }
}

public record IfExpression(
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

public record Block(IReadOnlyList<IExpression> Expressions, IReadOnlyList<LangFunction> Functions)
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


public record ValueAccessor(ValueAccessType AccessType, Token Token)
{
    public override string ToString()
    {
        return Token.ToString();
    }
}

public record UnaryOperator(UnaryOperatorType OperatorType, IExpression Operand, Token OperatorToken)
{
    public override string ToString()
    {
        // todo: prefix and postfix
        return $"|{Operand}{OperatorToken}|";
    }
}

public record BinaryOperator(BinaryOperatorType OperatorType, IExpression Left, IExpression Right, Token OperatorToken)
{
    public override string ToString()
    {
        return $"|{Left} {OperatorToken} {Right}|";
    }
}

public record ElseIf(IExpression CheckExpression, IExpression Body)
{
    public override string ToString()
    {
        return $"else if ({CheckExpression}) {Body}";
    }
}

public record MethodCall(IExpression Method, IReadOnlyCollection<IExpression> ParameterList)
{
    public override string ToString()
    {
        return $"{Method}({string.Join(", ", ParameterList)})";
    }
}

public record GenericInstantiation(
    IExpression GenericInstance,
    IReadOnlyList<TypeIdentifier> TypeArguments)
{
    public override string ToString()
    {
        return $"{GenericInstance}::<{string.Join(", ", TypeArguments)}>";
    }
}

public record MethodReturn(IExpression Expression)
{
    public override string ToString()
    {
        return $"return {Expression};";
    }
}

public record ObjectInitializer(TypeIdentifier Type, IReadOnlyList<FieldInitializer> FieldInitializers)
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

public record FieldInitializer(Token FieldName, IExpression Value);

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