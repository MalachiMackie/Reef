using Reef.Core.TypeChecking;
using Reef.Core.LoweredExpressions;
using BlockExpression = Reef.Core.LoweredExpressions.BlockExpression;
using MethodCallExpression = Reef.Core.LoweredExpressions.MethodCallExpression;
using MethodReturnExpression = Reef.Core.LoweredExpressions.MethodReturnExpression;
using VariableDeclarationExpression = Reef.Core.LoweredExpressions.VariableDeclarationExpression;
using Reef.Core.IL;

namespace Reef.Core;

public class ILCompile(LoweredProgram program)
{
    public static (ReefILModule, IReadOnlyList<ReefILModule> importedModules) CompileToIL(LoweredProgram program)
    {
        var compiler = new ILCompile(program);

        return (compiler.CompileToILInner(), compiler._importedModules);
    }

    private readonly List<ReefILTypeDefinition> _types = [];
    private readonly List<ReefILModule> _importedModules = [GetImportedStdLibrary()];
    private readonly List<ReefMethod> _methods = [];
    private readonly Stack<Scope> _scopeStack = [];
    private readonly LoweredProgram _program = program;
    private InstructionList Instructions => _scopeStack.Peek().InstructionList;
    private Stack<IReefTypeReference> TypeStack => _scopeStack.Peek().TypeStack;

    private class Scope
    {
        public InstructionList InstructionList { get; init; } = new([], []);
        public HashSet<string> ReservedLabels { get; init; } = [];
        public Stack<IReefTypeReference> TypeStack { get; set; } = [];
        public required LoweredMethod? Method { get; init; }
    }

    private static ReefILModule GetImportedStdLibrary()
    {
        var importedDataTypes = new List<ReefILTypeDefinition>()
        {
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.Int64.Name,
                Id = DefId.Int64,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.Int32.Name,
                Id = DefId.Int32,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.Int16.Name,
                Id = DefId.Int16,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.Int8.Name,
                Id = DefId.Int8,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.UInt64.Name,
                Id = DefId.UInt64,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.UInt32.Name,
                Id = DefId.UInt32,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.UInt16.Name,
                Id = DefId.UInt16,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.UInt8.Name,
                Id = DefId.UInt8,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.String.Name,
                Id = DefId.String,
                IsValueType = false,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
            new ReefILTypeDefinition
            {
                DisplayName = TypeChecker.ClassSignature.Boolean.Name,
                Id = DefId.Boolean,
                IsValueType = true,
                Variants = [
                    new(){DisplayName = "_classVariant", Fields = []}
                ],
                StaticFields = [],
                TypeParameters = []
            },
        };

        var printf = TypeChecker.FunctionSignature.Printf;
        var importedMethods = new List<ReefMethod>()
        {
            new()
            {
                Extern = true,
                DisplayName = printf.Name,
                Id = printf.Id,
                Instructions = new([], []),
                Locals = [],
                Parameters = [
                    new ConcreteReefTypeReference
                    {
                        DefinitionId = TypeChecker.ClassSignature.String.Id,
                        Name = TypeChecker.ClassSignature.String.Name,
                        TypeArguments = []
                    }
                ],
                ReturnType = new ConcreteReefTypeReference
                {
                    DefinitionId = TypeChecker.ClassSignature.Unit.Id,
                    Name = TypeChecker.ClassSignature.Unit.Name,
                    TypeArguments = []
                },
                TypeParameters = []
            }
        };

        for (var i = 2; i < 10; i++)
        {
            var tupleClass = TypeChecker.ClassSignature.Tuple(2);
            importedDataTypes.Add(
                new ReefILTypeDefinition
                {
                    Id = tupleClass.Id,
                    DisplayName = tupleClass.Name,
                    TypeParameters = [..tupleClass.TypeParameters.Select(x => x.GenericName)],
                    StaticFields = [],
                    Variants = [
                        new ReefVariant
                        {
                            DisplayName = "_classVariant",
                            Fields = [..Enumerable.Range(0, i).Select(j => new ReefField
                            {
                                DisplayName = $"Item{j}",
                                Type = new GenericReefTypeReference
                                {
                                    DefinitionId = tupleClass.Id,
                                    TypeParameterName = $"T{j}"
                                }
                            })]
                        }
                    ],
                    IsValueType = false
                });
        }

        var pointerType = new ConcreteReefTypeReference
        {
            Name = TypeChecker.ClassSignature.RawPointer.Name,
            DefinitionId = TypeChecker.ClassSignature.RawPointer.Id,
            TypeArguments = []
        };

        for (var i = 0; i < 7; i++)
        {
            var fnClass = TypeChecker.ClassSignature.Function(i);
            importedDataTypes.Add(
                new()
                {
                    Id = fnClass.Id,
                    DisplayName = fnClass.Name,
                    TypeParameters = [..fnClass.TypeParameters.Select(x => x.GenericName)],
                    StaticFields = [],
                    Variants = [new ReefVariant
                    {
                        DisplayName = "_classVariant",
                        Fields = [
                            new ReefField
                            {
                                DisplayName = "FunctionReference",
                                Type = new FunctionPointerReefType
                                {
                                    Parameters = [..fnClass.TypeParameters.SkipLast(1).Select(GetTypeReference)],
                                    ReturnType = GetTypeReference(fnClass.TypeParameters[^1])
                                }
                            },
                            new ReefField
                            {
                                DisplayName = "FunctionParameter",
                                Type = pointerType
                            }
                        ]
                    }],
                    IsValueType = false
                });

            var call = fnClass.Functions[0];

            importedMethods.Add(new ReefMethod()
            {
                Extern = false,
                Id = call.Id,
                DisplayName = $"{fnClass.Name}__{call.Name}",
                Instructions = new([], []),
                Locals = [],
                ReturnType = GetTypeReference(call.ReturnType),
                Parameters = [..call.Parameters.Values.Select(x => GetTypeReference(x.Type))],
                TypeParameters = [..fnClass.TypeParameters.Select(x => x.GenericName)]
            });
        }

        var resultSignature = TypeChecker.UnionSignature.Result;
        var valueGeneric = new GenericReefTypeReference
        {
            DefinitionId = resultSignature.Id,
            TypeParameterName = "TValue"
        };
        var errorGeneric = new GenericReefTypeReference
        {
            DefinitionId = resultSignature.Id,
            TypeParameterName = "TError"
        };
        var uint16Ref = GetTypeReference(TypeChecker.InstantiatedClass.UInt16);

        var resultDataType = new ReefILTypeDefinition()
        {
            Id = TypeChecker.UnionSignature.Result.Id,
            DisplayName = TypeChecker.UnionSignature.Result.Name,
            TypeParameters = [..TypeChecker.UnionSignature.Result.TypeParameters.Select(x => x.GenericName)],
            IsValueType = false,
            StaticFields = [],
            Variants = [
                new ReefVariant()
                {
                    DisplayName = "Ok",
                    Fields = [
                        new ReefField{DisplayName = "_variantIdentifier", Type = uint16Ref},
                        new ReefField{DisplayName = "Item0", Type = valueGeneric}
                    ]
                },
                new ReefVariant()
                {
                    DisplayName = "Error",
                    Fields = [
                        new ReefField{DisplayName = "_variantIdentifier", Type = uint16Ref},
                        new ReefField{DisplayName = "Item0", Type = errorGeneric}
                    ]
                }
            ]
        };
        importedDataTypes.Add(resultDataType);
        foreach (var variant in TypeChecker.UnionSignature.Result.Variants.OfType<TypeChecking.TypeChecker.TupleUnionVariant>())
        {
            importedMethods.Add(new()
            {
                Extern = false,
                Id = variant.CreateFunction.Id,
                DisplayName = variant.CreateFunction.Name,
                Instructions = new([], []),
                Locals = [],
                ReturnType = new ConcreteReefTypeReference
                {
                    DefinitionId = resultSignature.Id,
                    Name = resultSignature.Name,
                    TypeArguments = [valueGeneric, errorGeneric]
                },
                TypeParameters = ["TValue", "TError"],
                Parameters = [..variant.CreateFunction.Parameters.Values.Select(_ => new GenericReefTypeReference
                {
                    DefinitionId = resultSignature.Id,
                    TypeParameterName = variant.Name == "Ok" ? "TValue" : "TError"
                })]
            });
        }

        return new ReefILModule()
        {
            Methods = importedMethods,
            MainMethod = null,
            Types = importedDataTypes
        };

    }

    private ReefILModule CompileToILInner()
    {
        _types.AddRange(_program.DataTypes.Select(CompileDataType));
        _methods.AddRange(_program.Methods.Select(CompileMethod));

        return new ReefILModule
        {
            MainMethod = _methods.FirstOrDefault(x => x.DisplayName == "_Main"),
            Methods = _methods,
            Types = _types
        };
    }

    private ReefMethod CompileMethod(LoweredMethod method)
    {
        _scopeStack.Push(new Scope() { Method = method });

        foreach (var expression in method.Expressions)
        {
            CompileExpression(expression);
        }

        var scope = _scopeStack.Pop();
        
        return new ReefMethod
        {
            Extern = false,
            Id = method.Id,
            DisplayName = method.Name,
            Parameters = [..method.Parameters.Select(GetTypeReference)],
            Instructions = scope.InstructionList,
            ReturnType = GetTypeReference(method.ReturnType),
            TypeParameters = [..method.TypeParameters.Select(x => x.PlaceholderName)],
            Locals = [..method.Locals.Select(x => new ReefMethod.Local(){DisplayName = x.Name, Type = GetTypeReference(x.Type)})]
        };
    }

    private ReefILTypeDefinition CompileDataType(DataType dataType)
    {
        var variants = new List<ReefVariant>(dataType.Variants.Count);
        foreach (var variant in dataType.Variants)
        {
            variants.Add(new ReefVariant
            {
                DisplayName = variant.Name,
                Fields = [..variant.Fields.Select(x => new ReefField
                {
                    DisplayName = x.Name,
                    Type = GetTypeReference(x.Type), 
                })]
            });
        }

        return new ReefILTypeDefinition
        {
            DisplayName = dataType.Name,
            Variants = variants,
            TypeParameters = [..dataType.TypeParameters.Select(x => x.PlaceholderName)],
            Id = dataType.Id,
            IsValueType = false, // todo
            StaticFields = [..dataType.StaticFields.Select(field =>
            {
                _scopeStack.Push(new Scope(){Method = null});
                CompileExpression(field.StaticInitializer);
                var scope = _scopeStack.Pop();
                
                return new StaticReefField
                {
                    DisplayName = field.Name,
                    Type = GetTypeReference(field.Type),
                    StaticInitializerInstructions = scope.InstructionList
                };
            })]
        };
    }

    private static ConcreteReefTypeReference GetTypeReference(LoweredConcreteTypeReference typeReference)
    {
        return new ConcreteReefTypeReference
        {
            Name = typeReference.Name,
            TypeArguments = [..typeReference.TypeArguments.Select(GetTypeReference)],
            DefinitionId = typeReference.DefinitionId
        };
    }

    private static IReefTypeReference GetTypeReference(ILoweredTypeReference typeReference)
    {
        return typeReference switch
        {
            LoweredConcreteTypeReference concrete => new ConcreteReefTypeReference
            {
                Name = concrete.Name,
                TypeArguments = [..concrete.TypeArguments.Select(GetTypeReference)],
                DefinitionId = concrete.DefinitionId
            },
            LoweredFunctionPointer loweredFunctionType => FunctionCase(loweredFunctionType),
            LoweredGenericPlaceholder genericPlaceholder => new GenericReefTypeReference
            {
                DefinitionId = genericPlaceholder.OwnerDefinitionId,
                TypeParameterName = genericPlaceholder.PlaceholderName
            },
            _ => throw new InvalidOperationException("Unexpected type reference")
        };

        IReefTypeReference FunctionCase(LoweredFunctionPointer functionPointer)
        {
            return new FunctionPointerReefType
            {
                Parameters = [..functionPointer.ParameterTypes.Select(GetTypeReference)],
                ReturnType = GetTypeReference(functionPointer.ReturnType)
            };
        }
    }

    private ReefILTypeDefinition GetDataType(DefId definitionId)
    {
        if (_types.FirstOrDefault(x => x.Id == definitionId) is { } foundType)
        {
            return foundType;
        }

        return _importedModules.SelectMany(x => x.Types.Where(y => y.Id == definitionId))
                .FirstOrDefault()
            ?? throw new InvalidOperationException($"Unable to find type with id {definitionId}");
    }

    private static IReefTypeReference GetTypeReference(TypeChecker.ITypeReference typeReference)
    {
        return typeReference switch
        {
            TypeChecker.UnknownInferredType { ResolvedType: { } resolvedType } => GetTypeReference(resolvedType),
            TypeChecker.GenericTypeReference genericTypeReference =>
                GetTypeReference(genericTypeReference.ResolvedType ?? throw new InvalidOperationException("Expected resolved type")),
            TypeChecker.GenericPlaceholder genericPlaceholder =>
                new GenericReefTypeReference
                {
                    DefinitionId = genericPlaceholder.OwnerType.Id,
                    TypeParameterName = genericPlaceholder.GenericName
                },
            TypeChecker.InstantiatedClass instantiatedClass => new ConcreteReefTypeReference
            {
                Name = instantiatedClass.Signature.Name,
                DefinitionId = instantiatedClass.Signature.Id,
                TypeArguments = [.. instantiatedClass.TypeArguments.Select(GetTypeReference)]
            },
            TypeChecker.InstantiatedUnion instantiatedUnion => new ConcreteReefTypeReference
            {
                Name = instantiatedUnion.Signature.Name,
                DefinitionId = instantiatedUnion.Signature.Id,
                TypeArguments = [.. instantiatedUnion.TypeArguments.Select(GetTypeReference)]
            },
            TypeChecker.FunctionObject functionObject => FunctionCase(functionObject),
            _ => throw new InvalidOperationException("Unexpected type reference")
        };

        ConcreteReefTypeReference FunctionCase(TypeChecker.FunctionObject functionObject)
        {
            var signature = TypeChecker.ClassSignature.Function(functionObject.Parameters.Count);

            var typeArguments = functionObject.Parameters
                .Select(x => x.Type)
                .Append(functionObject.ReturnType)
                .ToArray();

            var resolvedTypeArguments = signature.TypeParameters.Select((x, i) => new TypeChecker.GenericTypeReference()
            {
                GenericName = x.GenericName,
                OwnerType = x.OwnerType,
                ResolvedType = typeArguments[i]
            });

            return new ConcreteReefTypeReference
            {
                DefinitionId = signature.Id,
                Name = signature.Name,
                TypeArguments = resolvedTypeArguments.Select(GetTypeReference).ToArray()
            };
        }
    }

    private void CompileExpression(ILoweredExpression expression)
    {
        switch (expression)
        {
            case BlockExpression blockExpression:
            {
                foreach (var innerExpression in blockExpression.Expressions)
                {
                    CompileExpression(innerExpression);
                }
                break;
            }
            case BoolAndExpression boolAndExpression:
                CompileBooleanAnd(boolAndExpression.Left, boolAndExpression.Right);
                break;
            case BoolConstantExpression boolConstantExpression:
            {
                    TypeStack.Push(GetTypeReference(boolConstantExpression.ResolvedType));
                Instructions.Add(new LoadBoolConstant(boolConstantExpression.Value));
                break;
            }
            case BoolNotExpression boolNotExpression:
            {
                CompileExpression(boolNotExpression.Operand);

                // no stack size modification
                Instructions.Add(new BoolNot());
                break;
            }
            case BoolOrExpression boolOrExpression:
            {
                CompileBooleanOr(boolOrExpression.Left, boolOrExpression.Right);
                break;
            }
            case CastBoolToIntExpression castBoolToIntExpression:
            {
                CompileExpression(castBoolToIntExpression.BoolExpression);

                // no stack size modification
                Instructions.Add(new CastBoolToInt());
                break;
            }
            case CreateObjectExpression createObjectExpression:
            {
                var typeReference = GetTypeReference(createObjectExpression.Type);
                Instructions.Add(new CreateObject(typeReference));
                    TypeStack.Push(typeReference);
                var (variantIndex, variant) = GetDataTypeVariant(typeReference, createObjectExpression.Variant);
                foreach (var field in variant.Fields)
                {
                    if (createObjectExpression.VariantFieldInitializers.TryGetValue(field.DisplayName,
                            out var fieldValue))
                    {
                            TypeStack.Push(typeReference);
                        Instructions.Add(new CopyStack());
                        CompileExpression(fieldValue);

                        TypeStack.Pop();
                        TypeStack.Pop();
                        Instructions.Add(new StoreField(variantIndex, field.DisplayName));
                    }
                    else
                    {
                        // todo: zero out fields that aren't assigned
                    }
                }

                break;
            }
            case FieldAccessExpression fieldAccessExpression:
            {
                CompileExpression(fieldAccessExpression.MemberOwner);
                var typeReference = GetTypeReference(fieldAccessExpression.MemberOwner.ResolvedType);
                var (variantIndex, variant) = GetDataTypeVariant(typeReference, fieldAccessExpression.VariantName);

                var field = variant.Fields.First(x => x.DisplayName == fieldAccessExpression.FieldName);

                TypeStack.Pop();
                    TypeStack.Push(field.Type);
                Instructions.Add(new LoadField(variantIndex, fieldAccessExpression.FieldName));
                break;
            }
            case FieldAssignmentExpression fieldAssignmentExpression:
            {
                CompileExpression(fieldAssignmentExpression.FieldOwnerExpression);
                CompileExpression(fieldAssignmentExpression.FieldValue);

                var type = GetTypeReference(fieldAssignmentExpression.FieldOwnerExpression.ResolvedType);

                var (variantIndex, _) = GetDataTypeVariant(
                    type,
                    fieldAssignmentExpression.VariantName);

                    TypeStack.Pop();
                    TypeStack.Pop();
                Instructions.Add(new StoreField(variantIndex, fieldAssignmentExpression.FieldName));
                break;
            }
            case FunctionReferenceConstantExpression functionReferenceConstantExpression:
                var functionReference = functionReferenceConstantExpression.FunctionReference;
                TypeStack.Push(GetTypeReference(functionReferenceConstantExpression.ResolvedType));
                Instructions.Add(new LoadFunction(new FunctionDefinitionReference
                {
                    DefinitionId = functionReference.DefinitionId,
                    Name = functionReference.Name,
                    TypeArguments = [..functionReference.TypeArguments.Select(GetTypeReference)]
                }));
                break;
            case Int64ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadInt64Constant(intConstantExpression.Value));
                break;
            case Int32ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadInt32Constant(intConstantExpression.Value));
                break;
            case Int16ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadInt16Constant(intConstantExpression.Value));
                break;
            case Int8ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadInt8Constant(intConstantExpression.Value));
                break;
            case UInt64ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadUInt64Constant(intConstantExpression.Value));
                break;
            case UInt32ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadUInt32Constant(intConstantExpression.Value));
                break;
            case UInt16ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadUInt16Constant(intConstantExpression.Value));
                break;
            case UInt8ConstantExpression intConstantExpression:
                TypeStack.Push(GetTypeReference(intConstantExpression.ResolvedType));
                Instructions.Add(new LoadUInt8Constant(intConstantExpression.Value));
                break;
            case Int64DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                TypeStack.Pop();
                TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new Int64Divide());
                break;
            }
            case Int32DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new Int32Divide());
                break;
            }
            case Int16DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new Int16Divide());
                break;
            }
            case Int8DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new Int8Divide());
                break;
            }
            case UInt64DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new UInt64Divide());
                break;
            }
            case UInt32DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new UInt32Divide());
                break;
            }
            case UInt16DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new UInt16Divide());
                break;
            }
            case UInt8DivideExpression intDivideExpression:
            {
                CompileExpression(intDivideExpression.Left);
                CompileExpression(intDivideExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                TypeStack.Push(GetTypeReference(intDivideExpression.ResolvedType));
                Instructions.Add(new UInt8Divide());
                break;
            }
            case Int64NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt64NotEqual());
                break;
            }
            case Int32NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt32NotEqual());
                break;
            }
            case Int16NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt16NotEqual());
                break;
            }
            case Int8NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt8NotEqual());
                break;
            }
            case UInt64NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt64NotEqual());
                break;
            }
            case UInt32NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt32NotEqual());
                break;
            }
            case UInt16NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt16NotEqual());
                break;
            }
            case UInt8NotEqualsExpression intNotEqualsExpression:
            {
                CompileExpression(intNotEqualsExpression.Left);
                CompileExpression(intNotEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intNotEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt8NotEqual());
                break;
            }
            case Int64EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt64Equal());
                break;
            }
            case Int32EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt32Equal());
                break;
            }
            case Int16EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt16Equal());
                break;
            }
            case Int8EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareInt8Equal());
                break;
            }
            case UInt64EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt64Equal());
                break;
            }
            case UInt32EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt32Equal());
                break;
            }
            case UInt16EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt16Equal());
                break;
            }
            case UInt8EqualsExpression intEqualsExpression:
            {
                CompileExpression(intEqualsExpression.Left);
                CompileExpression(intEqualsExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intEqualsExpression.ResolvedType));
                Instructions.Add(new CompareUInt8Equal());
                break;
            }
            case Int64GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareInt64GreaterThan());
                break;
            }
            case Int32GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareInt32GreaterThan());
                break;
            }
            case Int16GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareInt16GreaterThan());
                break;
            }
            case Int8GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareInt8GreaterThan());
                break;
            }
            case UInt64GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt64GreaterThan());
                break;
            }
            case UInt32GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt32GreaterThan());
                break;
            }
            case UInt16GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt16GreaterThan());
                break;
            }
            case UInt8GreaterThanExpression intGreaterThanExpression:
            {
                CompileExpression(intGreaterThanExpression.Left);
                CompileExpression(intGreaterThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intGreaterThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt8GreaterThan());
                break;
            }
            case Int64LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareInt64LessThan());
                break;
            }
            case Int32LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareInt32LessThan());
                break;
            }
            case Int16LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareInt16LessThan());
                break;
            }
            case Int8LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareInt8LessThan());
                break;
            }
            case UInt64LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt64LessThan());
                break;
            }
            case UInt32LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt32LessThan());
                break;
            }
            case UInt16LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt16LessThan());
                break;
            }
            case UInt8LessThanExpression intLessThanExpression:
            {
                CompileExpression(intLessThanExpression.Left);
                CompileExpression(intLessThanExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intLessThanExpression.ResolvedType));
                Instructions.Add(new CompareUInt8LessThan());
                break;
            }
            case Int64MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new Int64Minus());
                break;
            }
            case Int32MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new Int32Minus());
                break;
            }
            case Int16MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new Int16Minus());
                break;
            }
            case Int8MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new Int8Minus());
                break;
            }
            case UInt64MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new UInt64Minus());
                break;
            }
            case UInt32MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new UInt32Minus());
                break;
            }
            case UInt16MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new UInt16Minus());
                break;
            }
            case UInt8MinusExpression intMinusExpression:
            {
                CompileExpression(intMinusExpression.Left);
                CompileExpression(intMinusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMinusExpression.ResolvedType));
                Instructions.Add(new UInt8Minus());
                break;
            }
            case Int64MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new Int64Multiply());
                break;
            }
            case Int32MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new Int32Multiply());
                break;
            }
            case Int16MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new Int16Multiply());
                break;
            }
            case Int8MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new Int8Multiply());
                break;
            }
            case UInt64MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new UInt64Multiply());
                break;
            }
            case UInt32MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new UInt32Multiply());
                break;
            }
            case UInt16MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new UInt16Multiply());
                break;
            }
            case UInt8MultiplyExpression intMultiplyExpression:
            {
                CompileExpression(intMultiplyExpression.Left);
                CompileExpression(intMultiplyExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intMultiplyExpression.ResolvedType));
                Instructions.Add(new UInt8Multiply());
                break;
            }
            case Int64PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new Int64Plus());
                break;
            }
            case Int32PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new Int32Plus());
                break;
            }
            case Int16PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new Int16Plus());
                break;
            }
            case Int8PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new Int8Plus());
                break;
            }
            case UInt64PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new UInt64Plus());
                break;
            }
            case UInt32PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new UInt32Plus());
                break;
            }
            case UInt16PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new UInt16Plus());
                break;
            }
            case UInt8PlusExpression intPlusExpression:
            {
                CompileExpression(intPlusExpression.Left);
                CompileExpression(intPlusExpression.Right);

                    TypeStack.Pop();
                    TypeStack.Pop();
                    TypeStack.Push(GetTypeReference(intPlusExpression.ResolvedType));
                Instructions.Add(new UInt8Plus());
                break;
            }
            case LoadArgumentExpression loadArgumentExpression:
                var argumentType = _scopeStack.Peek().Method.NotNull().Parameters[(int)loadArgumentExpression.ArgumentIndex];
                TypeStack.Push(GetTypeReference(argumentType));
                Instructions.Add(new LoadArgument(loadArgumentExpression.ArgumentIndex));
                return;
            case LocalAssignmentExpression localAssignmentExpression:
            {
                CompileExpression(localAssignmentExpression.Value);
                    TypeStack.Pop();

                Instructions.Add(new StoreLocal(localAssignmentExpression.LocalName));
                break;
            }
            case LocalVariableAccessor localVariableAccessor:
            {
                var localType = _scopeStack.Peek().Method.NotNull().Locals.First(x => x.Name == localVariableAccessor.LocalName).Type;
                    TypeStack.Push(GetTypeReference(localType));
                Instructions.Add(new LoadLocal(localVariableAccessor.LocalName));
                break;
            }
            case MethodCallExpression methodCallExpression:
            {
                foreach (var argument in methodCallExpression.Arguments)
                {
                    CompileExpression(argument);
                }
                Instructions.Add(new LoadFunction(new FunctionDefinitionReference
                {
                    DefinitionId = methodCallExpression.FunctionReference.DefinitionId,
                    Name = methodCallExpression.FunctionReference.Name,
                    TypeArguments = [..methodCallExpression.FunctionReference.TypeArguments.Select(GetTypeReference)]
                }));
                    for (var i = 0; i < methodCallExpression.Arguments.Count; i++)
                    {
                        TypeStack.Pop();
                    }
                    var copyStack = new Stack<IReefTypeReference>();
                    foreach (var type in TypeStack)
                    {
                        copyStack.Push(type);
                    }
                Instructions.Add(new Call((uint)methodCallExpression.Arguments.Count, copyStack, methodCallExpression.ValueUseful));

                if (methodCallExpression.ValueUseful)
                {
                        var importedMethod = _importedModules.SelectMany(x => x.Methods).FirstOrDefault(x => x.Id == methodCallExpression.FunctionReference.DefinitionId);

                        IReefTypeReference returnType;

                        if (importedMethod is not null)
                        {
                            returnType = importedMethod.ReturnType;
                        }
                        else
                        {
                            var method = _program.Methods.First(x => x.Id == methodCallExpression.FunctionReference.DefinitionId);
                            returnType = GetTypeReference(method.ReturnType);
                        }

                        TypeStack.Push(returnType);
                }

                break;
            }
            case MethodReturnExpression methodReturnExpression:
                CompileExpression(methodReturnExpression.ReturnValue);
                Instructions.Add(new Return());
                break;
            case StaticFieldAccessExpression staticFieldAccessExpression:
            {
                var typeReference = GetTypeReference(staticFieldAccessExpression.OwnerType);

                var field = GetDataType(typeReference.DefinitionId).StaticFields.First(x => x.DisplayName == staticFieldAccessExpression.FieldName);

                    TypeStack.Push(field.Type);
                Instructions.Add(new LoadStaticField(typeReference, staticFieldAccessExpression.FieldName));
                break;
            }
            case StaticFieldAssignmentExpression staticFieldAssignmentExpression:
            {
                CompileExpression(staticFieldAssignmentExpression.FieldValue);
                var typeReference = GetTypeReference(staticFieldAssignmentExpression.OwnerType);
                    TypeStack.Pop();
                Instructions.Add(new StoreStaticField(typeReference, staticFieldAssignmentExpression.FieldName));
                break;
            }
            case StringConstantExpression stringConstantExpression:
            {
                    TypeStack.Push(GetTypeReference(stringConstantExpression.ResolvedType));
                Instructions.Add(new LoadStringConstant(stringConstantExpression.Value));
                break;
            }
            case SwitchIntExpression switchIntExpression:
            {
                CompileExpression(switchIntExpression.Check);
                var reservedLabels = _scopeStack.Peek().ReservedLabels;
                var switchIntIndex = -1;
                string otherwiseLabel;
                do
                {
                    switchIntIndex++;
                    otherwiseLabel = $"switchInt_{switchIntIndex}_otherwise";
                } while (!reservedLabels.Add(otherwiseLabel));

                var branches = new Dictionary<int, string>();
                TypeStack.Pop();
                Instructions.Add(new SwitchInt(branches, otherwiseLabel));

                var afterLabel = $"switchInt_{switchIntIndex}_after";
                var afterNeeded = !switchIntExpression.Otherwise.Diverges || switchIntExpression.Results.Count > 1;
                if (afterNeeded)
                    reservedLabels.Add(afterLabel);
                foreach (var intValue in switchIntExpression.Results.Keys)
                {
                    var label = $"switchInt_{switchIntIndex}_branch_{intValue}";
                    reservedLabels.Add(label);
                    branches[intValue] = label;
                }

                Stack<IReefTypeReference>? expectedStackSizeAfter = null;
                    var beginningStack = new Stack<IReefTypeReference>();
                var copyStack = new Stack<IReefTypeReference>();
                    foreach (var size in TypeStack)
                    {
                        copyStack.Push(size);
                        beginningStack.Push(size);
                    }

                var currentScope = _scopeStack.Peek();

                if (switchIntExpression.Otherwise is not UnreachableExpression)
                {
                    _scopeStack.Push(new Scope()
                    {
                        InstructionList = currentScope.InstructionList,
                        ReservedLabels = currentScope.ReservedLabels,
                        Method = currentScope.Method,
                        TypeStack = copyStack,
                    });

                    Instructions.Labels.Add(new InstructionLabel(otherwiseLabel, (uint)Instructions.Instructions.Count));
                    CompileExpression(switchIntExpression.Otherwise);
                    if (!switchIntExpression.Otherwise.Diverges)
                    {
                        expectedStackSizeAfter = copyStack;
                        Instructions.Add(new Branch(afterLabel));
                    }

                    _scopeStack.Pop();
                }

                var lastBranch = switchIntExpression.Results.Keys.Max();


                
                foreach (var (intValue, branchExpression) in switchIntExpression.Results.OrderBy(x => x.Key))
                {
                    copyStack = [];
                    foreach (var size in TypeStack)
                    {
                        copyStack.Push(size);
                    }

                    _scopeStack.Push(new Scope()
                    {
                        InstructionList = currentScope.InstructionList,
                        ReservedLabels = currentScope.ReservedLabels,
                        TypeStack = copyStack,
                        Method = currentScope.Method
                    });
                    Instructions.Labels.Add(new InstructionLabel(branches[intValue], (uint)Instructions.Instructions.Count));
                    CompileExpression(branchExpression);
                    if (intValue != lastBranch)
                    {
                        Instructions.Add(new Branch(afterLabel));
                    }

                    if (!branchExpression.Diverges)
                    {
                            if (expectedStackSizeAfter is null)
                            {
                                expectedStackSizeAfter = [];
                                foreach (var type in copyStack)
                                {
                                    expectedStackSizeAfter.Push(type);
                                }
                            }
                            else
                            {
                                if (expectedStackSizeAfter.Count != copyStack.Count)
                                {
                                    throw new InvalidOperationException("Expected resulting type stack to be equivalent");
                                }
                                if (expectedStackSizeAfter.Zip(copyStack).Any(x => !AreTypesEquivalent(x.First, x.Second)))
                                {
                                    throw new InvalidOperationException("Expected resulting type stack to be equivalent");
                                }
                            }
                    }

                    _scopeStack.Pop();
                }

                if (afterNeeded)
                {
                    Instructions.Labels.Add(new InstructionLabel(afterLabel, (uint)Instructions.Instructions.Count));
                }

                currentScope.TypeStack = expectedStackSizeAfter ?? beginningStack;
                break;
            }
            case UnitConstantExpression:
                // this is actually a noop, so no stack modification
                Instructions.Add(new LoadUnitConstant());
                break;
            case UnreachableExpression:
                throw new InvalidOperationException("Should not ever have to IL Compile unreachable");
            case VariableDeclarationAndAssignmentExpression variableDeclarationAndAssignmentExpression:
                CompileExpression(variableDeclarationAndAssignmentExpression.Value);
                TypeStack.Pop();
                Instructions.Add(new StoreLocal(variableDeclarationAndAssignmentExpression.LocalName));
                break;
            case VariableDeclarationExpression:
                // noop
                break;
            case NoopExpression:
                break;
            default:
                throw new ArgumentOutOfRangeException(expression.GetType().ToString());
        }
        
        // todo: push units and pop unneeded values
    }

    private bool AreTypesEquivalent(IReefTypeReference expected, IReefTypeReference actual)
    {
        switch (expected, actual)
        {
            case (ConcreteReefTypeReference expectedConcrete, ConcreteReefTypeReference actualConcrete):
                {
                    if (expectedConcrete.DefinitionId != actualConcrete.DefinitionId)
                    {
                        return false;
                    }
                    if (expectedConcrete.TypeArguments.Count != actualConcrete.TypeArguments.Count)
                    {
                        return false;
                    }

                    return expectedConcrete.TypeArguments.Count == 0
                        || expectedConcrete.TypeArguments.Zip(actualConcrete.TypeArguments).All(x => AreTypesEquivalent(x.First, x.Second));
                }
            case (GenericReefTypeReference expectedGeneric, GenericReefTypeReference actualGeneric):
                {
                    return expectedGeneric.DefinitionId == actualGeneric.DefinitionId
                        && expectedGeneric.TypeParameterName == actualGeneric.TypeParameterName;
                }
            case (FunctionPointerReefType expectedFunctionPointer, FunctionPointerReefType actualFunctionPointer):
                {
                    return true; // maybe?
                }
            default:
                return false;
        }
    }

    private (uint variantIndex, ReefVariant variant) GetDataTypeVariant(IReefTypeReference typeReference, string variantName)
    {
        if (typeReference is not ConcreteReefTypeReference concrete)
        {
            throw new InvalidOperationException("Can only access fields on a concrete type");
        }

        var dataType = GetDataType(concrete.DefinitionId);
        var (variantIndex, variant) = dataType.Variants.Index().First(x => x.Item.DisplayName == variantName);
        return ((uint)variantIndex, variant);
    }

    private void CompileBooleanAnd(ILoweredExpression left, ILoweredExpression right)
    {
        var scope = _scopeStack.Peek();
        var andIndex = -1;
        string falseLabel;
        do
        {
            andIndex++;
            falseLabel = $"boolAnd_{andIndex}_false";
        } while (!scope.ReservedLabels.Add(falseLabel));

        var afterLabel = $"boolAnd_{andIndex}_after";
        scope.ReservedLabels.Add(afterLabel);

        CompileExpression(left);
        Instructions.Add(new BranchIfFalse(falseLabel));
        CompileExpression(right);
        Instructions.Add(new Branch(afterLabel));
        
        Instructions.Labels.Add(new InstructionLabel(falseLabel, (uint)Instructions.Instructions.Count));
        Instructions.Add(new LoadBoolConstant(false));
        Instructions.Labels.Add(new InstructionLabel(afterLabel, (uint)Instructions.Instructions.Count));
    }

    private void CompileBooleanOr(ILoweredExpression left, ILoweredExpression right)
    {
        var scope = _scopeStack.Peek();
        var orIndex = -1;
        string trueLabel;
        do
        {
            orIndex++;
            trueLabel = $"boolOr_{orIndex}_true";
        } while (!scope.ReservedLabels.Add(trueLabel));

        var afterLabel = $"boolOr_{orIndex}_after";
        scope.ReservedLabels.Add(afterLabel);

        CompileExpression(left);
        Instructions.Add(new BranchIfTrue(trueLabel));
        CompileExpression(right);
        Instructions.Add(new Branch(afterLabel));
        
        Instructions.Labels.Add(new InstructionLabel(trueLabel, (uint)Instructions.Instructions.Count));
        Instructions.Add(new LoadBoolConstant(true));
        Instructions.Labels.Add(new InstructionLabel(afterLabel, (uint)Instructions.Instructions.Count));
    }
}
