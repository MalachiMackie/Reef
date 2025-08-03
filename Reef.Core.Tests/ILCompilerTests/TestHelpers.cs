using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests;

public static class TestHelpers
{
    public static ReefMethod.Local Local(string name, IReefTypeReference type)
    {
        return new ReefMethod.Local { DisplayName = name, Type = type };
    }

    public static FunctionDefinitionReference FunctionDefinitionReference(string name) => new()
    {
        Name = name,
        TypeArguments = [],
        DefinitionId = Guid.Empty
    };

    public static ReefField Field(string name, IReefTypeReference type, bool isStatic = false, bool isPublic = false,
        IReadOnlyList<IInstruction>? staticInitializer = null)
    {
        return new ReefField
        {
            IsStatic = isStatic,
            IsPublic = isPublic,
            Type = type,
            DisplayName = name,
            StaticInitializerInstructions = staticInitializer ?? []
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

    public static ConcreteReefTypeReference ConcreteTypeReference(string name, IReadOnlyList<IReefTypeReference>? typeArguments = null)
    {
        return new ConcreteReefTypeReference
        {
            Name = name,
            DefinitionId = Guid.Empty,
            TypeArguments = typeArguments ?? []
        };
    }

    public static ReefMethod.Parameter Parameter(string name, IReefTypeReference typeReference)
    {
        return new ReefMethod.Parameter
        {
            DisplayName = name,
            Type = typeReference
        };
    }

    public static ReefMethod Method(
        string name,
        bool isStatic = false,
        IReadOnlyList<ReefMethod.Parameter>? parameters = null,
        IReefTypeReference? returnType = null,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<IInstruction>? instructions = null,
        IReadOnlyList<ReefMethod.Local>? locals = null)
    {
        return new ReefMethod
        {
            DisplayName = name,
            IsStatic = isStatic,
            TypeParameters = typeParameters ?? [],
            Instructions = instructions ?? [],
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

    public static ReefTypeDefinition Union(
        string name,
        IReadOnlyList<ReefVariant>? variants = null,
        IReadOnlyList<ReefMethod>? methods = null,
        IReadOnlyList<string>? typeParameters = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Methods = methods ?? [],
            Id = Guid.Empty,
            IsValueType = false,
            TypeParameters = typeParameters ?? [],
            Variants = variants ?? []
        };
    }

    public static ReefTypeDefinition Class(
        string name,
        string? variantName = null,
        IReadOnlyList<ReefMethod>? methods = null,
        IReadOnlyList<string>? typeParameters = null,
        IReadOnlyList<ReefField>? fields = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Id = Guid.Empty,
            Methods = methods ?? [],
            IsValueType = false,
            TypeParameters = typeParameters ?? [],
            Variants =
            [
                Variant(variantName ?? "!ClassVariant", fields: fields)
            ]
        };
    }

    public static ReefModule Module(IReadOnlyList<ReefTypeDefinition>? types = null,
        IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefModule
        {
            MainMethod = methods?.FirstOrDefault(x => x.DisplayName == "!Main"),
            Methods = methods ?? [],
            Types = types ?? []
        };
    }

    public static LoadUnitConstant LoadUnit(uint address)
    {
        return new LoadUnitConstant(new InstructionAddress(address));
    }

    public static Return Return(uint address)
    {
        return new Return(new InstructionAddress(address));
    }

    public static Drop Drop(uint address)
    {
        return new Drop(new InstructionAddress(address));
    }

    public static InstructionAddress Addr(uint address)
    {
        return new InstructionAddress(address);
    }
}