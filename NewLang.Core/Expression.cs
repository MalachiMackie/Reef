using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NewLang.Core;

public readonly record struct Expression(
    ExpressionType ExpressionType,
    ValueAccessor? ValueAccessor,
    StrongBox<UnaryOperator>? UnaryOperator,
    StrongBox<BinaryOperator>? BinaryOperator,
    StrongBox<VariableDeclaration>? VariableDeclaration)
{
    public Expression(ValueAccessor valueAccessor)
        : this(ExpressionType.ValueAccess, valueAccessor, null, null, null)
    {
        
    }
    
    public Expression(UnaryOperator unaryOperator)
        : this(ExpressionType.UnaryOperator, null, new StrongBox<UnaryOperator>(unaryOperator), null, null)
    {
    }

    public Expression(BinaryOperator binaryOperator)
        : this(ExpressionType.BinaryOperator, null, null, new StrongBox<BinaryOperator>(binaryOperator), null)
    {
    }
    
    public Expression(VariableDeclaration variableDeclaration)
        : this(ExpressionType.VariableDeclaration, null, null, null, new StrongBox<VariableDeclaration>(variableDeclaration))
    {}

    public override string ToString()
    {
        return ExpressionType switch
        {
            ExpressionType.ValueAccess => ValueAccessor!.Value.ToString(),
            ExpressionType.UnaryOperator => UnaryOperator!.Value.ToString(),
            ExpressionType.BinaryOperator => BinaryOperator!.Value.ToString(),
            ExpressionType.VariableDeclaration => VariableDeclaration!.Value.ToString(),
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
    VariableDeclaration
}