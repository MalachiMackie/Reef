using Reef.Core.LoweredExpressions.New;

namespace Reef.Core.Tests;

public static class NewLoweredProgramHelpers
{
    public static NewLoweredProgram NewLoweredProgram(
        IReadOnlyList<NewLoweredMethod> methods,
        IReadOnlyList<NewDataType>? dataTypes = null)
    {
        return new()
        {
            Methods = methods,
            DataTypes = dataTypes ?? []
        };
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
}