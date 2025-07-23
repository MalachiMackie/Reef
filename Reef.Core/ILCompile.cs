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
    private readonly Stack<Scope> _scopeStack = [];
    private List<IInstruction> Instructions => _scopeStack.Peek().Instructions;

    private class Scope
    {
        public List<IInstruction> Instructions { get; } = [];
        public required IReadOnlyList<TypeChecker.IVariable> AccessedOuterVariables { get; init; }
        public required IReefTypeReference? CurrentType { get; init; }
    }

    private InstructionAddress NextAddress()
    {
        var nextAddress = Instructions.LastOrDefault()?.Address.Index + 1 ?? 0;
        return new InstructionAddress(nextAddress);
    }
    
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
                Type = DefinitionAsTypeReference(ownerType)
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

        var locals = function.LocalVariables.Select(x => new ReefMethod.Local
        {
            Type = GetTypeReference(x.Type ?? throw new InvalidOperationException("Expected type")),
            DisplayName = x.Name.StringValue
        }).ToArray();

        _scopeStack.Push(new Scope
        {
            AccessedOuterVariables = function.AccessedOuterVariables,
            CurrentType = ownerType is null ? null : DefinitionAsTypeReference(ownerType)
        });

        foreach (var expression in function.Expressions)
        {
            CompileExpression(expression);
        }

        if (function.Expressions.Count == 0 || !function.Expressions[^1].Diverges)
        {
            // the last expression does not diverge, so we must be in a 'void' function. Need to push a unit onto the stack
            Instructions.Add(new LoadUnitConstant(NextAddress()));
            Instructions.Add(new Return(NextAddress()));
        }

        var scope = _scopeStack.Pop();
        
        return new ReefMethod
        {
            DisplayName = function.Name,
            Parameters = parameters,
            TypeParameters = function.TypeParameters.Select(x => x.GenericName).ToArray(),
            ReturnType = GetTypeReference(function.ReturnType),
            Instructions = scope.Instructions,
            Locals = locals,
            IsStatic = function.IsStatic
        };
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
        var staticFields = new List<ReefField>();
        
        
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
                StaticFields = staticFields
            }],
            TypeParameters = @class.TypeParameters.Select(x => x.GenericName).ToArray(),
            Methods = methods,
        };
        
        foreach (var field in @class.StaticFields)
        {
            var staticInitializerInstructions = new List<IInstruction>();
            if (field.StaticInitializer is not null)
            {
                _scopeStack.Push(new Scope
                {
                    AccessedOuterVariables = [],
                    CurrentType = DefinitionAsTypeReference(definition)
                });
                CompileExpression(field.StaticInitializer);
                var scope = _scopeStack.Pop();

                staticInitializerInstructions = scope.Instructions;
            }
            
            staticFields.Add(new ReefField
            {
                IsStatic = true,
                IsPublic = field.IsPublic,
                StaticInitializerInstructions = staticInitializerInstructions,
                DisplayName = field.Name,
                Type = GetTypeReference(field.Type)
            });
        }
        
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
    
    private void CompileExpression(IExpression expression)
    {
        switch (expression)
        {
            case BinaryOperatorExpression binaryOperatorExpression:
                CompileBinaryOperatorExpression(binaryOperatorExpression);
                break;
            case BlockExpression blockExpression:
                CompileBlockExpression(blockExpression);
                break;
            case IfExpressionExpression ifExpressionExpression:
                CompileIfExpression(ifExpressionExpression);
                break;
            case MatchesExpression matchesExpression:
                CompileMatchesExpression(matchesExpression);
                break;
            case MatchExpression matchExpression:
                CompileMatchExpression(matchExpression);
                break;
            case MemberAccessExpression memberAccessExpression:
                CompileMemberAccessExpression(memberAccessExpression);
                break;
            case MethodCallExpression methodCallExpression:
                CompileMethodCallExpression(methodCallExpression);
                break;
            case MethodReturnExpression methodReturnExpression:
                CompileMethodReturnExpression(methodReturnExpression);
                break;
            case ObjectInitializerExpression objectInitializerExpression:
                CompileObjectInitializerExpression(objectInitializerExpression);
                break;
            case StaticMemberAccessExpression staticMemberAccessExpression:
                CompileStaticMemberAccessExpression(staticMemberAccessExpression);
                break;
            case TupleExpression tupleExpression:
                CompileTupleExpression(tupleExpression);
                break;
            case UnaryOperatorExpression unaryOperatorExpression:
                CompileUnaryOperatorExpression(unaryOperatorExpression);
                break;
            case UnionClassVariantInitializerExpression unionClassVariantInitializerExpression:
                CompileUnionClassVariantInitializerExpression(unionClassVariantInitializerExpression);
                break;
            case ValueAccessorExpression valueAccessorExpression:
                CompileValueAccessorExpression(valueAccessorExpression);
                break;
            case VariableDeclarationExpression variableDeclarationExpression:
                CompileVariableDeclarationExpression(variableDeclarationExpression);
                break;
            case GenericInstantiationExpression:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }

        if (!expression.ValueUseful)
        {
            Instructions.Add(new Drop(NextAddress()));
        }
    }
    
    private void CompileBinaryOperatorExpression(
        BinaryOperatorExpression binaryOperatorExpression)
    {
        switch (binaryOperatorExpression.BinaryOperator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            {
                CompileExpression(
                    binaryOperatorExpression.BinaryOperator.Left ??
                                  throw new InvalidOperationException("Expected valid left expression"));

                CompileExpression(
                    binaryOperatorExpression.BinaryOperator.Right ??
                                  throw new InvalidOperationException("Expected valid right expression"));
                
                Instructions.Add(new CompareIntLessThan(NextAddress()));
                break;
            }
            case BinaryOperatorType.GreaterThan:
            {
                CompileExpression(
                    binaryOperatorExpression.BinaryOperator.Left ??
                                  throw new InvalidOperationException("Expected valid left expression"));

                CompileExpression(
                    binaryOperatorExpression.BinaryOperator.Right ??
                                  throw new InvalidOperationException("Expected valid right expression"));

                Instructions.Add(new CompareIntGreaterThan(NextAddress()));
                break;
            }
            case BinaryOperatorType.Plus:
                break;
            case BinaryOperatorType.Minus:
                break;
            case BinaryOperatorType.Multiply:
                break;
            case BinaryOperatorType.Divide:
                break;
            case BinaryOperatorType.EqualityCheck:
                break;
            case BinaryOperatorType.ValueAssignment:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    private static void CompileBlockExpression(
        BlockExpression blockExpression)
    {
    }
    
    private static void CompileIfExpression(
        IfExpressionExpression ifExpression)
    {
    }
    
    private static void CompileMatchesExpression(
        MatchesExpression matchesExpression)
    {
    }
    
    private static void CompileMatchExpression(
        MatchExpression matchExpression)
    {
    }
    
    private static void CompileMemberAccessExpression(
        MemberAccessExpression memberAccessExpression)
    {
    }
    
    private static void CompileMethodCallExpression(
        MethodCallExpression methodCallExpression)
    {
    }
    
    private void CompileMethodReturnExpression(
        MethodReturnExpression methodReturnExpression)
    {
        if (methodReturnExpression.MethodReturn.Expression is not null)
        {
            CompileExpression(methodReturnExpression.MethodReturn.Expression);
        }
        Instructions.Add(new Return(NextAddress()));
    }
    
    private static void CompileObjectInitializerExpression(
        ObjectInitializerExpression objectInitializerExpression)
    {
    }
    
    private static void CompileStaticMemberAccessExpression(
        StaticMemberAccessExpression staticMemberAccessExpression)
    {
    }
    
    private static void CompileTupleExpression(
        TupleExpression tupleExpression)
    {
    }
    
    private static void CompileUnaryOperatorExpression(
        UnaryOperatorExpression unaryOperatorExpression)
    {
    }
    
    private static void CompileUnionClassVariantInitializerExpression(
        UnionClassVariantInitializerExpression unionClassVariantInitializerExpression)
    {
    }

    private void CompileValueAccessorExpression(
        ValueAccessorExpression valueAccessorExpression)
    {
        switch (valueAccessorExpression.ValueAccessor)
        {
            case
            {
                AccessType: ValueAccessType.Literal,
                Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue }
            }:
            {
                Instructions.Add(new LoadIntConstant(NextAddress(), intValue));
                break;
            }
            case
            {
                AccessType: ValueAccessType.Literal,
                Token: StringToken { Type: TokenType.StringLiteral, StringValue: var stringValue }
            }:
            {
                Instructions.Add(new LoadStringConstant(NextAddress(), stringValue));
                break;
            }
            case { AccessType: ValueAccessType.Literal, Token.Type: TokenType.True }:
            {
                Instructions.Add(new LoadBoolConstant(NextAddress(), true));
                break;
            }
            case { AccessType: ValueAccessType.Literal, Token.Type: TokenType.False }:
            {
                Instructions.Add(new LoadBoolConstant(NextAddress(), false));
                break;
            }
            case
            {
                AccessType: ValueAccessType.Variable,
                Token: StringToken { Type: TokenType.Identifier, StringValue: "this" }
            }:
            {
                // this is always the first argument
                Instructions.Add(new LoadArgument(NextAddress(), ArgumentIndex: 0));
                break;
            }
            case { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Identifier }:
            {
                var referencedVariable = valueAccessorExpression.ReferencedVariable ??
                                         throw new InvalidOperationException(
                                             "Expected referenced variable");

                var scope = _scopeStack.Peek();
                var isOuter = scope.AccessedOuterVariables.Contains(referencedVariable);

                switch (referencedVariable)
                {
                    case TypeChecker.FieldVariable fieldVariable when isOuter:
                        break;
                    case TypeChecker.FieldVariable fieldVariable:
                    {
                        if (fieldVariable.IsStaticField)
                        {
                            Instructions.Add(new LoadStaticField(
                                NextAddress(),
                                scope.CurrentType ?? throw new InvalidOperationException("Expected current type to be set when referencing static fields via variable"),
                                VariantIndex: 0,
                                StaticFieldIndex: fieldVariable.FieldIndex
                            ));
                        }
                        else
                        {
                            Instructions.Add(new LoadArgument(NextAddress(), 0));
                            Instructions.Add(new LoadField(
                                NextAddress(),
                                VariantIndex: 0, // Accessing fields via named variables can only be done for classes
                                FieldIndex: fieldVariable.FieldIndex));
                        }
                        
                        break;
                    }
                    case TypeChecker.FunctionParameterVariable functionParameterVariable when isOuter:
                        break;
                    case TypeChecker.FunctionParameterVariable functionParameterVariable:
                    {
                        var parameterIndex = functionParameterVariable.ParameterIndex;
                        var fn = functionParameterVariable.ContainingFunction;
                        if (!fn.IsStatic
                            || fn.AccessedOuterVariables.Count > 0)
                        {
                            // for instance functions or closures, parameters need to be bumped by 1 because of either the closure object or `this`
                            parameterIndex++;
                        }
                        
                        Instructions.Add(new LoadArgument(NextAddress(), parameterIndex));
                        break;
                    }
                    case TypeChecker.LocalVariable localVariable when isOuter:
                        break;
                    case TypeChecker.LocalVariable localVariable:
                    {
                        Instructions.Add(new LoadLocal(NextAddress(), localVariable.VariableIndex ?? throw new InvalidOperationException("Expected variable index")));
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(referencedVariable));
                }
                
                break;
            }
        }
    }

    private void CompileVariableDeclarationExpression(
        VariableDeclarationExpression variableDeclarationExpression)
    {
        if (variableDeclarationExpression.VariableDeclaration.Variable is not TypeChecker.LocalVariable localVariable)
        {
            throw new InvalidOperationException("LocalVariable should be set");
        }

        if (variableDeclarationExpression.VariableDeclaration.Value is null)
        {
            return;
        }

        CompileExpression(variableDeclarationExpression.VariableDeclaration.Value);
            
        var index = localVariable.VariableIndex ?? throw new InvalidOperationException("Expected variable index to be set");

        Instructions.Add(new StoreLocal(NextAddress(), index));
    }

    private IReefTypeReference DefinitionAsTypeReference(ReefTypeDefinition definition)
    {
        return new ConcreteReefTypeReference
        {
            DefinitionId = definition.Id,
            Name = definition.DisplayName,
            TypeArguments = definition.TypeParameters.Select(x => new GenericReefTypeReference
            {
                DefinitionId = definition.Id,
                TypeParameterName = x
            }).ToArray()
        };
    }
}
