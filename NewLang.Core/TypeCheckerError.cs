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

    public static TypeCheckerError AccessUninitializedVariable(StringToken variableIdentifier)
    {
        return new(TypeCheckerErrorType.AccessUninitializedVariable,
            new SourceRange(variableIdentifier.SourceSpan, variableIdentifier.SourceSpan),
            $"Variable {variableIdentifier} may be uninitialized");
    }

    public static TypeCheckerError IncorrectNumberOfPatternsInTupleVariantUnionPattern(UnionTupleVariantPattern pattern, int expectedNumber)
    {
        return new(
            TypeCheckerErrorType.IncorrectNumberOfPatternsInTupleVariantUnionPattern,
            pattern.SourceRange,
            $"Expected {expectedNumber} patterns in union tuple variant, but found {pattern.TupleParamPatterns.Count}");
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

    public static TypeCheckerError DuplicateFunctionArgument(StringToken argumentName, StringToken functionName)
    {
        return new(
            TypeCheckerErrorType.DuplicateFunctionArgument,
            new SourceRange(argumentName.SourceSpan, argumentName.SourceSpan),
            $"Duplicate argument {argumentName.StringValue} found in function {functionName.StringValue}");
    }

    public static TypeCheckerError IncorrectNumberOfTypeArguments()
    {
        return new(TypeCheckerErrorType.IncorrectNumberOfTypeArguments, SourceRange.Default, "");
    }

    public static TypeCheckerError ClassFieldSetMultipleTypesInInitializer(StringToken fieldName)
    {
        return new(TypeCheckerErrorType.ClassFieldSetMultipleTypesInInitializer,
            new SourceRange(fieldName.SourceSpan, fieldName.SourceSpan),
            $"Field {fieldName.StringValue} already assigned");
    }

    public static TypeCheckerError UnknownClassField(StringToken fieldName)
    {
        return new(TypeCheckerErrorType.UnknownClassField, new SourceRange(fieldName.SourceSpan, fieldName.SourceSpan), $"Unknown field {fieldName.StringValue}");
    }

    public static TypeCheckerError FieldsLeftUnassignedInClassInitializer(ObjectInitializerExpression objectInitializerExpression, IEnumerable<string> missingFieldNames)
    {
        return new(
            TypeCheckerErrorType.FieldsLeftUnassignedInClassInitializer,
            objectInitializerExpression.SourceRange,
            $"Not all fields were initialized {string.Join(",", missingFieldNames.Select(x => $"'{x}'"))}");
    }

    public static TypeCheckerError ConflictingTypeArgument(StringToken typeArgument)
    {
        return new(
            TypeCheckerErrorType.ConflictingTypeArgument,
            new SourceRange(typeArgument.SourceSpan, typeArgument.SourceSpan),
            $"Conflicting type argument with existing type argument {typeArgument.StringValue} outside of current definition");
    }

    public static TypeCheckerError TypeArgumentConflictsWithType(StringToken typeArgument)
    {
        return new(
            TypeCheckerErrorType.TypeArgumentConflictsWithType,
            new SourceRange(typeArgument.SourceSpan, typeArgument.SourceSpan),
            $"Type Argument {typeArgument} conflicts type existing type");
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
    DuplicateFunctionArgument,
    IncorrectNumberOfTypeArguments,
    ClassFieldSetMultipleTypesInInitializer,
    UnknownClassField,
    FieldsLeftUnassignedInClassInitializer,
    ConflictingTypeArgument,
    TypeArgumentConflictsWithType,
}