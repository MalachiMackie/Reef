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
        public required TypeChecker.FunctionSignature? CurrentFunction { get; init; }
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
            .Select(x => CompileMethod(x.Signature!)));
        
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
                program.Functions.Select(x => x.Signature!).Where(x => !x.IsStatic).ToArray(),
                functionIndex: null
            )
            {
                ReturnType = TypeChecker.InstantiatedClass.Unit,
                LocalVariables = [..program.TopLevelLocalVariables],
                OwnerType = null,
            });
            
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
        TypeChecker.FunctionSignature function)
    {
        foreach (var innerMethod in function.LocalFunctions)
        {
            _methods.Add(CompileMethod(innerMethod));
        }
        
        var parameters = new List<ReefMethod.Parameter>();
        var ownerTypeReference = function.OwnerType is null
            ? null
            : SignatureAsTypeReference(function.OwnerType);
        if (!function.IsStatic && ownerTypeReference is not null)
        {
            parameters.Add(new ReefMethod.Parameter
            {
                DisplayName = "this",
                Type = ownerTypeReference
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
            function.ClosureTypeId = closureType.Id;
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
            CurrentType = ownerTypeReference,
            CurrentFunction = function
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
        methods.AddRange(union.Functions.Select(CompileMethod));

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
                    CurrentType = SignatureAsTypeReference(@class),
                    CurrentFunction = null
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
        
        methods.AddRange(@class.Functions.Select(CompileMethod));

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
    
    private void CompileMemberAccessExpression(
        MemberAccessExpression memberAccessExpression)
    {
        var memberIndex = memberAccessExpression.MemberAccess.ItemIndex
            ?? throw new InvalidOperationException("Expected member item index");
        var memberType = memberAccessExpression.MemberAccess.MemberType
            ?? throw new InvalidOperationException("Expected member type");
        var ownerType = memberAccessExpression.MemberAccess.OwnerType
                        ?? throw new InvalidOperationException("Expected member access owner type");
        var ownerReference = GetTypeReference(ownerType) as ConcreteReefTypeReference
                             ?? throw new InvalidOperationException(
                                 "Expected member access owner type to be concrete type");

        CompileExpression(memberAccessExpression.MemberAccess.Owner);
            
        if (memberType == MemberType.Function)
        {
            Instructions.Add(new LoadTypeFunction(NextAddress(), ownerReference, memberIndex));
        }
        else if (memberType == MemberType.Field)
        {
            Instructions.Add(new LoadField(NextAddress(), 0, memberIndex));
        }
    }
    
    private void CompileMethodCallExpression(
        MethodCallExpression methodCallExpression)
    {
        CompileExpression(methodCallExpression.MethodCall.Method);

        // move instruction to load function to after the argument list loading instructions
        var loadFunctionInstruction = Instructions[^1];
        Instructions.RemoveAt(Instructions.Count - 1);
        
        foreach (var argument in methodCallExpression.MethodCall.ArgumentList)
        {
            CompileExpression(argument);
        }
        
        Instructions.Add(loadFunctionInstruction switch
        {
            LoadGlobalFunction loadGlobalFunction => loadGlobalFunction with {Address = NextAddress()},
            LoadTypeFunction loadTypeFunction => loadTypeFunction with {Address = NextAddress()},
            _ => throw new InvalidOperationException("Expected instruction to be function load instruction")
        });
        
        Instructions.Add(new Call(NextAddress()));
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
    
    private void CompileObjectInitializerExpression(
        ObjectInitializerExpression objectInitializerExpression)
    {
        var type = GetTypeReference(objectInitializerExpression.ResolvedType ?? throw new InvalidOperationException("Expected object initializer type"));
        if (type is not ConcreteReefTypeReference concreteTypeReference)
        {
            throw new InvalidOperationException("Expected object initializer type to be concrete");
        }
        Instructions.Add(new CreateObject(NextAddress(), concreteTypeReference));

        foreach (var initializer in objectInitializerExpression.ObjectInitializer.FieldInitializers)
        {
            if (initializer.TypeField is null)
            {
                throw new InvalidOperationException("Expected FieldInitializer fieldVariable to be set");
            }
            Instructions.Add(new CopyStack(NextAddress()));
            CompileExpression(initializer.Value ?? throw new InvalidOperationException("Expected Value to be set"));
            Instructions.Add(new StoreField(NextAddress(), 0, initializer.TypeField.FieldIndex));
        }
    }
    
    private void CompileStaticMemberAccessExpression(
        StaticMemberAccessExpression staticMemberAccessExpression)
    {
        var ownerType = staticMemberAccessExpression.OwnerType
                        ?? throw new InvalidOperationException("Expected static member access ownerType");

        var ownerReference = GetTypeReference(ownerType) as ConcreteReefTypeReference
            ?? throw new InvalidOperationException("Expected owner type to be concrete type");
        
        var itemIndex = staticMemberAccessExpression.StaticMemberAccess.ItemIndex
            ?? throw new InvalidOperationException("Expected ItemIndex to be set");

        var memberType = staticMemberAccessExpression.StaticMemberAccess.MemberType
                         ?? throw new InvalidOperationException("Expected MemberType to be set");
        
        if (memberType == MemberType.Function)
        {
            Instructions.Add(new LoadTypeFunction(NextAddress(), ownerReference, itemIndex));
        }
        else if (memberType == MemberType.Field)
        {
            Instructions.Add(new LoadStaticField(NextAddress(), ownerReference, VariantIndex: 0, itemIndex));
        }
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
                CompileIdentifierValueAccessor(valueAccessorExpression);
                break;
            }
        }
    }

    private void CompileFunctionReference(TypeChecker.InstantiatedFunction instantiatedFunction)
    {
        if (instantiatedFunction.OwnerType is null)
        {
            if (instantiatedFunction.AccessedOuterVariables.Count > 0)
            {
                var closureTypeId = instantiatedFunction.ClosureTypeId ??
                                    throw new InvalidOperationException("Expected closure type id");
                var type = _types.First(x => x.Id == closureTypeId);
                Instructions.Add(new CreateObject(NextAddress(), new ConcreteReefTypeReference
                {
                    DefinitionId = type.Id,
                    Name = type.DisplayName,
                    TypeArguments = []
                }));

                foreach (var (index, outerVariable) in instantiatedFunction.AccessedOuterVariables.Index())
                {
                    Instructions.Add(new CopyStack(NextAddress()));

                    var currentFunction = _scopeStack.Peek().CurrentFunction
                                          ?? throw new InvalidOperationException(
                                              "Expected to be in a function when referencing outer variables");
                    
                    // load value
                    switch (outerVariable)
                    {
                        case TypeChecker.FieldVariable:
                            throw new InvalidOperationException("Unexpected field variable");
                        case TypeChecker.FunctionParameterVariable functionParameterVariable
                            when currentFunction == functionParameterVariable.ContainingFunction:
                        {
                            Instructions.Add(new LoadArgument(NextAddress(), functionParameterVariable.ParameterIndex));
                            break;
                        }
                        case TypeChecker.FunctionParameterVariable functionParameterVariable:
                        {
                            var indexInCurrentFunction = currentFunction.AccessedOuterVariables.Index().First(x => x.Item == outerVariable).Index;
                            
                            // we're referencing a parameter that is not our own parameter, so it will be in the closure capture
                            Instructions.Add(new LoadArgument(NextAddress(), 0));
                            Instructions.Add(new LoadField(NextAddress(), 0, (uint)indexInCurrentFunction));
                            break;
                        }
                        case TypeChecker.LocalVariable localVariable
                            when currentFunction == localVariable.ContainingFunction
                                || currentFunction.Name == "!Main" && localVariable.ContainingFunction is null:
                        {
                            Instructions.Add(new LoadLocal(NextAddress(), localVariable.LocalIndex ?? throw new InvalidOperationException("Expected local index")));
                            break;
                        }
                        case TypeChecker.LocalVariable:
                        {
                            var indexInCurrentFunction = currentFunction.AccessedOuterVariables.Index().First(x => x.Item == outerVariable).Index;
                            // we're referencing a local that is not our own local, so it will be in the closure capture
                            Instructions.Add(new LoadArgument(NextAddress(), 0));
                            Instructions.Add(new LoadField(NextAddress(), 0, (uint)indexInCurrentFunction));
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(outerVariable));
                    }
                    
                    Instructions.Add(new StoreField(NextAddress(), 0, (uint)index));
                }
            }
            
            Instructions.Add(new LoadGlobalFunction(NextAddress(), new FunctionReference
            {
                Name = instantiatedFunction.Name,
                TypeArguments = instantiatedFunction.TypeArguments.Select(GetTypeReference).ToArray(),
                DefinitionId = instantiatedFunction.FunctionId
            }));
        }
    }

    private void CompileIdentifierValueAccessor(ValueAccessorExpression valueAccessorExpression)
    {
        if (valueAccessorExpression.ResolvedType is TypeChecker.InstantiatedFunction instantiatedFunction)
        {
            CompileFunctionReference(instantiatedFunction);
            return;
        }
        
        var referencedVariable = valueAccessorExpression.ReferencedVariable ??
                                 throw new InvalidOperationException(
                                     "Expected referenced variable");

        var scope = _scopeStack.Peek();
        var (accessedOuterVariableIndex, accessedOuterVariable) = scope.AccessedOuterVariables.Index()
            .FirstOrDefault(x => x.Item == referencedVariable);
        
        if (accessedOuterVariable is not null)
        {
            Instructions.Add(new LoadArgument(NextAddress(), 0));
            Instructions.Add(new LoadField(NextAddress(), VariantIndex: 0, FieldIndex: (uint)accessedOuterVariableIndex));
            return;
        }

        switch (referencedVariable)
        {
            case TypeChecker.FieldVariable fieldVariable:
            {
                if (fieldVariable.IsStaticField)
                {
                    Instructions.Add(new LoadStaticField(
                        NextAddress(),
                        scope.CurrentType ?? throw new InvalidOperationException("Expected current type to be set when referencing static fields via variable"),
                        VariantIndex: 0,
                        FieldIndex: fieldVariable.FieldIndex
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
            case TypeChecker.LocalVariable localVariable:
            {
                Instructions.Add(new LoadLocal(NextAddress(), localVariable.LocalIndex ?? throw new InvalidOperationException("Expected variable index")));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(referencedVariable));
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
            
        var index = localVariable.LocalIndex ?? throw new InvalidOperationException("Expected variable index to be set");

        Instructions.Add(new StoreLocal(NextAddress(), index));
    }

    private ConcreteReefTypeReference SignatureAsTypeReference(TypeChecker.ITypeSignature definition)
    {
        var typeParameters = definition switch
        {
            TypeChecker.ClassSignature classSignature => classSignature.TypeParameters,
            TypeChecker.UnionSignature unionSignature => unionSignature.TypeParameters,
            _ => []
        };
        return new ConcreteReefTypeReference
        {
            DefinitionId = definition.Id,
            Name = definition.Name,
            TypeArguments = typeParameters.Select(GetTypeReference).ToArray()
        };
    }
}
