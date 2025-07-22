using Reef.IL;

namespace Reef.Core;

public class ILCompile
{
    public static ReefModule CompileToIL(LangProgram program)
    {
        var compiler = new ILCompile();

        return compiler.CompileToILInner(program);
    }

    private readonly List<ReefTypeDefinition> _types = [];
    private readonly List<ReefMethod> _methods = [];
    
    private ReefModule CompileToILInner(LangProgram program)
    {
        _types.AddRange(program.Unions
            .Select(x =>
                CompileUnion(x.Signature ?? throw new InvalidOperationException("Expected signature to be set")))
            .Concat(program.Classes.Select(x =>
                CompileClass(x.Signature ?? throw new InvalidOperationException("Expected signature to be set")))));

        // add static methods directly to module
        _methods.AddRange(program.Functions.Where(x => x.Signature!.IsStatic)
            .Select(x => CompileMethod(x.Signature!, ownerType: null)));
        
        ReefMethod? mainMethod = null;
        
        if (program.Expressions.Count > 0)
        {
            mainMethod = CompileMethod(new TypeChecker.FunctionSignature(
                Token.Identifier("!Main", SourceSpan.Default),
                [],
                [],
                isStatic: true,
                isMutable: false,
                program.Expressions,
                program.Functions.Select(x => x.Signature!).Where(x => !x.IsStatic).ToArray()
            )
            {
                ReturnType = TypeChecker.InstantiatedClass.Unit,
                LocalVariables = [..program.TopLevelLocalVariables],
            }, ownerType: null);
            
            _methods.Add(mainMethod);
        }

        return new ReefModule
        {
            MainMethod = mainMethod,
            Methods = _methods,
            Types = _types
        };
    }

    private ReefMethod CompileMethod(
        TypeChecker.FunctionSignature function,
        ReefTypeDefinition? ownerType)
    {
        foreach (var innerMethod in function.LocalFunctions)
        {
            _methods.Add(CompileMethod(innerMethod, ownerType: null));
        }
        
        var parameters = new List<ReefMethod.Parameter>();
        if (!function.IsStatic && ownerType is not null)
        {
            parameters.Add(new ReefMethod.Parameter
            {
                DisplayName = "this",
                Type = new ConcreteReefTypeReference
                {
                    Name = ownerType.DisplayName,
                    DefinitionId = ownerType.Id,
                    TypeArguments = ownerType.TypeParameters.Select(x => new GenericReefTypeReference
                    {
                        DefinitionId = ownerType.Id,
                        TypeParameterName = x
                    }).ToArray()
                }
            });
        }

        if (function.AccessedOuterVariables.Count > 0)
        {
            var closureType = new ReefTypeDefinition
            {
                DisplayName = $"{function.Name}!Closure",
                Methods = [],
                TypeParameters = [],
                Id = Guid.NewGuid(),
                IsValueType = false,
                Variants = [new ReefVariant
                {
                    DisplayName = "ClosureVariant",
                    StaticFields = [],
                    InstanceFields = function.AccessedOuterVariables.Select((x, i) => new ReefField
                    {
                        DisplayName = $"Field_{i}",
                        IsStatic = false,
                        Type = GetTypeReference(x.Type ?? throw new InvalidOperationException("Expected variable to have type")),
                        IsPublic = true,
                        StaticInitializerInstructions = []
                    }).ToArray()
                }]
            };
            // todo: possible to have more than one closure within a method
            _types.Add(closureType);
            parameters.Add(new ReefMethod.Parameter
            {
                Type = new ConcreteReefTypeReference
                {
                    DefinitionId = closureType.Id,
                    TypeArguments = [],
                    Name = closureType.DisplayName
                },
                DisplayName = "ClosureParameter",
            });
        }
        parameters.AddRange(function.Parameters.Select(x => new ReefMethod.Parameter
        {
            DisplayName = x.Key,
            Type = GetTypeReference(x.Value.Type)
        }));
        
        return new ReefMethod
        {
            DisplayName = function.Name,
            Parameters = parameters,
            TypeParameters = function.TypeParameters.Select(x => x.GenericName).ToArray(),
            ReturnType = GetTypeReference(function.ReturnType),
            Instructions = function.Expressions.SelectMany(CompileExpression).ToArray(),
            Locals = function.LocalVariables.Select(x => new ReefMethod.Local
            {
                Type = GetTypeReference(x.Type ?? throw new InvalidOperationException("Expected type")),
                DisplayName = x.Name.StringValue
            }).ToArray(),
            IsStatic = function.IsStatic
        };
    }

    private static IEnumerable<IInstruction> CompileExpression(IExpression expression)
    {
        return [];
    }

    private ReefTypeDefinition CompileUnion(TypeChecker.UnionSignature union)
    {
        var methods = new List<ReefMethod>();
        var definition = new ReefTypeDefinition
        {
            Id = union.Id,
            DisplayName = union.Name,
            IsValueType = false,
            Variants = union.Variants.Select(x => x switch
            {
                TypeChecker.ClassUnionVariant classUnionVariant => new ReefVariant
                {
                    DisplayName = x.Name,
                    StaticFields = [],
                    InstanceFields = classUnionVariant.Fields.Select(y => new ReefField
                    {
                        DisplayName = y.Name,
                        IsStatic = false,
                        IsPublic = true,
                        Type = GetTypeReference(y.Type),
                        StaticInitializerInstructions = []
                    }).ToArray()
                },
                TypeChecker.TupleUnionVariant tupleUnionVariant => new ReefVariant
                {
                    DisplayName = x.Name,
                    StaticFields = [],
                    InstanceFields = tupleUnionVariant.TupleMembers.Select((y, i) => new ReefField
                    {
                        DisplayName = TypeChecker.ClassSignature.TupleFieldNames[i],
                        IsStatic = false,
                        IsPublic = true,
                        StaticInitializerInstructions = [],
                        Type = GetTypeReference(y)
                    }).ToArray()
                },
                TypeChecker.UnitUnionVariant => new ReefVariant
                {
                    DisplayName = x.Name,
                    StaticFields = [],
                    InstanceFields = [],
                },
                _ => throw new ArgumentOutOfRangeException(nameof(x))
            }).ToArray(),
            TypeParameters = union.TypeParameters.Select(x => x.GenericName).ToArray(),
            Methods = methods
        };
        methods.AddRange(union.Functions.Select(x => CompileMethod(x, definition)));

        return definition;
    }

    private ReefTypeDefinition CompileClass(TypeChecker.ClassSignature @class)
    {
        var methods = new List<ReefMethod>();
        var definition = new ReefTypeDefinition
        {
            Id = @class.Id,
            DisplayName = @class.Name,
            IsValueType = false,
            Variants = [new ReefVariant
            {
                DisplayName = "!ClassVariant",
                InstanceFields = @class.Fields.Select(x => new ReefField
                    {
                        IsStatic = false,
                        IsPublic = x.IsPublic,
                        StaticInitializerInstructions = [],
                        DisplayName = x.Name,
                        Type = GetTypeReference(x.Type)
                    }).ToArray(),
                StaticFields = @class.StaticFields.Select(x => new ReefField
                    {
                        IsStatic = true,
                        IsPublic = x.IsPublic,
                        StaticInitializerInstructions = x.StaticInitializer is null ? [] : CompileExpression(x.StaticInitializer).ToArray(),
                        DisplayName = x.Name,
                        Type = GetTypeReference(x.Type)
                    })
                    .ToArray()
            }],
            TypeParameters = @class.TypeParameters.Select(x => x.GenericName).ToArray(),
            Methods = methods,
        };
        methods.AddRange(@class.Functions.Select(x => CompileMethod(x, definition)));

        return definition;
    }

    private static IReefTypeReference GetTypeReference(TypeChecker.ITypeReference typeReference)
    {
        return typeReference switch
        {
            TypeChecker.GenericTypeReference { ResolvedType: null } genericTypeReference => new GenericReefTypeReference
            {
                DefinitionId = genericTypeReference.OwnerType.Id,
                TypeParameterName = genericTypeReference.GenericName
            },
            TypeChecker.GenericTypeReference genericTypeReference =>
                GetTypeReference(genericTypeReference.ResolvedType),
            TypeChecker.InstantiatedClass instantiatedClass => new ConcreteReefTypeReference
            {
                Name = instantiatedClass.Signature.Name,
                DefinitionId = instantiatedClass.Signature.Id,
                TypeArguments = instantiatedClass.TypeArguments.Select(GetTypeReference).ToArray()
            },
            TypeChecker.InstantiatedUnion instantiatedUnion => new ConcreteReefTypeReference
            {
                Name = instantiatedUnion.Signature.Name,
                DefinitionId = instantiatedUnion.Signature.Id,
                TypeArguments = instantiatedUnion.TypeArguments.Select(GetTypeReference).ToArray()
            },
            _ => throw new InvalidOperationException("Unexpected type reference")
        };
    }
    
}
