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
        return new(TypeCheckerErrorType.UnresolvedInferredType, SourceRange.Default, "");
    }

    public static TypeCheckerError ThisAccessedOutsideOfInstanceMethod()
    {
        return new(TypeCheckerErrorType.ThisAccessedOutsideOfInstanceMethod, SourceRange.Default, "");
    }

    public static TypeCheckerError MatchNonExhaustive()
    {
        return new(TypeCheckerErrorType.MatchNonExhaustive, SourceRange.Default, "");
    }

    public static TypeCheckerError AccessUninitializedVariable()
    {
        return new(TypeCheckerErrorType.AccessUninitializedVariable, SourceRange.Default, "");
    }

    public static TypeCheckerError IncorrectNumberOfPatternsInTupleVariantUnionPattern()
    {
        return new(TypeCheckerErrorType.IncorrectNumberOfPatternsInTupleVariantUnionPattern, SourceRange.Default, "");
    }

    public static TypeCheckerError UnknownVariant()
    {
        return new(TypeCheckerErrorType.UnknownVariant, SourceRange.Default, "");
    }

    public static TypeCheckerError NonUnionTypeUsedInStructVariantUnionPattern()
    {
        return new(TypeCheckerErrorType.NonUnionTypeUsedInStructVariantUnionPattern, SourceRange.Default, "");
    }

    public static TypeCheckerError MissingFieldsInStructVariantUnionPattern()
    {
        return new(TypeCheckerErrorType.MissingFieldsInStructVariantUnionPattern, SourceRange.Default, "");
    }

    public static TypeCheckerError MissingFieldsInClassPattern(IEnumerable<string> fieldNames, TypeIdentifier className)
    {
        return new(
            TypeCheckerErrorType.MissingFieldsInClassPattern,
            className.SourceRange,
            $"missing fields from class pattern {string.Join(",", fieldNames.Select(x => $"'{x}'"))} for class {className}");
    }

    public static TypeCheckerError PrivateFieldReferenced(StringToken fieldNameReference)
    {
        return new(TypeCheckerErrorType.PrivateFieldReferenced,
            new SourceRange(fieldNameReference.SourceSpan,
                fieldNameReference.SourceSpan), $"Cannot access private field {fieldNameReference.StringValue}");
    }

    public static TypeCheckerError UnknownUnionStructVariantField()
    {
        return new(TypeCheckerErrorType.UnknownUnionStructVariantField, SourceRange.Default, "");
    }

    public static TypeCheckerError UnionStructVariantInitializerNotStructVariant()
    {
        return new(TypeCheckerErrorType.UnionStructVariantInitializerNotStructVariant, SourceRange.Default, "");
    }

    public static TypeCheckerError DuplicateVariantName(StringToken variantName)
    {
        return new(
            TypeCheckerErrorType.DuplicateVariantName,
            new SourceRange(variantName.SourceSpan, variantName.SourceSpan),
            $"Duplicate variant name {variantName.StringValue}");
    }

    public static TypeCheckerError ConflictingTypeName(StringToken name)
    {
        return new(TypeCheckerErrorType.ConflictingTypeName, new SourceRange(
            name.SourceSpan,
            name.SourceSpan), $"Type with name {name.StringValue} already defined in scope");
    }

    public static TypeCheckerError DuplicateFieldInUnionStructVariant(StringToken typeName, StringToken variantName, StringToken fieldName)
    {
        return new(TypeCheckerErrorType.DuplicateFieldInUnionStructVariant,
            new SourceRange(fieldName.SourceSpan, fieldName.SourceSpan),
            $"Duplicate field found in union struct variant. Union Type: {typeName}, Variant: {variantName}, fieldName: {fieldName}");
    }

    public static TypeCheckerError UnionTupleVariantNoParameters()
    {
        return new(TypeCheckerErrorType.UnionTupleVariantNoParameters, SourceRange.Default, "");
    }

    public static TypeCheckerError ConflictingFunctionName(StringToken functionName)
    {
        return new(TypeCheckerErrorType.ConflictingFunctionName,
            new SourceRange(functionName.SourceSpan, functionName.SourceSpan),
            $"Function with name {functionName.StringValue} already defined in scope");
    }

    public static TypeCheckerError IncorrectNumberOfMethodParameters()
    {
        return new(TypeCheckerErrorType.IncorrectNumberOfMethodParameters, SourceRange.Default, "");
    }

    public static TypeCheckerError MemberAccessOnGenericExpression()
    {
        return new(TypeCheckerErrorType.MemberAccessOnGenericExpression, SourceRange.Default, "");
    }

    public static TypeCheckerError StaticMemberAccessOnGenericReference()
    {
        return new(TypeCheckerErrorType.StaticMemberAccessOnGenericReference, SourceRange.Default, "");
    }

    public static TypeCheckerError DuplicateGenericArgument(StringToken argumentIdentifier)
    {
        return new(TypeCheckerErrorType.DuplicateGenericArgument, new SourceRange(
            argumentIdentifier.SourceSpan, argumentIdentifier.SourceSpan), $"Generic argument {argumentIdentifier.StringValue} already defined");
    }

    public static TypeCheckerError IncorrectNumberOfTypeArguments()
    {
        return new(TypeCheckerErrorType.IncorrectNumberOfTypeArguments, SourceRange.Default, "");
    }

    public static TypeCheckerError ClassFieldSetMultipleTypesInInitializer()
    {
        return new(TypeCheckerErrorType.ClassFieldSetMultipleTypesInInitializer, SourceRange.Default, "");
    }

    public static TypeCheckerError UnknownClassField()
    {
        return new(TypeCheckerErrorType.UnknownClassField, SourceRange.Default, "");
    }

    public static TypeCheckerError FieldsLeftUnassignedInClassInitializer()
    {
        return new(TypeCheckerErrorType.FieldsLeftUnassignedInClassInitializer, SourceRange.Default, "");
    }

    public static TypeCheckerError ConflictingTypeArgument(StringToken typeArgument)
    {
        return new(
            TypeCheckerErrorType.ConflictingTypeArgument,
            new SourceRange(typeArgument.SourceSpan, typeArgument.SourceSpan),
            $"Conflicting type argument with existing type argument {typeArgument.StringValue} outside of current definition");
    }

    public static TypeCheckerError TypeArgumentConflictsWithType()
    {
        return new(TypeCheckerErrorType.TypeArgumentConflictsWithType, SourceRange.Default, "");
    }
}

public enum TypeCheckerErrorType
{
    MismatchedTypes,
    ExpressionNotAssignable,
    NonMutableAssignment,
    NonMutableMemberAssignment,
    NonMutableMemberOwnerAssignment,
    SymbolNotFound,
    UnresolvedInferredType,
    ThisAccessedOutsideOfInstanceMethod,
    MatchNonExhaustive,
    AccessUninitializedVariable,
    IncorrectNumberOfPatternsInTupleVariantUnionPattern,
    UnknownVariant,
    NonUnionTypeUsedInStructVariantUnionPattern,
    MissingFieldsInStructVariantUnionPattern,
    MissingFieldsInClassPattern,
    PrivateFieldReferenced,
    UnknownUnionStructVariantField,
    UnionStructVariantInitializerNotStructVariant,
    DuplicateVariantName,
    ConflictingTypeName,
    DuplicateFieldInUnionStructVariant,
    UnionTupleVariantNoParameters,
    ConflictingFunctionName,
    IncorrectNumberOfMethodParameters,
    MemberAccessOnGenericExpression,
    StaticMemberAccessOnGenericReference,
    DuplicateGenericArgument,
    IncorrectNumberOfTypeArguments,
    ClassFieldSetMultipleTypesInInitializer,
    UnknownClassField,
    FieldsLeftUnassignedInClassInitializer,
    ConflictingTypeArgument,
    TypeArgumentConflictsWithType,
}