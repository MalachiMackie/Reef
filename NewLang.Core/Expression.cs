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
    StrongBox<Block>? Block,
    StrongBox<MethodCall>? MethodCall,
    StrongBox<MemberAccess>? MemberAccess,
    StrongBox<MethodReturn>? MethodReturn,
    StrongBox<ObjectInitializer>? ObjectInitializer,
    StrongBox<ValueAssignment>? ValueAssignment,
    StrongBox<StaticMemberAccess>? StaticMemberAccess,
    StrongBox<GenericInstantiation>? GenericInstantiation,
    StrongBox<EqualityCheck>? EqualityCheck)
{
    public Expression(ValueAccessor valueAccessor)
        : this(ExpressionType.ValueAccess, valueAccessor, null, null, null, null, null, null, null, null, null, null, null, null, null)
    {
        
    }
    
    public Expression(UnaryOperator unaryOperator)
        : this(ExpressionType.UnaryOperator, null, new StrongBox<UnaryOperator>(unaryOperator), null, null, null, null, null, null, null, null, null, null, null, null)
    {
    }

    public Expression(BinaryOperator binaryOperator)
        : this(ExpressionType.BinaryOperator, null, null, new StrongBox<BinaryOperator>(binaryOperator), null, null, null, null, null, null, null, null, null, null, null)
    {
    }
    
    public Expression(VariableDeclaration variableDeclaration)
        : this(ExpressionType.VariableDeclaration, null, null, null, new StrongBox<VariableDeclaration>(variableDeclaration), null, null, null, null, null, null, null, null, null, null)
    {}
    
    public Expression(IfExpression ifExpression)
        : this(ExpressionType.IfExpression, null, null, null, null, new StrongBox<IfExpression>(ifExpression), null, null, null, null, null, null, null, null, null)
    {
    }
    
    public Expression(Block block)
        : this(ExpressionType.Block, null, null, null, null, null, new StrongBox<Block>(block), null, null, null, null, null, null, null, null)
    {}
    
    public Expression(MethodCall methodCall)
        : this(ExpressionType.MethodCall, null, null, null, null, null, null, new StrongBox<MethodCall>(methodCall), null, null, null, null, null, null, null)
    {}

    public Expression(MemberAccess memberAccess)
        : this(ExpressionType.MemberAccess, null, null, null, null, null, null, null, new StrongBox<MemberAccess>(memberAccess), null, null, null, null, null, null)
    { }

    public Expression(MethodReturn methodReturn)
        : this(ExpressionType.MethodReturn, null, null, null, null, null, null, null, null, new StrongBox<MethodReturn>(methodReturn), null, null, null, null, null)
    { }

    public Expression(ObjectInitializer objectInitializer)
        : this(ExpressionType.ObjectInitializer, null, null, null, null, null, null, null, null, null, new StrongBox<ObjectInitializer>(objectInitializer), null, null, null, null)
    { }

    public Expression(ValueAssignment valueAssignment)
        : this(ExpressionType.ValueAssignment, null, null, null, null, null, null, null, null, null, null, new StrongBox<ValueAssignment>(valueAssignment), null, null, null)
    { }

    public Expression(StaticMemberAccess staticMemberAccess)
        : this(ExpressionType.StaticMemberAccess, null, null, null, null, null, null, null, null, null, null, null, new StrongBox<StaticMemberAccess>(staticMemberAccess), null, null)
    { }

    public Expression(GenericInstantiation genericInstantiation)
        : this(ExpressionType.GenericInstantiation, null, null, null, null, null, null, null, null, null, null, null, null, new StrongBox<GenericInstantiation>(genericInstantiation), null)
    { }
    
    public Expression(EqualityCheck equalityCheck)
        : this(ExpressionType.EqualityCheck, null, null, null, null, null, null, null, null, null, null, null, null, null, new StrongBox<EqualityCheck>(equalityCheck))
    { }

    public override string ToString()
    {
        return ExpressionType switch
        {
            ExpressionType.None => throw new InvalidOperationException("Expression has no expression type"),
            ExpressionType.ValueAccess => ValueAccessor!.Value.ToString(),
            ExpressionType.UnaryOperator => UnaryOperator!.Value.ToString(),
            ExpressionType.BinaryOperator => BinaryOperator!.Value.ToString(),
            ExpressionType.VariableDeclaration => VariableDeclaration!.Value.ToString(),
            ExpressionType.IfExpression => IfExpression!.Value.ToString(),
            ExpressionType.Block => Block!.Value.ToString(),
            ExpressionType.MethodCall => MethodCall!.Value.ToString(),
            ExpressionType.MemberAccess => MemberAccess!.Value.ToString(),
            ExpressionType.MethodReturn => MethodReturn!.Value.ToString(),
            ExpressionType.ObjectInitializer => ObjectInitializer!.Value.ToString(),
            ExpressionType.ValueAssignment => ValueAssignment!.Value.ToString(),
            ExpressionType.StaticMemberAccess => StaticMemberAccess!.Value.ToString(),
            ExpressionType.GenericInstantiation => GenericInstantiation!.Value.ToString(),
            ExpressionType.EqualityCheck => EqualityCheck!.Value.ToString(),
            _ => throw new UnreachableException()
        };
    }
}

public readonly record struct MemberAccess(Expression MemberOwner, Token Identifier)
{
    public override string ToString()
    {
        return $"{MemberOwner}.{Identifier}";
    }
}

public readonly record struct StaticMemberAccess(Expression Owner, Token Identifier)
{
    public override string ToString()
    {
        return $"{Owner}::{Identifier}";
    }
}

public readonly record struct VariableDeclaration(
    Token VariableNameToken, MutabilityModifier? MutabilityModifier, TypeIdentifier? Type, Expression? Value)
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
        if (Value.HasValue)
        {
            sb.Append($" = {Value.Value}");
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

public readonly record struct Block(IReadOnlyList<Expression> Expressions, IReadOnlyList<LangFunction> Functions)
{
    public static readonly Block Empty = new ([], []);
    
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

public readonly record struct MethodCall(Expression Method, IReadOnlyCollection<Expression> ParameterList)
{
    public override string ToString()
    {
        return $"{Method}({string.Join(", ", ParameterList)})";
    }
}

public readonly record struct GenericInstantiation(
    Expression GenericInstance,
    IReadOnlyList<TypeIdentifier> TypeArguments)
{
    public override string ToString()
    {
        return $"{GenericInstance}::<{string.Join(", ", TypeArguments)}>";
    }
}

public readonly record struct MethodReturn(Expression Expression)
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

public readonly record struct FieldInitializer(Token FieldName, Expression Value);

public readonly record struct ValueAssignment(Expression Left, Expression Right)
{
    public override string ToString()
    {
        return $"{Left} = {Right}";
    }
}

public readonly record struct EqualityCheck(Expression Left, Expression Right)
{
    public override string ToString()
    {
        return $"{Left} == {Right}";
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
    None,
    ValueAccess,
    UnaryOperator,
    BinaryOperator,
    VariableDeclaration,
    IfExpression,
    Block,
    MethodCall,
    MemberAccess,
    StaticMemberAccess,
    MethodReturn,
    ObjectInitializer,
    ValueAssignment,
    GenericInstantiation,
    EqualityCheck
}