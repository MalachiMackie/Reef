using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;

namespace Reef.Core.Tests;

public static class LoweredProgramHelpers
{
    public static LoweredModule LoweredProgram(
        string moduleId,
        IReadOnlyList<LoweredMethod>? methods = null,
        IReadOnlyList<DataType>? types = null)
    {
        return new()
        {
            Id = moduleId,
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
        return new DataType(
            defId,
            name,
            [..(typeParameters ?? []).Select(x => new LoweredGenericPlaceholder(defId, x))],
            variants ?? [],
            staticFields ?? []);
    }

    public static DataTypeVariant Variant(string name, IReadOnlyList<DataTypeField>? fields = null)
    {
        return new DataTypeVariant(
            name,
            fields ?? []);
    }

    public static DataTypeField Field(string name, ILoweredTypeReference type)
    {
        return new DataTypeField(name, type);
    }

    public static StaticDataTypeField StaticField(
        string name,
        ILoweredTypeReference type,
        IReadOnlyList<BasicBlock> initializerBasicBlocks,
        IReadOnlyList<MethodLocal> initializerLocals)
    {
        return new StaticDataTypeField(name, type, initializerBasicBlocks, initializerLocals,
            new MethodLocal("_returnValue", null, type));
    }

    public static readonly Local LocalsObject = new ("_localsObject");
    public static readonly Local Local0 = new ("_local0");
    public static readonly Local Local1 = new ("_local1");
    public static readonly Local Local2 = new ("_local2");
    public static readonly Local Local3 = new ("_local3");
    public static readonly Local ReturnValue = new ("_returnValue");
    
    public static readonly Local Param0 = new ("_param0");
    public static readonly Local Param1 = new ("_param1");
    public static readonly Local Param2 = new ("_param2");
    
    public static readonly BasicBlockId BB0 = new ("bb0");
    public static readonly BasicBlockId BB1 = new ("bb1");
    public static readonly BasicBlockId BB2 = new ("bb2");
    public static readonly BasicBlockId BB3 = new ("bb3");
    public static readonly BasicBlockId BB4 = new ("bb4");
    public static readonly BasicBlockId BB5 = new ("bb5");
    public static readonly BasicBlockId BB6 = new ("bb6");
    public static readonly BasicBlockId BB7 = new ("bb7");
    public static readonly BasicBlockId BB8 = new ("bb8");
    public static readonly BasicBlockId BB9 = new ("bb9");
    public static readonly BasicBlockId BB10 = new ("bb10");
    public static readonly BasicBlockId BB11 = new ("bb11");
    public static readonly BasicBlockId BB12 = new ("bb12");
    

    public static LoweredConcreteTypeReference ConcreteTypeReference(string name, string moduleId, IReadOnlyList<ILoweredTypeReference>? typeArguments = null)
    {
        return new LoweredConcreteTypeReference(
            name,
            new DefId(moduleId, $"{moduleId}.{name}"),
            typeArguments ?? []);
    }

    public static MethodCall AllocateMethodCall(ILoweredTypeReference type, IPlace destination, BasicBlockId goTo)
    {
        return new MethodCall(
            new LoweredFunctionReference(DefId.Allocate, []),
            [new SizeOf(type)],
            destination,
            goTo);
    }

    public static LoweredMethod Method(
        DefId id,
        string name,
        IReadOnlyList<BasicBlock> basicBlocks,
        ILoweredTypeReference returnType,
        IReadOnlyList<(DefId, string)>? typeParameters = null,
        IReadOnlyList<(string, ILoweredTypeReference)>? parameters = null,
        List<MethodLocal>? locals = null)
    {
        return new LoweredMethod(
            id,
            name,
            [..(typeParameters ?? []).Select(x => new LoweredGenericPlaceholder(x.Item1, x.Item2))],
            basicBlocks,
            new MethodLocal("_returnValue", null, returnType),
            [..(parameters ?? []).Select((x, i) => new MethodLocal($"_param{i}", x.Item1, x.Item2))],
            locals ?? []);
    }
    
    public static LoweredConcreteTypeReference BooleanT { get; }
        = new (
            TypeChecker.ClassSignature.Boolean.Name,
            TypeChecker.ClassSignature.Boolean.Id,
            []);
    
    public static LoweredConcreteTypeReference Unit { get; }
        = new (
            TypeChecker.ClassSignature.Unit.Name,
            TypeChecker.ClassSignature.Unit.Id,
            []);
    
    public static LoweredConcreteTypeReference StringT { get; }
        = new (
            TypeChecker.ClassSignature.String.Name,
            TypeChecker.ClassSignature.String.Id,
            []);
    
    public static LoweredConcreteTypeReference Int32T { get; }
        = new (
            TypeChecker.ClassSignature.Int32.Name,
            TypeChecker.ClassSignature.Int32.Id,
            []);
    
    public static LoweredConcreteTypeReference Int64T { get; }
        = new (
            TypeChecker.ClassSignature.Int64.Name,
            TypeChecker.ClassSignature.Int64.Id,
            []);
    
    public static LoweredConcreteTypeReference UInt16T { get; }
        = new (
            TypeChecker.ClassSignature.UInt16.Name,
            TypeChecker.ClassSignature.UInt16.Id,
            []);

    public static LoweredConcreteTypeReference Tuple(params IReadOnlyList<ILoweredTypeReference> types)
    {
        var signature = TypeChecker.ClassSignature.Tuple((ushort)types.Count);
        return new LoweredConcreteTypeReference(
            signature.Name,
            signature.Id,
            types);
    }

    public static LoweredConcreteTypeReference FunctionObject(
        IReadOnlyList<ILoweredTypeReference> parameterTypes,
        ILoweredTypeReference returnType)
    {
        var signature = TypeChecker.ClassSignature.Function(parameterTypes.Count);

        return new LoweredConcreteTypeReference(
            signature.Name,
            signature.Id,
            [..parameterTypes, returnType]);
    }

    public static LoweredFunctionReference FunctionObjectCall(
        IReadOnlyList<ILoweredTypeReference> parameterTypes,
        ILoweredTypeReference returnType)
    {
        return new LoweredFunctionReference(
            DefId.FunctionObject_Call(parameterTypes.Count),
            [..parameterTypes, returnType]);
    }
}