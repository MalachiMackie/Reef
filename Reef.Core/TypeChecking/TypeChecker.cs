﻿using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private readonly LangProgram _program;
    private readonly List<TypeCheckerError> _errors = [];

    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new();

    private readonly Dictionary<string, ITypeSignature> _types = ClassSignature.BuiltInTypes
        .Concat(UnionSignature.BuiltInTypes)
        .ToDictionary(x => x.Name);

    private TypeChecker(LangProgram program)
    {
        _program = program;
    }

    private Dictionary<string, FunctionSignature> ScopedFunctions => _typeCheckingScopes.Peek().Functions;
    private HashSet<GenericPlaceholder> GenericPlaceholders => _typeCheckingScopes.Peek().GenericPlaceholders;
    private ITypeSignature? CurrentTypeSignature => _typeCheckingScopes.Peek().CurrentTypeSignature;
    private FunctionSignature? CurrentFunctionSignature => _typeCheckingScopes.Peek().CurrentFunctionSignature;
    private ITypeReference ExpectedReturnType => _typeCheckingScopes.Peek().ExpectedReturnType;

    public static IReadOnlyList<TypeCheckerError> TypeCheck(LangProgram program)
    {
        var typeChecker = new TypeChecker(program);
        typeChecker.TypeCheckInner();

        return typeChecker._errors;
    }

    private void TypeCheckInner()
    {
        // initial scope
        _typeCheckingScopes.Push(new TypeCheckingScope(
            null,
            new Dictionary<string, FunctionSignature>(),
            InstantiatedClass.Unit,
            null,
            null,
            []));

        var (classes, unions) = SetupSignatures();

        foreach (var unionSignature in unions)
        {
            using var _ = PushScope(unionSignature, genericPlaceholders: unionSignature.TypeParameters);

            foreach (var function in unionSignature.Functions)
            {
                TypeCheckFunctionBody(function);
            }
        }

        foreach (var (@class, classSignature) in classes)
        {
            using var _ = PushScope(genericPlaceholders: classSignature.TypeParameters);

            var instanceFieldVariables = new List<IVariable>();
            var staticFieldVariables = new List<IVariable>();

            foreach (var (fieldIndex, field) in @class.Fields.Index())
            {
                var isStatic = field.StaticModifier is not null;

                var fieldTypeReference = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type);

                if (isStatic)
                {
                    // todo: static constructor?
                    if (field.InitializerValue is null)
                    {
                        throw new InvalidOperationException("Expected field initializer for static field");
                    }

                    var valueType = TypeCheckExpression(field.InitializerValue);
                    field.InitializerValue.ValueUseful = true;

                    ExpectType(valueType, fieldTypeReference, field.InitializerValue.SourceRange);

                    staticFieldVariables.Add(new FieldVariable(
                        classSignature,
                        field.Name,
                        fieldTypeReference,
                        field.MutabilityModifier is not null,
                        IsStaticField: true,
                        (uint)fieldIndex));
                }
                else
                {
                    if (field.InitializerValue is not null)
                    {
                        throw new InvalidOperationException("Instance fields cannot have initializers");
                    }

                    instanceFieldVariables.Add(new FieldVariable(
                        classSignature,
                        field.Name,
                        fieldTypeReference,
                        field.MutabilityModifier is not null,
                        IsStaticField: false,
                        (uint)fieldIndex));
                }
            }

            // static functions
            using (PushScope(classSignature))
            {
                // static functions only have access to static fields
                foreach (var variable in staticFieldVariables)
                {
                    AddScopedVariable(variable.Name.StringValue, variable);
                }

                foreach (var function in classSignature.Functions.Where(x => x.IsStatic))
                {
                    TypeCheckFunctionBody(function);
                }
            }

            // instance functions
            using (PushScope(classSignature))
            {
                // instance functions have access to both instance and static fields
                foreach (var variable in instanceFieldVariables.Concat(staticFieldVariables))
                {
                    AddScopedVariable(variable.Name.StringValue, variable);
                }

                foreach (var function in classSignature.Functions.Where(x => !x.IsStatic))
                {
                    TypeCheckFunctionBody(function);
                }
            }
        }

        foreach (var expression in _program.Expressions)
        {
            TypeCheckExpression(expression);
        }

        foreach (var functionSignature in ScopedFunctions.Values)
        {
            TypeCheckFunctionBody(functionSignature);
        }

        PopScope();

        if (_errors.Count == 0)
        {
            _errors.AddRange(TypeTwoTypeChecker.TypeTwoTypeCheck(_program));
        }
    }

    private InstantiatedClass TypeCheckBlock(
        Block block)
    {
        using var _ = PushScope();

        foreach (var fn in block.Functions)
        {
            var signature = fn.Signature ?? TypeCheckFunctionSignature(fn, functionIndex: null, ownerType: null);

            CurrentFunctionSignature?.LocalFunctions.Add(signature);

            ScopedFunctions[fn.Name.StringValue] = signature;
        }

        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(ScopedFunctions[fn.Name.StringValue]);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression);
        }

        // todo: tail expressions
        return InstantiatedClass.Unit;
    }

    private TypeField? TryGetClassField(InstantiatedClass classType, StringToken fieldName)
    {
        var field = classType.Fields.FirstOrDefault(x => x.Name == fieldName.StringValue);

        if (field is null)
        {
            _errors.Add(TypeCheckerError.UnknownTypeMember(fieldName, classType.Signature.Name));
            return null;
        }

        if ((CurrentTypeSignature is not ClassSignature currentClassSignature
             || !classType.MatchesSignature(currentClassSignature))
            && !field.IsPublic)
        {
            _errors.Add(TypeCheckerError.PrivateFieldReferenced(fieldName));
        }

        return field;
    }

    private InstantiatedClass TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression)
    {
        var varName = expression.VariableDeclaration.VariableNameToken;
        var isVariableDefined = VariableIsDefined(varName.StringValue);
        if (isVariableDefined)
        {
            // todo: variable shadowing?
            _errors.Add(TypeCheckerError.DuplicateVariableDeclaration(varName));
        }

        LocalVariable? variable = null;
        switch (expression.VariableDeclaration)
        {
            case { Value: null, Type: null, MutabilityModifier: var mutModifier }:
                {
                    variable = new LocalVariable(
                        CurrentFunctionSignature,
                        varName, new UnknownInferredType(), Instantiated: false, Mutable: mutModifier is not null);
                    break;
                }
            case { Value: { } value, Type: var type, MutabilityModifier: var mutModifier }:
                {
                    var valueType = TypeCheckExpression(value);
                    value.ValueUseful = true;
                    ITypeReference variableType;
                    if (type is not null)
                    {
                        variableType = GetTypeReference(type);

                        ExpectExpressionType(variableType, value);
                    }
                    else
                    {
                        variableType = valueType;
                    }

                    variable = new LocalVariable(CurrentFunctionSignature, varName, variableType, true, mutModifier is not null);
                    break;
                }
            case { Value: null, Type: { } type, MutabilityModifier: var mutModifier }:
                {
                    var langType = GetTypeReference(type);
                    variable = new LocalVariable(CurrentFunctionSignature, varName, langType, false, mutModifier is not null);
                    break;
                }
        }

        if (variable is not null && !isVariableDefined)
        {
            AddScopedVariable(varName.StringValue, variable);
        }
        expression.VariableDeclaration.Variable = variable;

        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedClass.Unit;
    }

    private ITypeReference GetTypeReference(
        ITypeIdentifier typeIdentifier)
    {
        return typeIdentifier switch
        {
            FnTypeIdentifier fnTypeIdentifier => GetFnTypeReference(fnTypeIdentifier),
            NamedTypeIdentifier namedTypeIdentifier => GetTypeReference(namedTypeIdentifier),
            TupleTypeIdentifier tupleTypeIdentifier => GetTypeReference(tupleTypeIdentifier),
            UnitTypeIdentifier => InstantiatedClass.Unit,
            _ => throw new ArgumentOutOfRangeException(nameof(typeIdentifier))
        };
    }

    private FunctionObject GetFnTypeReference(FnTypeIdentifier identifier)
    {
        return new FunctionObject(
            identifier.Parameters.Select(x => new FunctionParameter(GetTypeReference(x.ParameterType), x.Mut)).ToArray(),
            identifier.ReturnType is null ? InstantiatedClass.Unit : GetTypeReference(identifier.ReturnType));
    }

    private ITypeReference GetTypeReference(TupleTypeIdentifier tupleTypeIdentifier)
    {
        return InstantiateTuple(tupleTypeIdentifier.Members.Select(x => (GetTypeReference(x), x.SourceRange)).ToArray(), tupleTypeIdentifier.SourceRange);
    }

    private ITypeReference GetTypeReference(
        NamedTypeIdentifier typeIdentifier)
    {
        var identifierName = typeIdentifier.Identifier.StringValue;

        if (_types.TryGetValue(identifierName, out var nameMatchingType))
        {
            switch (nameMatchingType)
            {
                case ClassSignature classSignature:
                    return InstantiateClass(classSignature, [
                        ..typeIdentifier.TypeArguments
                            .Select(x => (GetTypeReference(x), x.SourceRange))
                    ], typeIdentifier.SourceRange);
                case UnionSignature unionSignature:
                    return InstantiateUnion(unionSignature, [
                        ..typeIdentifier.TypeArguments
                            .Select(x => (GetTypeReference(x), x.SourceRange))
                    ], typeIdentifier.SourceRange);
            }
        }

        if (GenericPlaceholders.FirstOrDefault(x => x.GenericName == identifierName) is { } genericTypeReference)
        {
            return genericTypeReference;
        }

        _errors.Add(TypeCheckerError.SymbolNotFound(typeIdentifier.Identifier));
        return UnknownType.Instance;
    }

    private void ExpectExpressionType(ITypeReference expected, IExpression? actual)
    {
        if (actual is null)
        {
            return;
        }

        if (actual.ResolvedType is null)
        {
            throw new InvalidOperationException("Expected should have been type checked first before expecting it's value type");
        }

        _ = actual switch
        {
            MatchExpression matchExpression => ExpectMatchExpressionType(matchExpression),
            BlockExpression blockExpression => ExpectBlockExpressionType(blockExpression),
            IfExpressionExpression ifExpressionExpression => ExpectIfExpressionType(ifExpressionExpression),
            // these expression types are considered to provide their own types, rather than deferring to inner expressions
            BinaryOperatorExpression or MatchesExpression or MemberAccessExpression or MethodCallExpression
                or MethodReturnExpression or ObjectInitializerExpression or StaticMemberAccessExpression
                or TupleExpression or UnaryOperatorExpression or UnionClassVariantInitializerExpression
                or ValueAccessorExpression or VariableDeclarationExpression => ExpectType(actual.ResolvedType!,
                    expected, actual.SourceRange),
            _ => throw new UnreachableException(actual.GetType().ToString())
        };

        return;

        bool ExpectIfExpressionType(IfExpressionExpression ifExpression)
        {
            return ExpectType(ifExpression.ResolvedType!, expected, SourceRange.Default);
            // todo: tail expression
        }

        bool ExpectBlockExpressionType(BlockExpression blockExpression)
        {
            return ExpectType(blockExpression.ResolvedType!, expected, SourceRange.Default);
            // todo: tail expression
        }

        bool ExpectMatchExpressionType(MatchExpression matchExpression)
        {
            return ExpectType(matchExpression.ResolvedType!, expected, SourceRange.Default);
            // todo: tail expression
        }
    }

    private bool ExpectType(ITypeReference actual, ITypeReference expected,
        SourceRange actualSourceRange, bool reportError = true)
    {
        if ((actual is InstantiatedClass x && x.IsSameSignature(InstantiatedClass.Never))
            || (expected is InstantiatedClass y && y.IsSameSignature(InstantiatedClass.Never)))
        {
            return true;
        }

        var result = true;

        switch (actual, expected)
        {
            case (GenericPlaceholder placeholder1, GenericTypeReference reference2):
                {
                    if (reference2.ResolvedType is not null)
                    {
                        result = ExpectType(placeholder1, reference2.ResolvedType, actualSourceRange, reportError);
                    }
                    else
                    {
                        reference2.ResolvedType = placeholder1;
                    }

                    break;
                }
            case (GenericTypeReference reference1, GenericPlaceholder placeholder2):
                {
                    if (reference1.ResolvedType is not null)
                    {
                        result = ExpectType(placeholder2, reference1.ResolvedType, actualSourceRange, reportError);
                    }
                    else
                    {
                        reference1.ResolvedType = placeholder2;
                    }

                    break;
                }
            case (GenericPlaceholder placeholder1, GenericPlaceholder placeholder2):
                {
                    result = placeholder1 == placeholder2;
                    if (!result && reportError)
                    {
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    break;
                }
            case (GenericPlaceholder, not null):
            case (not null, GenericPlaceholder):
                {
                    result = false;
                    if (reportError)
                    {
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    break;
                }
            case (InstantiatedClass actualClass, InstantiatedClass expectedClass):
                {
                    if (!actualClass.IsSameSignature(expectedClass))
                    {
                        if (reportError)
                            _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                        break;
                    }

                    var argumentsPassed = true;

                    for (var i = 0; i < actualClass.TypeArguments.Count; i++)
                    {
                        argumentsPassed &= ExpectType(actualClass.TypeArguments[i], expectedClass.TypeArguments[i], actualSourceRange, reportError: false);
                    }

                    if (!argumentsPassed && reportError)
                    {
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }

                    result &= argumentsPassed;

                    break;
                }
            case (InstantiatedUnion actualUnion, InstantiatedUnion expectedUnion):
                {
                    if (!actualUnion.IsSameSignature(expectedUnion))
                    {
                        if (reportError)
                            _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                        break;
                    }

                    var argumentsPassed = true;

                    for (var i = 0; i < actualUnion.TypeArguments.Count; i++)
                    {
                        argumentsPassed &= ExpectType(actualUnion.TypeArguments[i], expectedUnion.TypeArguments[i],
                            actualSourceRange, reportError: false);
                    }

                    if (!argumentsPassed && reportError)
                    {
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    result &= argumentsPassed;

                    break;
                }
            case (InstantiatedUnion union, GenericTypeReference generic):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError);
                    }
                    else
                    {
                        generic.ResolvedType = union;
                    }

                    break;
                }
            case (GenericTypeReference generic, InstantiatedUnion union):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError);
                    }
                    else
                    {
                        generic.ResolvedType = union;
                    }

                    break;
                }
            case (InstantiatedClass @class, GenericTypeReference generic):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(@class, generic.ResolvedType, actualSourceRange, reportError);
                    }
                    else
                    {
                        generic.ResolvedType = @class;
                    }

                    break;
                }
            case (GenericTypeReference generic, InstantiatedClass @class):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(@class, generic.ResolvedType, actualSourceRange, reportError);
                    }
                    else
                    {
                        generic.ResolvedType = @class;
                    }

                    break;
                }
            case (GenericTypeReference genericTypeReference, GenericTypeReference expectedGeneric):
                {
                    if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is not null)
                    {
                        result &= ExpectType(genericTypeReference.ResolvedType, expectedGeneric.ResolvedType, actualSourceRange, reportError);
                    }
                    else if (genericTypeReference.ResolvedType is null && expectedGeneric.ResolvedType is not null)
                    {
                        genericTypeReference.ResolvedType = expectedGeneric.ResolvedType;
                    }
                    else if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is null)
                    {
                        expectedGeneric.ResolvedType = genericTypeReference.ResolvedType;
                    }
                    else
                    {
                        genericTypeReference.Link(expectedGeneric);
                        if (expectedGeneric != genericTypeReference)
                        {
                            expectedGeneric.ResolvedType = genericTypeReference;
                        }
                    }

                    break;
                }
            case (FunctionObject functionObject1, FunctionObject functionObject2):
                {
                    result &= ExpectType(functionObject1.ReturnType, functionObject2.ReturnType, actualSourceRange,
                        reportError: false);
                    result &= functionObject1.Parameters.Count == functionObject2.Parameters.Count;
                    result &= functionObject1.Parameters.Zip(functionObject2.Parameters)
                        .All(z => z.First.Mutable == z.Second.Mutable
                                  && ExpectType(z.First.Type, z.Second.Type, actualSourceRange, reportError: false));

                    if (!result && reportError)
                    {
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }

                    break;
                }
        }

        return result;
    }

    public interface ITypeSignature
    {
        string Name { get; }
        Guid Id { get; }
        IReadOnlyList<GenericPlaceholder> TypeParameters { get; }
    }

    public record TypeField
    {
        public required ITypeReference Type { get; init; }
        public required string Name { get; init; }
        public required bool IsStatic { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
        public required IExpression? StaticInitializer { get; init; }
        public required uint FieldIndex { get; init; }
    }
}
