namespace NewLang.Core;

public record TypeCheckerError
{
    public TypeCheckerErrorType Type { get; init; }
    public SourceRange Range { get; init; }
    public string Message { get; init; }
    
    private TypeCheckerError(TypeCheckerErrorType type, SourceRange range, string message)
    {
        Type = type;
        Range = range;
        Message = message;
    }

    public static TypeCheckerError MismatchedTypes(SourceRange range, TypeChecker.ITypeReference expected, TypeChecker.ITypeReference actual) =>
        new(TypeCheckerErrorType.MismatchedTypes, range, $"Expected {expected}, but found {actual}");
    
    public static TypeCheckerError ExpressionNotAssignable(IExpression expression) =>
        new(TypeCheckerErrorType.ExpressionNotAssignable, expression.SourceRange, $"Expression {expression} is not assignable");
    
    public static TypeCheckerError NonMutableAssignment(string variableName, SourceRange sourceRange) =>
        new(TypeCheckerErrorType.NonMutableAssignment, sourceRange, $"Variable {variableName} is not marked as mutable");
    
    public static TypeCheckerError NonMutableMemberAssignment(IExpression memberAccess) =>
        new(TypeCheckerErrorType.NonMutableMemberAssignment, memberAccess.SourceRange, $"member {memberAccess} is not marked as mutable");
    
    public static TypeCheckerError NonMutableMemberOwnerAssignment(IExpression ownerExpression) =>
        new(TypeCheckerErrorType.NonMutableMemberOwnerAssignment, ownerExpression.SourceRange, $"member owner {ownerExpression} is not marked as mutable");

    public static TypeCheckerError SymbolNotFound(Token symbol) =>
        new(TypeCheckerErrorType.SymbolNotFound, new SourceRange(symbol.SourceSpan, symbol.SourceSpan), $"Symbol {symbol} not found");

    public static TypeCheckerError UnresolvedInferredType()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError ThisAccessedOutsideOfInstanceMethod()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError MatchNonExhaustive()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError AccessUninitializedVariable()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError IncorrectNumberOfPatternsInTupleVariantUnionPattern()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError UnknownVariant()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError NonUnionTypeUsedInStructVariantUnionPattern()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError MissingFieldsInStructVariantUnionPattern()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError MissingFieldsInClassPattern()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError PrivateFieldUsedInClassPattern()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError PrivateFieldReferenced()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError UnknownUnionStructVariantField()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError UnionStructVariantInitializerNotStructVariant()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError DuplicateVariantName()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError ConflictingTypeName()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError DuplicateFieldInUnionStructVariant()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError UnionTupleVariantNoParameters()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError ConflictingFunctionName()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError IncorrectNumberOfMethodParameters()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError MemberAccessOnGenericExpression()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError StaticMemberAccessOnGenericReference()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError DuplicateGenericArgument()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError IncorrectNumberOfTypeArguments()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError ClassFieldSetMultipleTypesInInitializer()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError UnknownClassField()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError FieldsLeftUnassignedInClassInitializer()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError ConflictingTypeArgument()
    {
        throw new NotImplementedException();
    }

    public static TypeCheckerError TypeArgumentConflictsWithType()
    {
        throw new NotImplementedException();
    }
}

public enum TypeCheckerErrorType
{
    MismatchedTypes,
    ExpressionNotAssignable,
    NonMutableAssignment,
    NonMutableMemberAssignment,
    NonMutableMemberOwnerAssignment,
    SymbolNotFound
}