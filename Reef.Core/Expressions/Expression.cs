using System.Text;
using Reef.Core.TypeChecking;

namespace Reef.Core.Expressions;

public interface IExpression
{
    ExpressionType ExpressionType { get; }

    TypeChecker.ITypeReference? ResolvedType { get; set; }

    SourceRange SourceRange { get; }
    bool Diverges { get; }
    bool ValueUseful { get; set; }
}

// Only used during parsing, should not exist turing type checking phase 
public record TypeIdentifierExpression(ITypeIdentifier TypeIdentifier) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.TypeIdentifier;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    public SourceRange SourceRange => TypeIdentifier.SourceRange;

    public bool Diverges => false;
    public bool ValueUseful { get; set; }
}

public record ValueAccessorExpression(ValueAccessor ValueAccessor) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.ValueAccess;

    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    public SourceRange SourceRange => new(ValueAccessor.Token.SourceSpan, ValueAccessor.Token.SourceSpan);

    public TypeChecker.IVariable? ReferencedVariable { get; set; }

    public bool Diverges => false;
    public bool ValueUseful { get; set; }
    public TypeChecker.InstantiatedFunction? FunctionInstantiation { get; set; }

    public override string ToString()
    {
        return ValueAccessor.ToString();
    }
}

public record MemberAccessExpression(MemberAccess MemberAccess) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MemberAccess;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public SourceRange SourceRange => MemberAccess.Owner.SourceRange with { End = MemberAccess.MemberName?.SourceSpan ?? MemberAccess.Owner.SourceRange.End };
    public bool Diverges { get; } = MemberAccess.Owner.Diverges;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return MemberAccess.ToString();
    }
}

public record StaticMemberAccessExpression(StaticMemberAccess StaticMemberAccess) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.StaticMemberAccess;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public TypeChecker.ITypeReference? OwnerType { get; set; }

    public SourceRange SourceRange => StaticMemberAccess.Type.SourceRange with
    {
        End = StaticMemberAccess.MemberName?.SourceSpan ?? StaticMemberAccess.Type.SourceRange.End
    };

    public bool Diverges => false;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return StaticMemberAccess.ToString();
    }
}

public record MemberAccess(IExpression Owner, StringToken? MemberName, IReadOnlyList<ITypeIdentifier>? TypeArguments)
{
    public override string ToString()
    {
        return $"{Owner}.{MemberName}";
    }

    public MemberType? MemberType { get; set; }
    public TypeChecker.ITypeReference? OwnerType { get; set; }
    public TypeChecker.InstantiatedFunction? InstantiatedFunction { get; set; }
}

public record StaticMemberAccess(NamedTypeIdentifier Type, StringToken? MemberName, IReadOnlyList<ITypeIdentifier>? TypeArguments)
{
    public MemberType? MemberType { get; set; }
    public TypeChecker.InstantiatedFunction? InstantiatedFunction { get; set; }

    public override string ToString()
    {
        return $"{Type}::{MemberName}";
    }
}

public enum MemberType
{
    Field,
    Function,
    Variant
}

public record UnaryOperatorExpression(UnaryOperator UnaryOperator) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.UnaryOperator;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges { get; } = UnaryOperator.Operand?.Diverges ?? false;
    public bool ValueUseful { get; set; }

    public SourceRange SourceRange
    {
        get
        {
            if (UnaryOperator.OperatorType.IsPrefix())
            {
                return new SourceRange(
                    UnaryOperator.OperatorToken.SourceSpan,
                    UnaryOperator.Operand?.SourceRange.End ?? UnaryOperator.OperatorToken.SourceSpan);
            }

            return new SourceRange(
                UnaryOperator.Operand?.SourceRange.Start ?? UnaryOperator.OperatorToken.SourceSpan,
                UnaryOperator.OperatorToken.SourceSpan);
        }
    }

    public override string ToString()
    {
        return UnaryOperator.ToString();
    }
}

public record BinaryOperatorExpression(BinaryOperator BinaryOperator) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.BinaryOperator;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    public SourceRange SourceRange
    {
        get
        {
            var start = BinaryOperator.Left?.SourceRange.Start ?? BinaryOperator.OperatorToken.SourceSpan;
            var end = BinaryOperator.Right?.SourceRange.End ?? BinaryOperator.OperatorToken.SourceSpan;

            return new SourceRange(start, end);
        }
    }

    public bool Diverges { get; } = (BinaryOperator.Left?.Diverges ?? false) || (BinaryOperator.Right?.Diverges ?? false);
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return BinaryOperator.ToString();
    }
}

public record WhileExpression(IExpression? Check, IExpression? Body, SourceRange SourceRange) : IExpression
{
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public ExpressionType ExpressionType => ExpressionType.While;
    
    public bool Diverges => Check is { Diverges: true } || Body is { Diverges: true };
    
    public bool ValueUseful { get; set; }
    
    public override string ToString()
    {
        return $"while ({Check}) {Body}";
    }
}

public record BreakExpression(SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Break;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges => false;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return "break";
    }
}

public record ContinueExpression(SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Continue;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges => false;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return "continue";
    }
}

public record VariableDeclarationExpression(VariableDeclaration VariableDeclaration, SourceRange SourceRange)
    : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.VariableDeclaration;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    public bool Diverges { get; } = VariableDeclaration.Value?.Diverges ?? false;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return VariableDeclaration.ToString();
    }
}

// todo: better name
public record IfExpressionExpression(IfExpression IfExpression, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.IfExpression;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    // If expression only diverges if all branches diverge
    public bool Diverges { get; } =
        IfExpression is { Body.Diverges: true, ElseBody.Diverges: true }
        && IfExpression.ElseIfs.All(x => x.Body is { Diverges: true });

    private bool _valueUseful;
    public bool ValueUseful
    {
        get => _valueUseful;
        set
        {
            _valueUseful = value;
            if (IfExpression.Body is not null)
            {
                IfExpression.Body.ValueUseful = value;
            }
            foreach (var elseIf in IfExpression.ElseIfs)
            {
                if (elseIf.Body is not null)
                {
                    elseIf.Body.ValueUseful = value;
                }
            }
            if (IfExpression.ElseBody is not null)
            {
                IfExpression.ElseBody.ValueUseful = value;
            }
        }
    }

    public override string ToString()
    {
        return IfExpression.ToString();
    }
}

public record BlockExpression(Block Block, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Block;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    public bool Diverges { get; } = Block.Expressions.Any(x => x.Diverges);
    private bool _valueUseful;
    public bool ValueUseful
    {
        get => _valueUseful;
        set
        {
            _valueUseful = value;
            if (Block.Expressions.Count > 0)
            {
                Block.Expressions[^1].ValueUseful = value;
            }
        }
    }

    public override string ToString()
    {
        return Block.ToString();
    }
}

public record TupleExpression(IReadOnlyList<IExpression> Values, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Tuple;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges { get; } = Values.Any(x => x.Diverges);
    public bool ValueUseful { get; set; }
}

public record MethodCallExpression(MethodCall MethodCall, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MethodCall;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges { get; } = MethodCall.Method.Diverges;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return MethodCall.ToString();
    }
}

public record MethodReturnExpression(MethodReturn MethodReturn, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.MethodReturn;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges => true;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return MethodReturn.ToString();
    }
}

public record UnionClassVariantInitializerExpression(
    UnionClassVariantInitializer UnionInitializer,
    SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.UnionClassVariantInitializer;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges { get; } = UnionInitializer.FieldInitializers.Any(x => x.Value is { Diverges: true });
    public bool ValueUseful { get; set; }
}

public record MatchesExpression(IExpression ValueExpression, IPattern? Pattern, SourceRange SourceRange) : IExpression
{
    /// <summary>
    ///     Collection of declared variables within <see cref="Pattern" />. Initialized during type checking
    /// </summary>
    public IReadOnlyList<TypeChecker.LocalVariable> DeclaredVariables { get; set; } = [];

    public ExpressionType ExpressionType => ExpressionType.Matches;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }

    public bool Diverges { get; } = ValueExpression.Diverges;
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder($"{ValueExpression} matches");
        if (Pattern is not null)
        {
            sb.Append($" {Pattern}");
        }

        return sb.ToString();
    }
}

public record UnionClassVariantInitializer(
    NamedTypeIdentifier UnionType,
    StringToken VariantIdentifier,
    IReadOnlyList<FieldInitializer> FieldInitializers)
{
    public override string ToString()
    {
        return $"{UnionType}::{VariantIdentifier} {{\r\n\t{string.Join("\r\n\t,", FieldInitializers)}\r\n}}";
    }

    public uint? VariantIndex { get; set; }
}

public record ObjectInitializerExpression(ObjectInitializer ObjectInitializer, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.ObjectInitializer;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges { get; } = ObjectInitializer.FieldInitializers.Any(x => x.Value is { Diverges: true });
    public bool ValueUseful { get; set; }

    public override string ToString()
    {
        return ObjectInitializer.ToString();
    }
}

public record VariableDeclaration(
    StringToken VariableNameToken,
    MutabilityModifier? MutabilityModifier,
    ITypeIdentifier? Type,
    IExpression? Value)
{
    public TypeChecker.IVariable? Variable { get; set; }

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

public record MatchExpression(IExpression Value, IReadOnlyList<MatchArm> Arms, SourceRange SourceRange) : IExpression
{
    public ExpressionType ExpressionType => ExpressionType.Match;
    public TypeChecker.ITypeReference? ResolvedType { get; set; }
    public bool Diverges { get; } = Value.Diverges || Arms.All(x => x.Expression is { Diverges: true });
    public bool ValueUseful { get; set; }
}

public record MatchArm(IPattern Pattern, IExpression? Expression);

public record IfExpression(
    IExpression CheckExpression,
    IExpression? Body,
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

public record ValueAccessor(ValueAccessType AccessType, Token Token, IReadOnlyList<ITypeIdentifier>? TypeArguments)
{
    public override string ToString()
    {
        return Token.ToString();
    }
}

public record UnaryOperator(UnaryOperatorType OperatorType, IExpression? Operand, Token OperatorToken)
{
    public override string ToString()
    {
        // todo: prefix and postfix
        return $"|{Operand}{OperatorToken}|";
    }
}

public static class UnaryOperatorTypeExtensions
{
    public static bool IsPrefix(this UnaryOperatorType @operator)
    {
        return @operator is UnaryOperatorType.Not;
    }
}

public record BinaryOperator(
    BinaryOperatorType OperatorType,
    IExpression? Left,
    IExpression? Right,
    Token OperatorToken)
{
    public override string ToString()
    {
        return $"|{Left} {OperatorToken} {Right}|";
    }
}

public record ElseIf(IExpression CheckExpression, IExpression? Body)
{
    public override string ToString()
    {
        return $"else if ({CheckExpression}) {Body}";
    }
}

public record MethodCall(IExpression Method, IReadOnlyList<IExpression> ArgumentList)
{
    public override string ToString()
    {
        return $"{Method}({string.Join(", ", ArgumentList)})";
    }
}

public record MethodReturn(IExpression? Expression)
{
    public override string ToString()
    {
        return Expression is null
            ? "return"
            : $"return {Expression}";
    }
}

public record ObjectInitializer(NamedTypeIdentifier Type, IReadOnlyList<FieldInitializer> FieldInitializers)
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

public record FieldInitializer(StringToken FieldName, IExpression? Value)
{
    public TypeChecker.TypeField? TypeField { get; set; }
}

public enum BinaryOperatorType
{
    LessThan,
    GreaterThan,
    Plus,
    Minus,
    Multiply,
    Divide,
    EqualityCheck,
    NegativeEqualityCheck,
    ValueAssignment,
    BooleanAnd,
    BooleanOr
}

public enum ValueAccessType
{
    Variable,
    Literal
}

public enum UnaryOperatorType
{
    // ?
    FallOut,
    Not
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
    MemberAccess,
    StaticMemberAccess,
    UnionClassVariantInitializer,
    Matches,
    Match,
    Tuple,
    While,
    Break,
    Continue,
    TypeIdentifier
}
