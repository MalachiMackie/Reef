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

    public static TypeCheckerError UnresolvedInferredVariableType(StringToken variableName)
    {
        return new(TypeCheckerErrorType.UnresolvedInferredVariableType, new SourceRange(variableName.SourceSpan, variableName.SourceSpan), $"Could not infer type for '{variableName.StringValue}");
    }
    
    public static TypeCheckerError UnresolvedInferredGenericType(IExpression expression, string unresolvedTypeArgument)
    {
        return new(TypeCheckerErrorType.UnresolvedInferredTypeArgument, expression.SourceRange, $"Could not infer type arguments {unresolvedTypeArgument}");
    }

    public static TypeCheckerError ThisAccessedOutsideOfInstanceMethod(StringToken thisToken)
    {
        return new(TypeCheckerErrorType.ThisAccessedOutsideOfInstanceMethod,
            new SourceRange(thisToken.SourceSpan, thisToken.SourceSpan),
            "'this' is only available within instance functions");
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

    public static TypeCheckerError UnknownVariant(StringToken variantIdentifier, string unionName)
    {
        return new(
            TypeCheckerErrorType.UnknownVariant,
            new SourceRange(variantIdentifier.SourceSpan, variantIdentifier.SourceSpan),
            $"Unknown variant {variantIdentifier} on union {unionName}");
    }

    public static TypeCheckerError NonClassUsedInClassPattern(TypeIdentifier typeIdentifier)
    {
        return new(
            TypeCheckerErrorType.NonClassUsedInClassPattern,
            typeIdentifier.SourceRange,
            $"{typeIdentifier} cannot be used as a class in a class pattern");
    }

    public static TypeCheckerError MissingFieldsInUnionClassVariantPattern(UnionClassVariantPattern pattern, IEnumerable<string> missingFields)
    {
        return new(TypeCheckerErrorType.MissingFieldsInUnionClassVariantPattern,
            pattern.SourceRange,
            $"Not all fields in union class variant pattern were listed. Missing fields: {string.Join(", ", missingFields)}");
    }

    public static TypeCheckerError MissingFieldsInClassPattern(IEnumerable<string> fieldNames, TypeIdentifier className)
    {
        return new(
            TypeCheckerErrorType.MissingFieldsInClassPattern,
            className.SourceRange,
            $"missing fields from class pattern {string.Join(", ", fieldNames.Select(x => $"'{x}'"))} for class {className}");
    }

    public static TypeCheckerError PrivateFieldReferenced(StringToken fieldNameReference)
    {
        return new(TypeCheckerErrorType.PrivateFieldReferenced,
            new SourceRange(fieldNameReference.SourceSpan,
                fieldNameReference.SourceSpan), $"Cannot access private field {fieldNameReference.StringValue}");
    }

    public static TypeCheckerError UnionClassVariantInitializerNotClassVariant(StringToken variantNameToken)
    {
        return new(
            TypeCheckerErrorType.UnionClassVariantInitializerNotClassVariant,
            new SourceRange(variantNameToken.SourceSpan, variantNameToken.SourceSpan),
            $"Variant {variantNameToken.StringValue} is not a union class variant");
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

    public static TypeCheckerError DuplicateFieldInUnionClassVariant(StringToken typeName, StringToken variantName, StringToken fieldName)
    {
        return new(TypeCheckerErrorType.DuplicateFieldInUnionClassVariant,
            new SourceRange(fieldName.SourceSpan, fieldName.SourceSpan),
            $"Duplicate field found in union class variant. Union Type: {typeName}, Variant: {variantName}, fieldName: {fieldName}");
    }

    public static TypeCheckerError ConflictingFunctionName(StringToken functionName)
    {
        return new(TypeCheckerErrorType.ConflictingFunctionName,
            new SourceRange(functionName.SourceSpan, functionName.SourceSpan),
            $"Function with name {functionName.StringValue} already defined in scope");
    }

    public static TypeCheckerError IncorrectNumberOfMethodArguments(MethodCallExpression methodCallExpression, int expectedNumberOfMethodArguments)
    {
        return new(
            TypeCheckerErrorType.IncorrectNumberOfMethodArguments,
            methodCallExpression.SourceRange,
            $"Expected {expectedNumberOfMethodArguments} arguments, but found {methodCallExpression.MethodCall.ArgumentList.Count}");
    }

    public static TypeCheckerError MemberAccessOnGenericExpression(MemberAccessExpression memberAccessExpression)
    {
        return new(
            TypeCheckerErrorType.MemberAccessOnGenericExpression,
            memberAccessExpression.MemberAccess.MemberName is {} memberName
                ? new SourceRange(memberName.SourceSpan, memberName.SourceSpan)
                : memberAccessExpression.SourceRange,
            $"Cannot access member on {memberAccessExpression.MemberAccess.Owner} which is a generic type");
    }

    public static TypeCheckerError StaticMemberAccessOnGenericReference(StaticMemberAccessExpression staticMemberAccess)
    {
        return new(
            TypeCheckerErrorType.StaticMemberAccessOnGenericReference,
            staticMemberAccess.StaticMemberAccess.MemberName is {} memberName
                ? new SourceRange(memberName.SourceSpan, memberName.SourceSpan)
                : staticMemberAccess.SourceRange,
            $"Cannot access static member on {staticMemberAccess.StaticMemberAccess.Type} which is a generic type");
    }

    public static TypeCheckerError DuplicateTypeParameter(StringToken parameterIdentifier)
    {
        return new(TypeCheckerErrorType.DuplicateGenericParameter, new SourceRange(
            parameterIdentifier.SourceSpan, parameterIdentifier.SourceSpan), $"Generic parameter {parameterIdentifier.StringValue} already defined");
    }

    public static TypeCheckerError DuplicateFunctionParameter(StringToken parameterName, StringToken functionName)
    {
        return new(
            TypeCheckerErrorType.DuplicateFunctionParameter,
            new SourceRange(parameterName.SourceSpan, parameterName.SourceSpan),
            $"Duplicate parameter {parameterName.StringValue} found in function {functionName.StringValue}");
    }

    public static TypeCheckerError IncorrectNumberOfTypeArguments(SourceRange sourceRange, int receivedCount, int expectedCount)
    {
        return new(TypeCheckerErrorType.IncorrectNumberOfTypeArguments, sourceRange, $"Expected {expectedCount} type arguments, but found {receivedCount}");
    }

    public static TypeCheckerError ClassFieldSetMultipleTypesInInitializer(StringToken fieldName)
    {
        return new(TypeCheckerErrorType.ClassFieldSetMultipleTypesInInitializer,
            new SourceRange(fieldName.SourceSpan, fieldName.SourceSpan),
            $"Field {fieldName.StringValue} already assigned");
    }

    public static TypeCheckerError UnknownField(StringToken fieldName, string ownerName)
    {
        return new(TypeCheckerErrorType.UnknownField, new SourceRange(fieldName.SourceSpan, fieldName.SourceSpan), $"Unknown field {fieldName.StringValue} on {ownerName}");
    }

    public static TypeCheckerError FieldsLeftUnassignedInClassInitializer(ObjectInitializerExpression objectInitializerExpression, IEnumerable<string> missingFieldNames)
    {
        return new(
            TypeCheckerErrorType.FieldsLeftUnassignedInClassInitializer,
            objectInitializerExpression.SourceRange,
            $"Not all fields were initialized {string.Join(", ", missingFieldNames.Select(x => $"'{x}'"))}");
    }

    public static TypeCheckerError ConflictingTypeParameter(StringToken typeParameter)
    {
        return new(
            TypeCheckerErrorType.ConflictingTypeParameter,
            new SourceRange(typeParameter.SourceSpan, typeParameter.SourceSpan),
            $"Conflicting type parameter with existing type parameter {typeParameter.StringValue} outside of current definition");
    }

    public static TypeCheckerError TypeParameterConflictsWithType(StringToken typeParameter)
    {
        return new(
            TypeCheckerErrorType.TypeParameterConflictsWithType,
            new SourceRange(typeParameter.SourceSpan, typeParameter.SourceSpan),
            $"Type Parameter {typeParameter} conflicts type existing type");
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
    UnresolvedInferredVariableType,
    UnresolvedInferredTypeArgument,
    ThisAccessedOutsideOfInstanceMethod,
    MatchNonExhaustive,
    AccessUninitializedVariable,
    IncorrectNumberOfPatternsInTupleVariantUnionPattern,
    UnknownVariant,
    NonClassUsedInClassPattern,
    MissingFieldsInUnionClassVariantPattern,
    MissingFieldsInClassPattern,
    PrivateFieldReferenced,
    UnionClassVariantInitializerNotClassVariant,
    DuplicateVariantName,
    ConflictingTypeName,
    DuplicateFieldInUnionClassVariant,
    UnionTupleVariantNoParameters,
    ConflictingFunctionName,
    IncorrectNumberOfMethodArguments,
    MemberAccessOnGenericExpression,
    StaticMemberAccessOnGenericReference,
    DuplicateGenericParameter,
    DuplicateFunctionParameter,
    IncorrectNumberOfTypeArguments,
    ClassFieldSetMultipleTypesInInitializer,
    UnknownField,
    FieldsLeftUnassignedInClassInitializer,
    ConflictingTypeParameter,
    TypeParameterConflictsWithType,
}