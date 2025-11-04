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
        string moduleId,
        string name,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<DataTypeVariant>? variants = null,
        IReadOnlyList<StaticDataTypeField>? staticFields = null)
    {
        var defId = new DefId(moduleId, $"{moduleId}.{name}");

        return new(
                defId,
                name,
                typeParameters?.Select(x => new LoweredGenericPlaceholder(defId, x)).ToArray() ?? [],
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
        DefId defId,
            string name,
            IReadOnlyList<ILoweredExpression> expressions,
            IReadOnlyList<(DefId, string)>? typeParameters = null,
            IReadOnlyList<ILoweredTypeReference>? parameters = null,
            ILoweredTypeReference? returnType = null,
            List<MethodLocal>? locals = null)
    {
        return new(
                defId,
                name,
                typeParameters?.Select(x => new LoweredGenericPlaceholder(x.Item1, x.Item2)).ToArray() ?? [],
                parameters ?? [],
                returnType ?? Unit,
                expressions ?? [],
                locals ?? []);
    }

    public static StringConstantExpression StringConstant(string value, bool valueUseful) =>
        new(valueUseful, value);

    public static Int64ConstantExpression Int64Constant(long value, bool valueUseful) =>
        new(valueUseful, value);
    public static Int32ConstantExpression Int32Constant(int value, bool valueUseful) =>
        new(valueUseful, value);
    public static Int16ConstantExpression Int16Constant(short value, bool valueUseful) =>
        new(valueUseful, value);
    public static Int8ConstantExpression Int8Constant(short value, bool valueUseful) =>
        new(valueUseful, value);
    public static UInt64ConstantExpression UInt64Constant(ulong value, bool valueUseful) =>
        new(valueUseful, value);
    public static UInt32ConstantExpression UInt32Constant(uint value, bool valueUseful) =>
        new(valueUseful, value);
    public static UInt16ConstantExpression UInt16Constant(ushort value, bool valueUseful) =>
        new(valueUseful, value);
    public static UInt8ConstantExpression UInt8Constant(byte value, bool valueUseful) =>
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

    public static NoopExpression Noop() => new();

    public static Int64PlusExpression Int64Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32PlusExpression Int32Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16PlusExpression Int16Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8PlusExpression Int8Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64PlusExpression UInt64Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32PlusExpression UInt32Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16PlusExpression UInt16Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8PlusExpression UInt8Plus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);

    public static Int64MinusExpression Int64Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32MinusExpression Int32Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16MinusExpression Int16Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8MinusExpression Int8Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64MinusExpression UInt64Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32MinusExpression UInt32Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16MinusExpression UInt16Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8MinusExpression UInt8Minus(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);

    public static Int64DivideExpression Int64Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32DivideExpression Int32Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16DivideExpression Int16Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8DivideExpression Int8Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64DivideExpression UInt64Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32DivideExpression UInt32Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16DivideExpression UInt16Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8DivideExpression UInt8Divide(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);

    public static Int64MultiplyExpression Int64Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32MultiplyExpression Int32Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16MultiplyExpression Int16Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8MultiplyExpression Int8Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64MultiplyExpression UInt64Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32MultiplyExpression UInt32Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16MultiplyExpression UInt16Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8MultiplyExpression UInt8Multiply(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);

    public static Int64GreaterThanExpression Int64GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32GreaterThanExpression Int32GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16GreaterThanExpression Int16GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8GreaterThanExpression Int8GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64GreaterThanExpression UInt64GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32GreaterThanExpression UInt32GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16GreaterThanExpression UInt16GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8GreaterThanExpression UInt8GreaterThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    
    public static Int64LessThanExpression Int64LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32LessThanExpression Int32LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16LessThanExpression Int16LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8LessThanExpression Int8LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64LessThanExpression UInt64LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32LessThanExpression UInt32LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16LessThanExpression UInt16LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8LessThanExpression UInt8LessThan(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    
    public static Int64EqualsExpression Int64Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static Int32EqualsExpression Int32Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static Int16EqualsExpression Int16Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static Int8EqualsExpression Int8Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static UInt64EqualsExpression UInt64Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static UInt32EqualsExpression UInt32Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static UInt16EqualsExpression UInt16Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }
    public static UInt8EqualsExpression UInt8Equals(
            ILoweredExpression left, ILoweredExpression right, bool valueUseful)
    {
        return new(valueUseful, left, right);
    }

    public static Int64NotEqualsExpression Int64NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int32NotEqualsExpression Int32NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int16NotEqualsExpression Int16NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static Int8NotEqualsExpression Int8NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt64NotEqualsExpression UInt64NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt32NotEqualsExpression UInt32NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt16NotEqualsExpression UInt16NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);
    public static UInt8NotEqualsExpression UInt8NotEquals(ILoweredExpression left, ILoweredExpression right, bool valueUseful) => new(valueUseful, left, right);

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
        LoweredFunctionPointer functionPointer)
    {
        return new(
                functionReference,
                valueUseful,
                functionPointer);
    }

    public static LoweredFunctionPointer FunctionType(
        IReadOnlyList<ILoweredTypeReference> parameterType,
        ILoweredTypeReference returnType)
    {
        return new(
            parameterType,
            returnType);
    }

    public static LoweredFunctionReference FunctionReference(
        DefId defId,
            string name,
            IReadOnlyList<ILoweredTypeReference>? typeArguments = null)
    {
        return new(name, defId, typeArguments ?? []);
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

    public static LoweredConcreteTypeReference Int64_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Int64.Name,
                TypeChecking.TypeChecker.ClassSignature.Int64.Id,
                []);
    public static LoweredConcreteTypeReference Int32_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Int32.Name,
                TypeChecking.TypeChecker.ClassSignature.Int32.Id,
                []);
    public static LoweredConcreteTypeReference Int16_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Int16.Name,
                TypeChecking.TypeChecker.ClassSignature.Int16.Id,
                []);
    public static LoweredConcreteTypeReference Int8_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.Int8.Name,
                TypeChecking.TypeChecker.ClassSignature.Int8.Id,
                []);
    public static LoweredConcreteTypeReference UInt64_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.UInt64.Name,
                TypeChecking.TypeChecker.ClassSignature.UInt64.Id,
                []);
    public static LoweredConcreteTypeReference UInt32_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.UInt32.Name,
                TypeChecking.TypeChecker.ClassSignature.UInt32.Id,
                []);
    public static LoweredConcreteTypeReference UInt16_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.UInt16.Name,
                TypeChecking.TypeChecker.ClassSignature.UInt16.Id,
                []);
    public static LoweredConcreteTypeReference UInt8_t { get; }
        = new LoweredConcreteTypeReference(
                TypeChecking.TypeChecker.ClassSignature.UInt8.Name,
                TypeChecking.TypeChecker.ClassSignature.UInt8.Id,
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
        string name, DefId defId, IReadOnlyList<ILoweredTypeReference>? typeArguments = null
    ) => new(name, defId, typeArguments ?? []);

    public static LoweredGenericPlaceholder GenericPlaceholder(DefId ownerDefId, string name)
        => new(ownerDefId, name);
}
