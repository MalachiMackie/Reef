
using System.Diagnostics;

namespace NewLang.Core;

// todo: this should probably be able to be a visitor
public static class ResolvedTypeChecker
{
    public static void CheckAllExpressionsHaveResolvedTypes(LangProgram program)
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

    private static void CheckUnion(ProgramUnion union)
    {
        IReadOnlyList<string> unionGenerics = [..union.GenericArguments.Select(x => x.StringValue)];
        foreach (var function in union.Functions)
        {
            CheckFunction(function, unionGenerics);
        }
    }

    private static void CheckClass(ProgramClass programClass)
    {
        IReadOnlyList<string> classGenerics = [..programClass.TypeArguments.Select(x => x.StringValue)];
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
    
    private static void CheckFunction(LangFunction function, IReadOnlyList<string> expectedGenerics)
    {
        IReadOnlyList<string> innerGenerics = [..function.TypeArguments.Select(x => x.StringValue), ..expectedGenerics];
        foreach (var blockFunction in function.Block.Functions)
        {
            CheckFunction(blockFunction, innerGenerics);
        }

        foreach (var expression in function.Block.Expressions)
        {
            CheckExpression(expression, innerGenerics);
        }
    }

    private static void CheckExpression(IExpression expression, IReadOnlyList<string> expectedGenerics)
    {
        if (expression.ResolvedType is null)
        {
            throw new UnreachableException("Every expression should be type checked");
        }
        CheckTypeReference(expression.ResolvedType, expression, expectedGenerics);
        
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
            case UnionStructVariantInitializerExpression unionStructVariantInitializerExpression: 
                CheckUnionStructInitializer(unionStructVariantInitializerExpression.UnionInitializer, expectedGenerics);
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

    private static void CheckMatchExpression(MatchExpression matchExpression, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(matchExpression.Value, expectedGenerics);
        foreach (var arm in matchExpression.Arms)
        {
            CheckExpression(arm.Expression, expectedGenerics);
        }
    }

    private static void CheckTupleExpression(TupleExpression tupleExpression, IReadOnlyList<string> expectedGenerics)
    {
        foreach (var element in tupleExpression.Values)
        {
            CheckExpression(element, expectedGenerics);
        }
    }

    private static void CheckMatchesExpression(MatchesExpression matchesExpression, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(matchesExpression.ValueExpression, expectedGenerics);
    }

    private static void CheckUnionStructInitializer(UnionStructVariantInitializer unionInitializer, IReadOnlyList<string> expectedGenerics)
    {
        foreach (var initializer in unionInitializer.FieldInitializers)
        {
            CheckExpression(initializer.Value, expectedGenerics);
        }
    }

    private static void CheckUnaryOperator(UnaryOperator unaryOperator, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(unaryOperator.Operand, expectedGenerics);
    }

    private static void CheckGenericInstantiation(GenericInstantiation genericInstantiation, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(genericInstantiation.Value, expectedGenerics);
    }

    private static void CheckMemberAccess(MemberAccess memberAccess, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(memberAccess.Owner, expectedGenerics);
    }

    private static void CheckObjectInitializer(ObjectInitializer objectInitializer, IReadOnlyList<string> expectedGenerics)
    {
        foreach (var initializer in objectInitializer.FieldInitializers)
        {
            CheckExpression(initializer.Value, expectedGenerics);
        }
    }

    private static void CheckBinaryOperatorExpression(BinaryOperator binaryOperator, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(binaryOperator.Left, expectedGenerics);
        CheckExpression(binaryOperator.Right, expectedGenerics);
    }

    private static void CheckIfExpression(IfExpression ifExpression, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(ifExpression.CheckExpression, expectedGenerics);
        CheckExpression(ifExpression.Body, expectedGenerics);
        foreach (var elseIf in ifExpression.ElseIfs)
        {
            CheckExpression(elseIf.CheckExpression, expectedGenerics);
            CheckExpression(elseIf.Body, expectedGenerics);
        }

        if (ifExpression.ElseBody is not null)
        {
            CheckExpression(ifExpression.ElseBody, expectedGenerics);
        }
    }

    private static void CheckBlock(Block blockExpressionBlock, IReadOnlyList<string> expectedGenerics)
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

    private static void CheckMethodCall(MethodCall methodCall, IReadOnlyList<string> expectedGenerics)
    {
        CheckExpression(methodCall.Method, expectedGenerics);
        foreach (var parameter in methodCall.ParameterList)
        {
            CheckExpression(parameter, expectedGenerics);
        }
    }

    private static void CheckMethodReturn(MethodReturnExpression methodReturnExpression, IReadOnlyList<string> expectedGenerics)
    {
        if (methodReturnExpression.MethodReturn.Expression is not null)
        {
            CheckExpression(methodReturnExpression.MethodReturn.Expression, expectedGenerics);
        }
    }

    private static void CheckVariableDeclaration(VariableDeclarationExpression variableDeclarationExpression, IReadOnlyList<string> expectedGenerics)
    {
        if (variableDeclarationExpression.VariableDeclaration.Value is {} value)
        {
            CheckExpression(value, expectedGenerics);
        }
    }

    private static void CheckTypeReference(TypeChecker.ITypeReference typeReference, IExpression expression, IReadOnlyList<string> expectedGenerics)
    {
        switch (typeReference)
        {
            case TypeChecker.InstantiatedClass { TypeArguments: { Count: > 0 } classTypeArguments }:
            {
                foreach (var argument in classTypeArguments)
                {
                    CheckTypeReference(argument, expression, expectedGenerics);
                }
                break;
            }
            case TypeChecker.InstantiatedUnion { TypeArguments: { Count: > 0 } unionTypeArguments }:
            {
                foreach (var argument in unionTypeArguments)
                {
                    CheckTypeReference(argument, expression, expectedGenerics);
                }
                break;
            }
            case TypeChecker.InstantiatedFunction { TypeArguments: { Count: > 0 } functionTypeArguments }:
            {
                foreach (var argument in functionTypeArguments)
                {
                    CheckTypeReference(argument, expression, expectedGenerics);
                }

                break;
            }
            case TypeChecker.GenericTypeReference genericTypeReference:
            {
                if (expectedGenerics.Contains(genericTypeReference.GenericName))
                {
                    if (genericTypeReference.ResolvedType is not null)
                    {
                        throw new Exception("Should not resolve an expected generic type");
                    }
                }
                else
                {
                    if (genericTypeReference.ResolvedType is null)
                    {
                        throw new InvalidOperationException($"Could not infer type {genericTypeReference} for {expression}");
                    }
                }
                break;
            }
        }
    }
}