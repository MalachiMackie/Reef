using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests;

public static class TestHelpers
{
    public static ReefMethod.Local Local(string name, IReefTypeReference type)
    {
        return new ReefMethod.Local { DisplayName = name, Type = type };
    }

    public static FunctionDefinitionReference FunctionDefinitionReference(string name, IReadOnlyList<IReefTypeReference>? typeArguments = null) => new()
    {
        Name = name,
        TypeArguments = typeArguments ?? [],
        DefinitionId = Guid.Empty
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

    public static IReefTypeReference GenericTypeReference(string typeParameterName)
    {
        return new GenericReefTypeReference
        {
            TypeParameterName = typeParameterName,
            DefinitionId = Guid.Empty
        };
    }

    public static ConcreteReefTypeReference StringType => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.String);
    public static ConcreteReefTypeReference IntType => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Int);
    public static ConcreteReefTypeReference BoolType => GetTypeReference(TypeChecking.TypeChecker.ClassSignature.Boolean);
    public static ReefField VariantIdentifierField => Field("_variantIdentifier", IntType);
    
    private static ConcreteReefTypeReference GetTypeReference(TypeChecking.TypeChecker.ClassSignature klass)
    {
        return new ConcreteReefTypeReference
        {
            Name = klass.Name,
            TypeArguments = [],
            DefinitionId = klass.Id
        };
    }

    public static ConcreteReefTypeReference ConcreteTypeReference(string name, IReadOnlyList<IReefTypeReference>? typeArguments = null)
    {
        return new ConcreteReefTypeReference
        {
            Name = name,
            DefinitionId = Guid.Empty,
            TypeArguments = typeArguments ?? []
        };
    }

    public static ReefMethod Method(
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
            DisplayName = name,
            TypeParameters = typeParameters ?? [],
            Instructions = new InstructionList([..instructions], [..labels ?? []]),
            Locals = locals ?? [],
            Parameters = parameters ?? [],
            ReturnType = returnType ?? ConcreteTypeReference("Unit")
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

    public static ReefTypeDefinition DataType(
        string name,
        IReadOnlyList<ReefVariant> variants,
        IReadOnlyList<StaticReefField>? staticFields = null,
        IReadOnlyList<string>? typeParameters = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Id = Guid.Empty,
            IsValueType = false,
            TypeParameters = typeParameters ?? [],
            Variants = variants,
            StaticFields = staticFields ?? []
        };
    }

    public static ReefModule Module(IReadOnlyList<ReefTypeDefinition>? types = null,
        IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefModule
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