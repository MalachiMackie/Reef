using Reef.Core.LoweredExpressions;

namespace Reef.Core.Tests;

public static class LoweredProgramHelpers
{
    public static LoweredProgram LoweredProgram(
            IReadOnlyList<DataType>? types = null)
    {
        return new()
        {
            Methods = [],
            DataTypes = types ?? []
        };
    }

    public static DataType DataType(
            string name,
            IReadOnlyList<string>? typeParameters = null,
            IReadOnlyList<DataTypeVariant>? variants = null,
            IReadOnlyList<DataTypeMethod>? methods = null,
            IReadOnlyList<StaticDataTypeField>? staticFields = null)
    {
        return new(
                Guid.Empty,
                name,
                typeParameters?.Select(x => new LoweredGenericPlaceholder(Guid.Empty, x)).ToArray() ?? [],
                variants ?? [],
                methods ?? [],
                staticFields ?? []);
    }

    public static DataTypeVariant Variant(string name, IReadOnlyList<DataTypeField>? fields = null)
    {
        return new(
                name,
                fields ?? []);
    }

    public static DataTypeField Field(string name, ILoweredTypeReference type)
    {
        return new(name, type);
    }

    public static StaticDataTypeField StaticField(
            string name,
            ILoweredTypeReference type,
            IReadOnlyList<ILoweredExpression> staticInitializer)
    {
        return new(name, type, staticInitializer);
    }

    public static DataTypeMethod DataTypeMethod(
            string name,
            IReadOnlyList<string> typeParameters,
            IReadOnlyList<ILoweredTypeReference> parameters,
            ILoweredTypeReference returnType,
            IReadOnlyList<ILoweredExpression> expressions)
    {
        return new(
                Guid.Empty,
                name,
                typeParameters.Select(x => new LoweredGenericPlaceholder(Guid.Empty, x)).ToArray(),
                parameters,
                returnType,
                expressions);
    }

    public static DataTypeMethod DataTypeMethod(
            string name,
            IReadOnlyList<string> typeParameters,
            IReadOnlyList<ILoweredTypeReference> parameters,
            ILoweredTypeReference returnType,
            CompilerImplementationType compilerImplementationType)
    {
        return new(
                Guid.Empty,
                name,
                typeParameters.Select(x => new LoweredGenericPlaceholder(Guid.Empty, x)).ToArray(),
                parameters,
                returnType,
                Expressions: [],
                compilerImplementationType);
    }

    public static StringConstantExpression StringConstant(string value, bool valueUseful) => 
        new StringConstantExpression(valueUseful, value);

    public static UnitConstantExpression UnitConstant(bool valueUseful) => new UnitConstantExpression(valueUseful);

    public static MethodReturnExpression MethodReturn(ILoweredExpression value)
    {
        return new(value);
    }

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
