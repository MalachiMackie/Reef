﻿using System.Diagnostics;
using Reef.Core.TypeChecking.PatternAnalysis;

namespace Reef.Core.TypeChecking;

// todo: this should probably be able to be a visitor
public class TypeTwoTypeChecker
{
    private readonly List<TypeCheckerError> _errors = [];
    private readonly Stack<Dictionary<TypeChecker.LocalVariable, bool>> _localVariables = [];

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
            CheckFunction(function);
        }

        foreach (var union in program.Unions)
        {
            CheckUnion(union);
        }

        foreach (var programClass in program.Classes)
        {
            CheckClass(programClass);
        }

        _localVariables.Push(program.TopLevelLocalVariables.ToDictionary(x => x, _ => false));

        foreach (var expression in program.Expressions)
        {
            CheckExpression(expression);
        }
    }

    private void CheckUnion(ProgramUnion union)
    {
        if (union.Signature is null)
        {
            throw new Exception("Union Signature was not created");
        }

        foreach (var function in union.Functions)
        {
            CheckFunction(function);
        }
    }

    private void CheckClass(ProgramClass programClass)
    {
        if (programClass.Signature is null)
        {
            throw new InvalidOperationException("Class signature was not created");
        }

        foreach (var function in programClass.Functions)
        {
            CheckFunction(function);
        }

        foreach (var expression in programClass.Fields
                     .Select(x => x.InitializerValue)
                     .Where(x => x is not null))
        {
            CheckExpression(expression!);
        }
    }

    private void CheckFunction(LangFunction function)
    {
        if (function.Signature is null)
        {
            throw new InvalidOperationException("Function signature was not created");
        }

        _localVariables.Push(function.Signature.LocalVariables.ToDictionary(x => x, _ => false));

        foreach (var blockFunction in function.Block.Functions)
        {
            CheckFunction(blockFunction);
        }

        foreach (var expression in function.Block.Expressions)
        {
            CheckExpression(expression);
        }
    }

    private void CheckExpression(IExpression expression)
    {
        if (expression.ResolvedType is null)
        {
            throw new UnreachableException("Every expression should be type checked");
        }

        CheckTypeReferenceIsResolved(expression.ResolvedType, expression);

        switch (expression)
        {
            case VariableDeclarationExpression variableDeclarationExpression:
                CheckVariableDeclaration(variableDeclarationExpression);
                break;
            case ValueAccessorExpression valueAccessorExpression:
            {
                CheckValueAccessor(valueAccessorExpression);
                break;
            }
            case MethodReturnExpression methodReturnExpression:
                CheckMethodReturn(methodReturnExpression);
                break;
            case MethodCallExpression methodCallExpression:
                CheckMethodCall(methodCallExpression.MethodCall);
                break;
            case BlockExpression blockExpression:
                CheckBlock(blockExpression.Block);
                break;
            case IfExpressionExpression ifExpressionExpression:
                CheckIfExpression(ifExpressionExpression.IfExpression);
                break;
            case BinaryOperatorExpression binaryOperatorExpression:
                CheckBinaryOperatorExpression(binaryOperatorExpression.BinaryOperator);
                break;
            case ObjectInitializerExpression objectInitializerExpression:
                CheckObjectInitializer(objectInitializerExpression.ObjectInitializer);
                break;
            case MemberAccessExpression memberAccessExpression:
                CheckMemberAccess(memberAccessExpression);
                break;
            case StaticMemberAccessExpression staticMemberAccessExpression:
                CheckStaticMemberAccess(staticMemberAccessExpression);
                break;
            case UnaryOperatorExpression unaryOperatorExpression:
                CheckUnaryOperator(unaryOperatorExpression.UnaryOperator);
                break;
            case UnionClassVariantInitializerExpression unionClassVariantInitializerExpression:
                CheckUnionClassVariantInitializer(unionClassVariantInitializerExpression.UnionInitializer);
                break;
            case MatchesExpression matchesExpression:
                CheckMatchesExpression(matchesExpression);
                break;
            case TupleExpression tupleExpression:
                CheckTupleExpression(tupleExpression);
                break;
            case MatchExpression matchExpression:
                CheckMatchExpression(matchExpression);
                break;
            default:
                throw new NotImplementedException($"{expression.ExpressionType}");
        }
    }

    private void CheckValueAccessor(ValueAccessorExpression valueAccessorExpression)
    {
        if (valueAccessorExpression.ValueAccessor.Token is StringToken nameToken
            && valueAccessorExpression.FunctionInstantiation is {} function)
        {
            var uninitializedAccessedVariables = function.AccessedOuterVariables
                .OfType<TypeChecker.LocalVariable>()
                .Where(x => !_localVariables.Peek()[x])
                .Select(x => x.Name)
                .ToArray();
            
            if (uninitializedAccessedVariables.Length > 0)
            {
                _errors.Add(TypeCheckerError.AccessingClosureWhichReferencesUninitializedVariables(nameToken, uninitializedAccessedVariables));
            }

            foreach (var functionTypeArgument in function.TypeArguments)
            {
                CheckTypeReferenceIsResolved(functionTypeArgument, valueAccessorExpression);
            }
        }
    }

    private void CheckMatchExpression(MatchExpression matchExpression)
    {
        CheckExpression(matchExpression.Value);
        foreach (var arm in matchExpression.Arms)
        {
            if (arm.Expression is not null)
            {
                CheckExpression(arm.Expression);
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

    private void CheckTupleExpression(TupleExpression tupleExpression)
    {
        foreach (var element in tupleExpression.Values)
        {
            CheckExpression(element);
        }
    }

    private void CheckMatchesExpression(MatchesExpression matchesExpression)
    {
        CheckExpression(matchesExpression.ValueExpression);
    }

    private void CheckUnionClassVariantInitializer(UnionClassVariantInitializer unionInitializer)
    {
        foreach (var initializer in unionInitializer.FieldInitializers)
        {
            if (initializer.Value is not null)
            {
                CheckExpression(initializer.Value);
            }
        }
    }

    private void CheckUnaryOperator(UnaryOperator unaryOperator)
    {
        if (unaryOperator.Operand is not null)
        {
            CheckExpression(unaryOperator.Operand);
        }
    }
    
    private void CheckStaticMemberAccess(StaticMemberAccessExpression staticMemberAccessExpression)
    {
        if (staticMemberAccessExpression.StaticMemberAccess.InstantiatedFunction is not { } function)
        {
            return;
        }

        foreach (var functionTypeArgument in function.TypeArguments)
        {
            CheckTypeReferenceIsResolved(functionTypeArgument, staticMemberAccessExpression);
        }
    }
    
    private void CheckMemberAccess(MemberAccessExpression memberAccessExpression)
    {
        CheckExpression(memberAccessExpression.MemberAccess.Owner);

        if (memberAccessExpression.MemberAccess.InstantiatedFunction is not { } function)
        {
            return;
        }

        foreach (var functionTypeArgument in function.TypeArguments)
        {
            CheckTypeReferenceIsResolved(functionTypeArgument, memberAccessExpression);
        }
    }

    private void CheckObjectInitializer(ObjectInitializer objectInitializer)
    {
        foreach (var initializer in objectInitializer.FieldInitializers)
        {
            if (initializer.Value is not null)
            {
                CheckExpression(initializer.Value);
            }
        }
    }

    private void CheckBinaryOperatorExpression(BinaryOperator binaryOperator)
    {
        if (binaryOperator.Left is not null)
        {
            CheckExpression(binaryOperator.Left);
        }

        if (binaryOperator.Right is not null)
        {
            CheckExpression(binaryOperator.Right);
        }

        if (binaryOperator is
            {
                OperatorType: BinaryOperatorType.ValueAssignment,
                Left: ValueAccessorExpression{ReferencedVariable: TypeChecker.LocalVariable localVariable }
            })
        {
            _localVariables.Peek()[localVariable] = true;
        }
    }

    private void CheckIfExpression(IfExpression ifExpression)
    {
        var uninitializedLocalVariables = _localVariables.Peek().Where(x => !x.Value)
            .Select(x => x.Key)
            .ToDictionary(x => x, _ => new TypeChecker.VariableIfInstantiation());
        
        CheckExpression(ifExpression.CheckExpression);
        if (ifExpression.Body is not null)
        {
            CheckExpression(ifExpression.Body);
        }

        foreach (var (variable, variableIfInstantiation) in uninitializedLocalVariables)
        {
            variableIfInstantiation.InstantiatedInBody = _localVariables.Peek()[variable];
            _localVariables.Peek()[variable] = false;
        }
        
        foreach (var elseIf in ifExpression.ElseIfs)
        {
            CheckExpression(elseIf.CheckExpression);
            if (elseIf.Body is not null)
            {
                CheckExpression(elseIf.Body);
            }
            
            foreach (var (variable, variableInstantiation) in uninitializedLocalVariables)
            {
                variableInstantiation.InstantiatedInEachElseIf &= _localVariables.Peek()[variable];
                _localVariables.Peek()[variable] = false;
            }
        }

        if (ifExpression.ElseBody is not null)
        {
            CheckExpression(ifExpression.ElseBody);
            
            foreach (var (variable, variableInstantiation) in uninitializedLocalVariables)
            {
                variableInstantiation.InstantiatedInEachElseIf &= _localVariables.Peek()[variable];
                _localVariables.Peek()[variable] = false;
            }
        }
        
        foreach (var (variable, variableInstantiation) in uninitializedLocalVariables)
        {
            _localVariables.Peek()[variable] = ifExpression.Body is not null && variableInstantiation.InstantiatedInBody
                                                                             && ifExpression.ElseBody is not null &&
                                                                             variableInstantiation.InstantiatedInElse
                                                                             && (ifExpression.ElseIfs.Count == 0 ||
                                                                                 variableInstantiation
                                                                                     .InstantiatedInEachElseIf);
        }
    }

    private void CheckBlock(Block blockExpressionBlock)
    {
        foreach (var function in blockExpressionBlock.Functions)
        {
            CheckFunction(function);
        }

        foreach (var expression in blockExpressionBlock.Expressions)
        {
            CheckExpression(expression);
        }
    }

    private void CheckMethodCall(MethodCall methodCall)
    {
        CheckExpression(methodCall.Method);
        foreach (var parameter in methodCall.ArgumentList)
        {
            CheckExpression(parameter);
        }
    }

    private void CheckMethodReturn(MethodReturnExpression methodReturnExpression)
    {
        if (methodReturnExpression.MethodReturn.Expression is not null)
        {
            CheckExpression(methodReturnExpression.MethodReturn.Expression);
        }
    }

    private void CheckVariableDeclaration(VariableDeclarationExpression variableDeclarationExpression)
    {
        if (variableDeclarationExpression.VariableDeclaration.Value is { } value)
        {
            if (variableDeclarationExpression.VariableDeclaration.Variable is TypeChecker.LocalVariable localVariable)
            {
                _localVariables.Peek()[localVariable] = true;
            }
            CheckExpression(value);
        }

        if (variableDeclarationExpression.VariableDeclaration.Variable is not { } variable)
        {
            throw new InvalidOperationException("Expected variable to be created");
        }

        if (variable.Type is TypeChecker.UnknownInferredType{ResolvedType: null})
        {
            _errors.Add(TypeCheckerError.UnresolvedInferredVariableType(variable.Name));
            return;
        }
        
        CheckTypeReferenceIsResolved(variable.Type, variableDeclarationExpression);
    }

    private void CheckTypeReferenceIsResolved(TypeChecker.ITypeReference typeReference, IExpression expression)
    {
        switch (typeReference)
        {
            case TypeChecker.InstantiatedClass { TypeArguments: { Count: > 0 } classTypeArguments }:
            {
                foreach (var argument in classTypeArguments)
                {
                    CheckTypeReferenceIsResolved(argument, expression);
                }

                break;
            }
            case TypeChecker.InstantiatedUnion { TypeArguments: { Count: > 0 } unionTypeArguments }:
            {
                foreach (var argument in unionTypeArguments)
                {
                    CheckTypeReferenceIsResolved(argument, expression);
                }

                break;
            }
            case TypeChecker.GenericTypeReference genericTypeReference:
            {
                if (genericTypeReference.ResolvedType is null
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
