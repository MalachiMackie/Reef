using Reef.Core.IL;

namespace Reef.Core.Tests.ILCompilerTests;

public static class TestHelpers
{
    public static ReefMethod.Local Local(string name, IReefTypeReference type)
    {
        return new ReefMethod.Local { DisplayName = name, Type = type };
    }

    public static FunctionDefinitionReference FunctionDefinitionReference(DefId defId, string name, IReadOnlyList<IReefTypeReference>? typeArguments = null) => new()
    {
        Name = name,
        TypeArguments = typeArguments ?? [],
        DefinitionId = defId
    };
    
    public static StaticReefField StaticField(string name, IReefTypeReference type,
        IEnumerable<IInstruction> staticInitializer)
    {
        return new StaticReefField
        {
            Type = type,
            DisplayName = name,
            StaticInitializerInstructions = new InstructionList([..staticInitializer], [])
        };
    }

    public static ReefField Field(string name, IReefTypeReference type)
    {
        return new ReefField
        {
            Type = type,
            DisplayName = name,
        };
    }

    public static IReefTypeReference GenericTypeReference(DefId ownerDefId, string typeParameterName)
    {
        return new GenericReefTypeReference
        {
            TypeParameterName = typeParameterName,
            DefinitionId = ownerDefId
        };
    }

    public static ConcreteReefTypeReference StringType => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.String);
    public static ConcreteReefTypeReference Int64Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Int64);
    public static ConcreteReefTypeReference Int32Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Int32);
    public static ConcreteReefTypeReference Int16Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Int16);
    public static ConcreteReefTypeReference Int8Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Int8);
    public static ConcreteReefTypeReference UInt64Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.UInt64);
    public static ConcreteReefTypeReference UInt32Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.UInt32);
    public static ConcreteReefTypeReference UInt16Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.UInt16);
    public static ConcreteReefTypeReference UInt8Type => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.UInt8);
    public static ConcreteReefTypeReference UnitType => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Unit);
    public static ConcreteReefTypeReference BoolType => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Boolean);
    public static ReefField VariantIdentifierField => Field("_variantIdentifier", UInt16Type);
    
    private static ConcreteReefTypeReference GetTypeReference(TypeChecking.TypeChecker.ClassSignature klass)
    {
        return new ConcreteReefTypeReference
        {
            Name = klass.Name,
            TypeArguments = [],
            DefinitionId = klass.Id
        };
    }

    public static ConcreteReefTypeReference ConcreteTypeReference(DefId defId, string name, IReadOnlyList<IReefTypeReference>? typeArguments = null)
    {
        return new ConcreteReefTypeReference
        {
            Name = name,
            DefinitionId = defId,
            TypeArguments = typeArguments ?? []
        };
    }

    public static ReefMethod Method(
        DefId defId,
        string name,
        IEnumerable<IInstruction> instructions,
        IEnumerable<InstructionLabel>? labels = null,
        IReadOnlyList<IReefTypeReference>? parameters = null,
        IReefTypeReference? returnType = null,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<ReefMethod.Local>? locals = null)
    {
        return new ReefMethod
        {
            Extern = false,
            DisplayName = name,
            TypeParameters = typeParameters ?? [],
            Instructions = new InstructionList([..instructions], [..labels ?? []]),
            Locals = locals ?? [],
            Parameters = parameters ?? [],
            ReturnType = returnType ?? ConcreteTypeReference(new DefId("Reef.Core", "System.Unit"), "Unit"),
            Id = defId
        };
    }

    public static ReefVariant Variant(string name,
        IReadOnlyList<ReefField>? fields = null)
    {
        return new ReefVariant
        {
            DisplayName = name,
            Fields = fields ?? []
        };
    }

    public static ReefILTypeDefinition DataType(
        DefId defId,
        string name,
        IReadOnlyList<ReefVariant> variants,
        IReadOnlyList<StaticReefField>? staticFields = null,
        IReadOnlyList<string>? typeParameters = null)
    {
        return new ReefILTypeDefinition
        {
            DisplayName = name,
            Id = defId,
            IsValueType = false,
            TypeParameters = typeParameters ?? [],
            Variants = variants,
            StaticFields = staticFields ?? []
        };
    }

    public static ReefILModule Module(IReadOnlyList<ReefILTypeDefinition>? types = null,
        IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefILModule
        {
            MainMethod = methods?.FirstOrDefault(x => x.DisplayName == "_Main"),
            Methods = methods ?? [],
            Types = types ?? []
        };
    }
    
    public static LoadUnitConstant LoadUnit()
    {
        return new LoadUnitConstant();
    }

    public static Return Return()
    {
        return new Return();
    }

    public static Drop Drop()
    {
        return new Drop();
    }

    public static LoadStringConstant StringConstant(string value)
    {
        return new LoadStringConstant(value);
    }
}