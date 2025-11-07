using Reef.Core.LoweredExpressions.New;

namespace Reef.Core.Tests;

public static class NewLoweredProgramHelpers
{
    public static NewLoweredProgram NewLoweredProgram(
        IReadOnlyList<NewLoweredMethod>? methods = null,
        IReadOnlyList<NewDataType>? types = null)
    {
        return new()
        {
            Methods = methods ?? [],
            DataTypes = types ?? []
        };
    }

    public static NewDataType NewDataType(
        string moduleId,
        string name,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<NewDataTypeVariant>? variants = null,
        IReadOnlyList<NewStaticDataTypeField>? staticFields = null)
    {
        var defId = new DefId(moduleId, $"{moduleId}.{name}");
        return new NewDataType(
            defId,
            name,
            [..(typeParameters ?? []).Select(x => new NewLoweredGenericPlaceholder(defId, x))],
            variants ?? [],
            staticFields ?? []);
    }

    public static NewDataTypeVariant NewVariant(string name, IReadOnlyList<NewDataTypeField>? fields = null)
    {
        return new NewDataTypeVariant(
            name,
            fields ?? []);
    }

    public static NewDataTypeField NewField(string name, INewLoweredTypeReference type)
    {
        return new NewDataTypeField(name, type);
    }

    public static NewStaticDataTypeField NewStaticField(
        string name,
        INewLoweredTypeReference type,
        IReadOnlyList<BasicBlock> initializerBasicBlocks,
        IReadOnlyList<NewMethodLocal> initializerLocals,
        NewMethodLocal initializerReturnValueLocal)
    {
        return new NewStaticDataTypeField(name, type, initializerBasicBlocks, initializerLocals,
            initializerReturnValueLocal);
    }

    public static NewLoweredMethod NewMethod(
        DefId id,
        string name,
        IReadOnlyList<BasicBlock> basicBlocks,
        INewLoweredTypeReference returnType,
        IReadOnlyList<(DefId, string)>? typeParameters = null,
        IReadOnlyList<(string, INewLoweredTypeReference)>? parameters = null,
        List<NewMethodLocal>? locals = null)
    {
        return new NewLoweredMethod(
            id,
            name,
            [..(typeParameters ?? []).Select(x => new NewLoweredGenericPlaceholder(x.Item1, x.Item2))],
            basicBlocks,
            new NewMethodLocal("_returnValue", null, returnType),
            [..(parameters ?? []).Select((x, i) => new NewMethodLocal($"_param{i}", x.Item1, x.Item2))],
            locals ?? []);
    }
    
    public static NewLoweredConcreteTypeReference BooleanT { get; }
        = new (
            TypeChecking.TypeChecker.ClassSignature.Boolean.Name,
            TypeChecking.TypeChecker.ClassSignature.Boolean.Id,
            []);
    
    public static NewLoweredConcreteTypeReference Unit { get; }
        = new (
            TypeChecking.TypeChecker.ClassSignature.Unit.Name,
            TypeChecking.TypeChecker.ClassSignature.Unit.Id,
            []);
    
    public static NewLoweredConcreteTypeReference StringT { get; }
        = new (
            TypeChecking.TypeChecker.ClassSignature.String.Name,
            TypeChecking.TypeChecker.ClassSignature.String.Id,
            []);
    
    public static NewLoweredConcreteTypeReference Int32T { get; }
        = new (
            TypeChecking.TypeChecker.ClassSignature.Int32.Name,
            TypeChecking.TypeChecker.ClassSignature.Int32.Id,
            []);
    
    public static NewLoweredConcreteTypeReference Int64T { get; }
        = new (
            TypeChecking.TypeChecker.ClassSignature.Int64.Name,
            TypeChecking.TypeChecker.ClassSignature.Int64.Id,
            []);
    
    public static NewLoweredConcreteTypeReference UInt16T { get; }
        = new (
            TypeChecking.TypeChecker.ClassSignature.UInt16.Name,
            TypeChecking.TypeChecker.ClassSignature.UInt16.Id,
            []);

    public static NewLoweredConcreteTypeReference Tuple(params IReadOnlyList<INewLoweredTypeReference> types)
    {
        var signature = TypeChecking.TypeChecker.ClassSignature.Tuple((ushort)types.Count);
        return new NewLoweredConcreteTypeReference(
            signature.Name,
            signature.Id,
            types);
    }
}