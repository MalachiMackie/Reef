using System.Diagnostics;
using NewLang.Core.PatternAnalysis;

namespace NewLang.Core;

// todo: this should probably be able to be a visitor
public class TypeTwoTypeChecker
{
    private readonly List<TypeCheckerError> _errors = [];

    public static IReadOnlyList<TypeCheckerError> TypeTwoTypeCheck(LangProgram program)
    {
        var checker = new TypeTwoTypeChecker();
        checker.InnerTypeTwoTypeCheck(program);

        return checker._errors;
    }

    private void InnerTypeTwoTypeCheck(LangProgram program)
    {
        foreach (var function in program.Functions)
        {
            CheckFunction(function, []);
        }

        foreach (var union in program.Unions)
        {
            CheckUnion(union);
        }

        foreach (var programClass in program.Classes)
        {
            CheckClass(programClass);
        }

        foreach (var expression in program.Expressions)
        {
            CheckExpression(expression, []);
        }
    }

    private void CheckUnion(ProgramUnion union)
    {
        if (union.Signature is null)
        {
            throw new Exception("Union Signature was not created");
        }

        var unionGenerics = union.Signature.TypeParameters.ToHashSet();
        foreach (var function in union.Functions)
        {
            CheckFunction(function, unionGenerics);
        }
    }

    private void CheckClass(ProgramClass programClass)
    {
        if (programClass.Signature is null)
        {
            throw new InvalidOperationException("Class signature was not created");
        }

        var classGenerics = programClass.Signature.TypeParameters.ToHashSet();
        foreach (var function in programClass.Functions)
        {
            CheckFunction(function, classGenerics);
        }

        foreach (var expression in programClass.Fields
                     .Select(x => x.InitializerValue)
                     .Where(x => x is not null))
        {
            CheckExpression(expression!, classGenerics);
        }
    }

    private void CheckFunction(LangFunction function, HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        if (function.Signature is null)
        {
            throw new InvalidOperationException("Function signature was not created");
        }

        HashSet<TypeChecker.GenericTypeReference> innerGenerics =
            [..function.Signature.TypeParameters, ..expectedGenerics];
        foreach (var blockFunction in function.Block.Functions)
        {
            CheckFunction(blockFunction, innerGenerics);
        }

        foreach (var expression in function.Block.Expressions)
        {
            CheckExpression(expression, innerGenerics);
        }
    }

    private void CheckExpression(IExpression expression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        if (expression.ResolvedType is null)
        {
            throw new UnreachableException("Every expression should be type checked");
        }

        CheckTypeReferenceIsResolved(expression.ResolvedType, expression, expectedGenerics);

        switch (expression)
        {
            case VariableDeclarationExpression variableDeclarationExpression:
                CheckVariableDeclaration(variableDeclarationExpression, expectedGenerics);
                break;
            case ValueAccessorExpression:
                // nothing to check
                break;
            case MethodReturnExpression methodReturnExpression:
                CheckMethodReturn(methodReturnExpression, expectedGenerics);
                break;
            case MethodCallExpression methodCallExpression:
                CheckMethodCall(methodCallExpression.MethodCall, expectedGenerics);
                break;
            case BlockExpression blockExpression:
                CheckBlock(blockExpression.Block, expectedGenerics);
                break;
            case IfExpressionExpression ifExpressionExpression:
                CheckIfExpression(ifExpressionExpression.IfExpression, expectedGenerics);
                break;
            case BinaryOperatorExpression binaryOperatorExpression:
                CheckBinaryOperatorExpression(binaryOperatorExpression.BinaryOperator, expectedGenerics);
                break;
            case ObjectInitializerExpression objectInitializerExpression:
                CheckObjectInitializer(objectInitializerExpression.ObjectInitializer, expectedGenerics);
                break;
            case MemberAccessExpression memberAccessExpression:
                CheckMemberAccess(memberAccessExpression.MemberAccess, expectedGenerics);
                break;
            case StaticMemberAccessExpression:
                // nothing to check
                break;
            case GenericInstantiationExpression genericInstantiationExpression:
                CheckGenericInstantiation(genericInstantiationExpression.GenericInstantiation, expectedGenerics);
                break;
            case UnaryOperatorExpression unaryOperatorExpression:
                CheckUnaryOperator(unaryOperatorExpression.UnaryOperator, expectedGenerics);
                break;
            case UnionClassVariantInitializerExpression unionClassVariantInitializerExpression:
                CheckUnionClassVariantInitializer(unionClassVariantInitializerExpression.UnionInitializer, expectedGenerics);
                break;
            case MatchesExpression matchesExpression:
                CheckMatchesExpression(matchesExpression, expectedGenerics);
                break;
            case TupleExpression tupleExpression:
                CheckTupleExpression(tupleExpression, expectedGenerics);
                break;
            case MatchExpression matchExpression:
                CheckMatchExpression(matchExpression, expectedGenerics);
                break;
            default:
                throw new NotImplementedException($"{expression.ExpressionType}");
        }
    }

    private void CheckMatchExpression(MatchExpression matchExpression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        CheckExpression(matchExpression.Value, expectedGenerics);
        foreach (var arm in matchExpression.Arms)
        {
            if (arm.Expression is not null)
            {
                CheckExpression(arm.Expression, expectedGenerics);
            }
        }

        var usefulnessReport = MatchUsefulnessAnalyzer.ComputeMatchUsefulness(
            matchExpression.Arms,
            matchExpression.Value.ResolvedType ?? throw new InvalidOperationException("expected resolved type"),
            PlaceValidity.ValidOnly,
            // random guess
            complexityLimit: 15);

        foreach (var (arm, usefulness) in usefulnessReport.ArmUsefulness)
        {
            if (usefulness is Useful { OrPatternRedundancies: { Count: > 0 } redundantSubPatterns })
            {
                foreach (var (pattern, explanation) in redundantSubPatterns)
                {
                    // warn
                }
            }
        }

        if (usefulnessReport.NonExhaustivenessWitnesses.Count > 0)
        {
            _errors.Add(TypeCheckerError.MatchNonExhaustive(matchExpression.SourceRange));
        }
    }

    private void CheckTupleExpression(TupleExpression tupleExpression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        foreach (var element in tupleExpression.Values)
        {
            CheckExpression(element, expectedGenerics);
        }
    }

    private void CheckMatchesExpression(MatchesExpression matchesExpression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        CheckExpression(matchesExpression.ValueExpression, expectedGenerics);
    }

    private void CheckUnionClassVariantInitializer(UnionClassVariantInitializer unionInitializer,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        foreach (var initializer in unionInitializer.FieldInitializers)
        {
            if (initializer.Value is not null)
            {
                CheckExpression(initializer.Value, expectedGenerics);
            }
        }
    }

    private void CheckUnaryOperator(UnaryOperator unaryOperator,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        if (unaryOperator.Operand is not null)
        {
            CheckExpression(unaryOperator.Operand, expectedGenerics);
        }
    }

    private void CheckGenericInstantiation(GenericInstantiation genericInstantiation,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        CheckExpression(genericInstantiation.Value, expectedGenerics);
    }

    private void CheckMemberAccess(MemberAccess memberAccess,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        CheckExpression(memberAccess.Owner, expectedGenerics);
    }

    private void CheckObjectInitializer(ObjectInitializer objectInitializer,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        foreach (var initializer in objectInitializer.FieldInitializers)
        {
            if (initializer.Value is not null)
            {
                CheckExpression(initializer.Value, expectedGenerics);
            }
        }
    }

    private void CheckBinaryOperatorExpression(BinaryOperator binaryOperator,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        if (binaryOperator.Left is not null)
        {
            CheckExpression(binaryOperator.Left, expectedGenerics);
        }

        if (binaryOperator.Right is not null)
        {
            CheckExpression(binaryOperator.Right, expectedGenerics);
        }
    }

    private void CheckIfExpression(IfExpression ifExpression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        CheckExpression(ifExpression.CheckExpression, expectedGenerics);
        if (ifExpression.Body is not null)
        {
            CheckExpression(ifExpression.Body, expectedGenerics);
        }
        foreach (var elseIf in ifExpression.ElseIfs)
        {
            CheckExpression(elseIf.CheckExpression, expectedGenerics);
            if (elseIf.Body is not null)
            {
                CheckExpression(elseIf.Body, expectedGenerics);
            }
        }

        if (ifExpression.ElseBody is not null)
        {
            CheckExpression(ifExpression.ElseBody, expectedGenerics);
        }
    }

    private void CheckBlock(Block blockExpressionBlock,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        foreach (var function in blockExpressionBlock.Functions)
        {
            CheckFunction(function, expectedGenerics);
        }

        foreach (var expression in blockExpressionBlock.Expressions)
        {
            CheckExpression(expression, expectedGenerics);
        }
    }

    private void CheckMethodCall(MethodCall methodCall,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        CheckExpression(methodCall.Method, expectedGenerics);
        foreach (var parameter in methodCall.ArgumentList)
        {
            CheckExpression(parameter, expectedGenerics);
        }
    }

    private void CheckMethodReturn(MethodReturnExpression methodReturnExpression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        if (methodReturnExpression.MethodReturn.Expression is not null)
        {
            CheckExpression(methodReturnExpression.MethodReturn.Expression, expectedGenerics);
        }
    }

    private void CheckVariableDeclaration(VariableDeclarationExpression variableDeclarationExpression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        if (variableDeclarationExpression.VariableDeclaration.Value is { } value)
        {
            CheckExpression(value, expectedGenerics);
        }

        if (variableDeclarationExpression.VariableDeclaration.Variable is not { } variable)
        {
            throw new InvalidOperationException("Expected variable to be created");
        }

        if (variable.Type is not { } variableType)
        {
            _errors.Add(TypeCheckerError.UnresolvedInferredVariableType(variable.Name));
            return;
        }
        
        CheckTypeReferenceIsResolved(variableType, variableDeclarationExpression, expectedGenerics);
    }

    private void CheckTypeReferenceIsResolved(TypeChecker.ITypeReference typeReference, IExpression expression,
        HashSet<TypeChecker.GenericTypeReference> expectedGenerics)
    {
        switch (typeReference)
        {
            case TypeChecker.InstantiatedClass { TypeArguments: { Count: > 0 } classTypeArguments }:
            {
                foreach (var argument in classTypeArguments)
                {
                    CheckTypeReferenceIsResolved(argument, expression, expectedGenerics);
                }

                break;
            }
            case TypeChecker.InstantiatedUnion { TypeArguments: { Count: > 0 } unionTypeArguments }:
            {
                foreach (var argument in unionTypeArguments)
                {
                    CheckTypeReferenceIsResolved(argument, expression, expectedGenerics);
                }

                break;
            }
            case TypeChecker.InstantiatedFunction { TypeArguments: { Count: > 0 } functionTypeArguments }:
            {
                foreach (var argument in functionTypeArguments)
                {
                    CheckTypeReferenceIsResolved(argument, expression, expectedGenerics);
                }

                break;
            }
            case TypeChecker.GenericTypeReference genericTypeReference:
            {
                if (!expectedGenerics.Contains(genericTypeReference)
                    && genericTypeReference.ResolvedType is null
                    && _erroredGenerics.Add(genericTypeReference))
                {
                    _errors.Add(TypeCheckerError.UnresolvedInferredGenericType(expression, genericTypeReference.GenericName));
                }

                break;
            }
        }
    }

    private readonly HashSet<TypeChecker.GenericTypeReference> _erroredGenerics = [];
}