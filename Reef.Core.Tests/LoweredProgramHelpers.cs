using Reef.Core.LoweredExpressions;

namespace Reef.Core.Tests;

public static class LoweredProgramHelpers
{
    public static LoweredProgram LoweredProgram(
            IReadOnlyList<DataType>? types = null,
            IReadOnlyList<LoweredMethod>? methods = null)
    {
        return new()
        {
            Methods = methods ?? [],
            DataTypes = types ?? []
        };
    }

    public static DataType DataType(
            string name,
            IReadOnlyList<string>? typeParameters = null,
            IReadOnlyList<DataTypeVariant>? variants = null,
            IReadOnlyList<StaticDataTypeField>? staticFields = null)
    {
        return new(
                Guid.Empty,
                name,
                typeParameters?.Select(x => new LoweredGenericPlaceholder(Guid.Empty, x)).ToArray() ?? [],
                variants ?? [],
                staticFields ?? []);
    }

    public static DataTypeVariant Variant(string name, IReadOnlyList<DataTypeField>? fields = null)
    {
        return new(
                name,
                fields ?? []);
    }

    public static CreateObjectExpression CreateObject(
            LoweredConcreteTypeReference type,
            string variant,
            bool valueUseful,
            Dictionary<string, ILoweredExpression>? fieldInitializers = null)
    {
        return new(type,
                variant,
                valueUseful,
                fieldInitializers ?? []);
    }

    public static DataTypeField Field(string name, ILoweredTypeReference type)
    {
        return new(name, type);
    }

    public static StaticDataTypeField StaticField(
            string name,
            ILoweredTypeReference type,
            ILoweredExpression staticInitializer)
    {
        return new(name, type, staticInitializer);
    }

    public static LoweredMethod Method(
            string name,
            IReadOnlyList<ILoweredExpression> expressions,
            IReadOnlyList<string>? typeParameters = null,
            IReadOnlyList<ILoweredTypeReference>? parameters = null,
            ILoweredTypeReference? returnType = null,
            List<MethodLocal>? locals = null)
    {
        return new(
                Guid.Empty,
                name,
                typeParameters?.Select(x => new LoweredGenericPlaceholder(Guid.Empty, x)).ToArray() ?? [],
                parameters ?? [],
                returnType ?? Unit,
                expressions ?? [],
                locals ?? []);
    }

    public static StringConstantExpression StringConstant(string value, bool valueUseful) =>
        new(valueUseful, value);

    public static IntConstantExpression IntConstant(int value, bool valueUseful) =>
        new(valueUseful, value);

    public static UnitConstantExpression UnitConstant(bool valueUseful) => new(valueUseful);

    public static MethodReturnExpression MethodReturn(ILoweredExpression value)
    {
        return new(value);
    }

    public static MethodReturnExpression MethodReturnUnit()
    {
        return new(UnitConstant(true));
    }

    public static VariableDeclarationAndAssignmentExpression VariableDeclaration(
            string name,
            ILoweredExpression value,
            bool valueUseful)
    {
        return new(
                name,
                value,
                valueUseful);
    }

    public static VariableDeclarationExpression VariableDeclaration(
            string name,
            bool valueUseful)
    {
        return new(
                name,
                valueUseful);
    }

    public static LocalAssignmentExpression LocalValueAssignment(
            string name,
            ILoweredExpression value,
            bool valueUseful,
            ILoweredTypeReference resolvedValue)
    {
        return new(
                name,
                value,
                resolvedValue,
                valueUseful);
    }

    public static IntPlusExpression IntPlus(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }

    public static IntMinusExpression IntMinus(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static IntMultiplyExpression IntMultiply(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static IntDivideExpression IntDivide(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static IntGreaterThanExpression IntGreaterThan(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static IntLessThanExpression IntLessThan(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static IntEqualsExpression IntEquals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static BoolAndExpression BoolAnd(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static BoolOrExpression BoolOr(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static BoolNotExpression BoolNot(
            ILoweredExpression operand, bool valueUseful)
    {
        return new(valueUseful, operand);
    }
    public static MethodLocal Local(string name, ILoweredTypeReference type)
    {
        return new(name, type);
    }

    public static BoolConstantExpression BoolConstant(bool value, bool valueUseful)
    {
        return new(valueUseful, value);
    }

    public static LoadArgumentExpression LoadArgument(uint argumentIndex, bool valueUseful, ILoweredTypeReference resolvedType)
    {
        return new LoadArgumentExpression(argumentIndex, valueUseful, resolvedType);
    }

    public static LocalVariableAccessor LocalAccess(
            string localName, bool valueUseful, ILoweredTypeReference resolvedType)
    {
        return new(localName, valueUseful, resolvedType);
    }

    public static FieldAccessExpression FieldAccess(
            ILoweredExpression memberOwner,
            string fieldName,
            string variantName,
            bool valueUseful,
            ILoweredTypeReference resolvedType)
    {
        return new(memberOwner, fieldName, variantName, valueUseful, resolvedType);
    }

    public static StaticFieldAccessExpression StaticFieldAccess(
        LoweredConcreteTypeReference ownerType,
        string fieldName,
        bool valueUseful,
        ILoweredTypeReference resolvedType)
    {
        return new(ownerType, fieldName, valueUseful, resolvedType);
    }

    public static MethodCallExpression MethodCall(
            LoweredFunctionReference functionReference,
            IReadOnlyList<ILoweredExpression> arguments,
            bool valueUseful,
            ILoweredTypeReference resolvedType)
    {
        return new(functionReference, arguments, valueUseful, resolvedType);
    }

    public static FunctionReferenceConstantExpression FunctionReferenceConstant(
        LoweredFunctionReference functionReference,
        bool valueUseful,
        LoweredFunctionType functionType)
    {
        return new(
                functionReference,
                valueUseful,
                functionType);
    }

    public static LoweredFunctionType FunctionType(
        IReadOnlyList<ILoweredTypeReference> parameterType,
        ILoweredTypeReference returnType)
    {
        return new(
            parameterType,
            returnType);
    }

    public static LoweredFunctionReference FunctionReference(
            string name,
            IReadOnlyList<ILoweredTypeReference>? typeArguments = null)
    {
        return new(name, Guid.Empty, typeArguments ?? []);
    }


    public static BlockExpression Block(
            IReadOnlyList<ILoweredExpression> expressions,
            ILoweredTypeReference resolvedType,
            bool valueUseful)
    {
        return new(expressions, resolvedType, valueUseful);
    }

    public static UnreachableExpression Unreachable()
    {
        return new();
    }

    public static FieldAssignmentExpression FieldAssignment(
            ILoweredExpression fieldOwnerExpression,
            string variantName,
            string fieldName,
            ILoweredExpression fieldValue,
            bool valueUseful,
            ILoweredTypeReference resolvedType)
    {
        return new(fieldOwnerExpression, variantName, fieldName, fieldValue, valueUseful, resolvedType);
    }

    public static StaticFieldAssignmentExpression StaticFieldAssignment(
            LoweredConcreteTypeReference ownerType,
            string fieldName,
            ILoweredExpression fieldValue,
            bool valueUseful,
            ILoweredTypeReference resolvedType)
    {
        return new(ownerType, fieldName, fieldValue, valueUseful, resolvedType);
    }

    public static SwitchIntExpression SwitchInt(
        ILoweredExpression checkExpression,
        Dictionary<int, ILoweredExpression> results,
        ILoweredExpression otherwise,
        bool valueUseful,
        ILoweredTypeReference resolvedType)
    {
        return new(checkExpression, results, otherwise, valueUseful, resolvedType);
    }

    public static CastBoolToIntExpression CastBoolToInt(
        ILoweredExpression intExpression, bool valueUseful)
    {
        return new(intExpression, valueUseful);
    }

    public static LoweredConcreteTypeReference BooleanType { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Boolean.Name,
                TypeChecking.TypeChecker.ClassSignature.Boolean.Id,
                []);

    public static LoweredConcreteTypeReference Int { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Int.Name,
                TypeChecking.TypeChecker.ClassSignature.Int.Id,
                []);

    public static LoweredConcreteTypeReference Unit { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Unit.Name,
                TypeChecking.TypeChecker.ClassSignature.Unit.Id,
                []);

    public static LoweredConcreteTypeReference StringType { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.String.Name,
                TypeChecking.TypeChecker.ClassSignature.String.Id,
                []);

    public static LoweredConcreteTypeReference ConcreteTypeReference(
        string name, IReadOnlyList<ILoweredTypeReference>? typeArguments = null
    ) => new(name, Guid.Empty, typeArguments ?? []);

    public static LoweredGenericPlaceholder GenericPlaceholder(string name)
        => new(Guid.Empty, name);
}
