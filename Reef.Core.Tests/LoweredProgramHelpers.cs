using System.Diagnostics;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;

namespace Reef.Core.Tests;

public static class LoweredProgramHelpers
{
    public static LoweredProgram LoweredProgram(
        ModuleId moduleId,
        IReadOnlyList<LoweredMethod>? methods = null,
        IReadOnlyList<DataType>? types = null)
    {
        return new()
        {
            Methods = methods ?? [],
            DataTypes = types ?? []
        };
    }

    public static DataType DataType(
        ModuleId moduleId,
        string name,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<DataTypeVariant>? variants = null,
        IReadOnlyList<StaticDataTypeField>? staticFields = null)
    {
        var defId = new DefId(moduleId, $"{moduleId}:::{name}");
        return new DataType(
            defId,
            name,
            [.. (typeParameters ?? []).Select(x => new LoweredGenericPlaceholder(defId, x))],
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

    public static readonly Local LocalsObject = new("_localsObject");
    public static readonly Local Local0 = new("_local0");
    public static readonly Local Local1 = new("_local1");
    public static readonly Local Local2 = new("_local2");
    public static readonly Local Local3 = new("_local3");
    public static readonly Local ReturnValue = new("_returnValue");

    public static readonly Local Param0 = new("_param0");
    public static readonly Local Param1 = new("_param1");
    public static readonly Local Param2 = new("_param2");

    public static readonly BasicBlockId BB0 = new("bb0");
    public static readonly BasicBlockId BB1 = new("bb1");
    public static readonly BasicBlockId BB2 = new("bb2");
    public static readonly BasicBlockId BB3 = new("bb3");
    public static readonly BasicBlockId BB4 = new("bb4");
    public static readonly BasicBlockId BB5 = new("bb5");
    public static readonly BasicBlockId BB6 = new("bb6");
    public static readonly BasicBlockId BB7 = new("bb7");
    public static readonly BasicBlockId BB8 = new("bb8");
    public static readonly BasicBlockId BB9 = new("bb9");
    public static readonly BasicBlockId BB10 = new("bb10");
    public static readonly BasicBlockId BB11 = new("bb11");
    public static readonly BasicBlockId BB12 = new("bb12");

    public static IEnumerable<IStatement> CreateBoxedObject(
        IPlace destination,
        LoweredConcreteTypeReference type)
    {
        Debug.Assert(destination is Deref, destination.GetType().ToString());

        return [
            new Assign(
                destination,
                new CreateObject(BoxedValue(type))
            ),
            new Assign(
                new Field(destination, "ObjectHeader", "_classVariant"),
                new CreateObject(ObjectHeader)),
            new Assign(
                new Field(new Field(destination, "ObjectHeader", "_classVariant"), "TypeId", "_classVariant"),
                new Use(new TypeIdOf(type))),
            new Assign(
                new Field(destination, "Value", "_classVariant"),
                new CreateObject(type))
        ];
    }

    public static IEnumerable<IStatement> CreateBoxedArray(
        IPlace destination,
        ILoweredTypeReference elementType,
        uint length)
    {
        Debug.Assert(destination is Deref);
        var arrayType = new LoweredArray(elementType, length);

        return [
            new Assign(
                destination,
                new CreateObject(BoxedValue(arrayType))
            ),
            new Assign(
                new Field(destination, "ObjectHeader", "_classVariant"),
                new CreateObject(ObjectHeader)),
            new Assign(
                new Field(new Field(destination, "ObjectHeader", "_classVariant"), "TypeId", "_classVariant"),
                new Use(new TypeIdOf(arrayType))),
            new Assign(
                new Field(destination, "Value", "_classVariant"),
                new CreateArray(arrayType)),
            new Assign(
                new Field(new Field(destination, "Value", "_classVariant"), "Length", "_classVariant"),
                new Use(new UIntConstant(length, 8))
            ),
        ];
    }

    public static LoweredConcreteTypeReference BoxedValue(ILoweredTypeReference value)
    {
        return new LoweredConcreteTypeReference(
            DefId.BoxedValue,
            [value]
        );
    }

    public static LoweredConcreteTypeReference ObjectHeader { get; } = new LoweredConcreteTypeReference(DefId.ObjectHeader, []);

    public static LoweredConcreteTypeReference ConcreteTypeReference(string name, ModuleId moduleId, IReadOnlyList<ILoweredTypeReference>? typeArguments = null)
    {
        return new LoweredConcreteTypeReference(
            new DefId(moduleId, $"{moduleId}:::{name}"),
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
            [.. (typeParameters ?? []).Select(x => new LoweredGenericPlaceholder(x.Item1, x.Item2))],
            basicBlocks,
            new MethodLocal("_returnValue", null, returnType),
            [.. (parameters ?? []).Select((x, i) => new MethodLocal($"_param{i}", x.Item1, x.Item2))],
            locals ?? []);
    }

    public static LoweredConcreteTypeReference BooleanT { get; }
        = new(
            DefId.Boolean,
            []);

    public static LoweredConcreteTypeReference Unit { get; }
        = new(
            DefId.Unit,
            []);

    public static LoweredPointer StringT { get; }
        = new(BoxedValue(new LoweredConcreteTypeReference(DefId.String, [])));

    public static LoweredConcreteTypeReference Int32T { get; }
        = new(
            DefId.Int32,
            []);

    public static LoweredConcreteTypeReference Int64T { get; }
        = new(
            DefId.Int64,
            []);

    public static LoweredConcreteTypeReference UInt16T { get; }
        = new(
            DefId.UInt16,
            []);

    public static LoweredConcreteTypeReference Tuple(params IReadOnlyList<ILoweredTypeReference> types)
    {
        return new LoweredConcreteTypeReference(
            DefId.Tuple(types.Count),
            types);
    }

    public static LoweredConcreteTypeReference FunctionObject(
        IReadOnlyList<ILoweredTypeReference> parameterTypes,
        ILoweredTypeReference returnType)
    {
        return new LoweredConcreteTypeReference(
            DefId.FunctionObject(parameterTypes.Count),
            [.. parameterTypes, returnType]);
    }

    public static LoweredFunctionReference FunctionObjectCall(
        IReadOnlyList<ILoweredTypeReference> parameterTypes,
        ILoweredTypeReference returnType)
    {
        return new LoweredFunctionReference(
            DefId.FunctionObject_Call(parameterTypes.Count),
            [.. parameterTypes, returnType]);
    }
}
