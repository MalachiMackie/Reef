using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace NewLang.Core;

public readonly record struct Expression(
    ExpressionType ExpressionType,
    ValueAccessor? ValueAccessor,
    StrongBox<UnaryOperator>? UnaryOperator,
    StrongBox<BinaryOperator>? BinaryOperator,
    StrongBox<VariableDeclaration>? VariableDeclaration,
    StrongBox<IfExpression>? IfExpression,
    StrongBox<Block>? Block)
{
    public Expression(ValueAccessor valueAccessor)
        : this(ExpressionType.ValueAccess, valueAccessor, null, null, null, null, null)
    {
        
    }
    
    public Expression(UnaryOperator unaryOperator)
        : this(ExpressionType.UnaryOperator, null, new StrongBox<UnaryOperator>(unaryOperator), null, null, null, null)
    {
    }

    public Expression(BinaryOperator binaryOperator)
        : this(ExpressionType.BinaryOperator, null, null, new StrongBox<BinaryOperator>(binaryOperator), null, null, null)
    {
    }
    
    public Expression(VariableDeclaration variableDeclaration)
        : this(ExpressionType.VariableDeclaration, null, null, null, new StrongBox<VariableDeclaration>(variableDeclaration), null, null)
    {}
    
    public Expression(IfExpression ifExpression)
        : this(ExpressionType.IfExpression, null, null, null, null, new StrongBox<IfExpression>(ifExpression), null)
    {
    }
    
    public Expression(Block block)
        : this(ExpressionType.Block, null, null, null, null, null, new StrongBox<Block>(block))
    {}

    public override string ToString()
    {
        return ExpressionType switch
        {
            ExpressionType.ValueAccess => ValueAccessor!.Value.ToString(),
            ExpressionType.UnaryOperator => UnaryOperator!.Value.ToString(),
            ExpressionType.BinaryOperator => BinaryOperator!.Value.ToString(),
            ExpressionType.VariableDeclaration => VariableDeclaration!.Value.ToString(),
            ExpressionType.IfExpression => IfExpression!.Value.ToString(),
            ExpressionType.Block => Block!.Value.ToString(),
            _ => throw new UnreachableException()
        };
    }
}

public readonly record struct VariableDeclaration(Token VariableNameToken, Expression Value)
{
    public override string ToString()
    {
        return $"var {VariableNameToken} = {Value}";
    }
}

public readonly record struct IfExpression(
    Expression CheckExpression,
    Expression Body,
    IReadOnlyCollection<ElseIf> ElseIfs,
    Expression? ElseBody)
{
    public override string ToString()
    {
        var sb = new StringBuilder($"if({CheckExpression}) {Body}");
        foreach (var elseIf in ElseIfs)
        {
            sb.Append($" {elseIf}");
        }
        if (ElseBody.HasValue)
        {
            sb.Append($" else {ElseBody.Value}");
        }

        return sb.ToString();
    }
}

public readonly record struct Block(IReadOnlyCollection<Expression> Expressions)
{
    public override string ToString()
    {
        var sb = new StringBuilder("{");
        foreach (var expression in Expressions)
        {
            sb.Append('\n');
            sb.Append(expression.ToString());
            sb.Append(';');
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

public readonly record struct UnaryOperator(UnaryOperatorType OperatorType, Expression Operand, Token OperatorToken)
{
    public override string ToString()
    {
        // todo: prefix and postfix
        return $"|{Operand}{OperatorToken}|";
    }
}

public readonly record struct BinaryOperator(BinaryOperatorType OperatorType, Expression Left, Expression Right, Token OperatorToken)
{
    public override string ToString()
    {
        return $"|{Left} {OperatorToken} {Right}|";
    }
}

public readonly record struct ElseIf(Expression CheckExpression, Expression Body)
{
    public override string ToString()
    {
        return $"else if ({CheckExpression}) {Body}";
    }
}

public enum BinaryOperatorType
{
    LessThan,
    GreaterThan,
    Plus,
    Minus,
    Multiply,
    Divide
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
    ValueAccess,
    UnaryOperator,
    BinaryOperator,
    VariableDeclaration,
    IfExpression,
    Block
}