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
    private TypeChecker.FunctionSignature? _mainFunction;
    private List<IInstruction> Instructions => _scopeStack.Peek().Instructions;

    private class Scope
    {
        public List<IInstruction> Instructions { get; } = [];
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
            _mainFunction = new TypeChecker.FunctionSignature(
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
            };
            mainMethod = CompileMethod(_mainFunction);
            
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
        var locals = new List<ReefMethod.Local>();
        var localsFields = new List<(ReefField localsField, TypeChecker.IVariable variable, uint? parameterIndex, uint fieldIndex)>();
        foreach (var (i, variable) in function.Parameters.Values.Index().Where(x => x.Item.ReferencedInClosure))
        {
            localsFields.Add((new ReefField
            {
                DisplayName = $"Field_{localsFields.Count}",
                Type = GetTypeReference(variable.Type),
                IsPublic = true,
                IsStatic = false,
                StaticInitializerInstructions = []
            }, variable, (uint?)i, (uint)localsFields.Count));
        }

        foreach (var local in function.LocalVariables.Where(x => x.ReferencedInClosure))
        {
            localsFields.Add((new ReefField
            {
                DisplayName = $"Field_{localsFields.Count}",
                Type = GetTypeReference(local.Type ?? throw new InvalidOperationException("Expected local to have a type")),
                IsPublic = true,
                IsStatic = false,
                StaticInitializerInstructions = []
            }, local, null, (uint)localsFields.Count));
        }

        ReefTypeDefinition? localsDefinition = null;

        if (localsFields.Count > 0)
        {
            localsDefinition = new ReefTypeDefinition
            {
                DisplayName = $"{function.Name}_Locals",
                Id = Guid.NewGuid(),
                IsValueType = false,
                Methods = [],
                TypeParameters = [],
                Variants = [
                    new ReefVariant
                    {
                        DisplayName = "!ClassVariant",
                        Fields = localsFields.Select(x => x.localsField).ToArray()
                    }
                ]
            };
            _types.Add(localsDefinition);
            if (function.Expressions.Count > 0)
            {
                locals.Add(new ReefMethod.Local
                {
                    DisplayName = "locals",
                    Type = new ConcreteReefTypeReference
                    {
                        Name = localsDefinition.DisplayName,
                        DefinitionId = localsDefinition.Id,
                        TypeArguments = []
                    }
                });
            }

            function.LocalsTypeFields = localsFields.Select(x => x.variable).ToArray();
            function.LocalsTypeId = localsDefinition.Id;
        }
        
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
            var closureParameters = new List<(ConcreteReefTypeReference parameterTypeReference, List<(TypeChecker.IVariable, uint fieldIndex)> parameterVariables)>();
            foreach (var variable in function.AccessedOuterVariables)
            {
                var ownerFunction = variable switch 
                {
                    TypeChecker.FieldVariable => throw new InvalidOperationException(
                        "Accessed outer variable cannot be a function"),
                    TypeChecker.FunctionParameter parameterVariable => 
                        parameterVariable.ContainingFunction,
                    TypeChecker.LocalVariable localVariable =>
                        (localVariable.ContainingFunction ?? _mainFunction) ?? throw new InvalidOperationException(""),
                    _ => throw new ArgumentOutOfRangeException(nameof(variable))
                };
                
                var ownerLocalsTypeId = ownerFunction.LocalsTypeId ?? throw new InvalidOperationException("Expected owner function to have locals type id");
                var ownerLocalsTypeFields = ownerFunction.LocalsTypeFields;

                var typeDefinition = _types.First(x => x.Id == ownerLocalsTypeId);
                var reference = new ConcreteReefTypeReference
                {
                    DefinitionId = ownerLocalsTypeId,
                    Name = typeDefinition.DisplayName,
                    TypeArguments = []
                };
                
                List<(TypeChecker.IVariable, uint)>? foundParameterVariables = null;
                foreach (var (parameterTypeReference, parameterVariables) in closureParameters)
                {
                    if (parameterTypeReference.DefinitionId == reference.DefinitionId)
                    {
                        foundParameterVariables = parameterVariables;
                        break;
                    }
                }

                if (foundParameterVariables is null)
                {
                    foundParameterVariables = [];
                    closureParameters.Add((reference, foundParameterVariables));
                }

                var fieldIndex = ownerLocalsTypeFields.Index().First(x => x.Item == variable).Index;
                
                foundParameterVariables.Add((variable, (uint)fieldIndex));
            }
            
            parameters.AddRange(closureParameters.Select((x, i) => new ReefMethod.Parameter
            {
                Type = x.parameterTypeReference,
                DisplayName = $"ClosureParameter_{i}"
            }));
            function.ClosureParameters = closureParameters
                .Select(x => (x.parameterTypeReference.DefinitionId, x.parameterVariables)).ToList();
        }
        parameters.AddRange(function.Parameters.Select(x => new ReefMethod.Parameter
        {
            DisplayName = x.Key,
            Type = GetTypeReference(x.Value.Type)
        }));

        locals.AddRange(function.LocalVariables.Where(x => !x.ReferencedInClosure).Select(x => new ReefMethod.Local
        {
            Type = GetTypeReference(x.Type ?? throw new InvalidOperationException("Expected type")),
            DisplayName = x.Name.StringValue
        }));

        _scopeStack.Push(new Scope
        {
            CurrentType = ownerTypeReference,
            CurrentFunction = function
        });

        if (localsFields.Count > 0 && localsDefinition is not null && function.Expressions.Count > 0)
        {
            Instructions.Add(new CreateObject(NextAddress(), new ConcreteReefTypeReference
            {
                Name = localsDefinition.DisplayName,
                DefinitionId = localsDefinition.Id,
                TypeArguments = []
            }));
            Instructions.Add(new StoreLocal(NextAddress(), 0));
            foreach (var (_, _, argumentIndex, fieldIndex) in localsFields.Where(x => x.parameterIndex.HasValue))
            {
                Instructions.Add(new LoadArgument(NextAddress(), argumentIndex!.Value));
                Instructions.Add(new LoadLocal(NextAddress(), 0));
                Instructions.Add(new StoreField(NextAddress(), VariantIndex: 0, FieldIndex: fieldIndex));
            }
        }

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
        var unionType = SignatureAsTypeReference(union);
        List<ReefMethod> methods = [..union.Functions.Select(CompileMethod)];
        
        var variants = new List<ReefVariant>(union.Variants.Count);

        var variantIdentifierField = new ReefField
        {
            DisplayName = "_variantIdentifier",
            IsStatic = false,
            IsPublic = true,
            StaticInitializerInstructions = [],
            Type = GetTypeReference(TypeChecker.InstantiatedClass.Int)
        };
        
        foreach (var (variantIndex, variant) in union.Variants.Index())
        {
            ReefVariant? reefVariant;
            switch (variant)
            {
                case TypeChecker.ClassUnionVariant classUnionVariant:
                {
                    reefVariant = new ReefVariant
                    {
                        DisplayName = variant.Name,
                        Fields = classUnionVariant.Fields.Select(y => new ReefField
                            {
                                DisplayName = y.Name,
                                IsStatic = false,
                                IsPublic = true,
                                Type = GetTypeReference(y.Type),
                                StaticInitializerInstructions = []
                            })
                            .Prepend(variantIdentifierField)
                            .ToArray()
                    };
                    break;
                }
                case TypeChecker.TupleUnionVariant tupleUnionVariant:
                {
                    reefVariant = new ReefVariant
                    {
                        DisplayName = variant.Name,
                        Fields = tupleUnionVariant.TupleMembers.Select((y, i) => new ReefField
                            {
                                DisplayName = TypeChecker.ClassSignature.TupleFieldNames[i],
                                IsStatic = false,
                                IsPublic = true,
                                StaticInitializerInstructions = [],
                                Type = GetTypeReference(y)
                            })
                            .Prepend(variantIdentifierField)
                            .ToArray()
                    };
                    
                    _scopeStack.Push(new Scope
                    {
                        CurrentFunction = null,
                        CurrentType = unionType,
                    });
                    
                    Instructions.Add(new CreateObject(NextAddress(), unionType));
                    Instructions.Add(new CopyStack(NextAddress()));
                    Instructions.Add(new LoadIntConstant(NextAddress(), variantIndex));
                    Instructions.Add(new StoreField(NextAddress(), (uint)variantIndex, 0));

                    var parameters = new List<ReefMethod.Parameter>();
                    foreach (var (memberIndex, member) in tupleUnionVariant.TupleMembers.Index())
                    {
                        Instructions.Add(new CopyStack(NextAddress()));
                        Instructions.Add(new LoadArgument(NextAddress(), (uint)memberIndex));
                        Instructions.Add(new StoreField(NextAddress(), (uint)variantIndex, (uint)memberIndex + 1));
                        var memberType = GetTypeReference(member);
                        parameters.Add(new ReefMethod.Parameter
                        {
                            DisplayName = TypeChecker.ClassSignature.TupleFieldNames[memberIndex],
                            Type = memberType,
                        });
                    }
                    
                    Instructions.Add(new Return(NextAddress()));

                    var createFnInstructions = _scopeStack.Pop().Instructions;
                    
                    methods.Add(new ReefMethod
                    {
                        DisplayName = $"{union.Name}_{variant.Name}_Create",
                        IsStatic = true,
                        TypeParameters = [],
                        Locals = [],
                        Parameters = parameters,
                        ReturnType = unionType,
                        Instructions = createFnInstructions
                    });
                    break;
                }
                case TypeChecker.UnitUnionVariant:
                {
                    reefVariant = new ReefVariant { DisplayName = variant.Name, Fields = [variantIdentifierField]};
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(variant));
            }
            
            variants.Add(reefVariant);
        }
        var definition = new ReefTypeDefinition
        {
            Id = union.Id,
            DisplayName = union.Name,
            IsValueType = false,
            Variants = variants,
            TypeParameters = union.TypeParameters.Select(x => x.GenericName).ToArray(),
            Methods = methods
        };

        return definition;
    }

    private ReefTypeDefinition CompileClass(TypeChecker.ClassSignature @class)
    {
        var methods = new List<ReefMethod>();
        var fields = new List<ReefField>();
        
        foreach (var field in @class.Fields)
        {
            var staticInitializerInstructions = new List<IInstruction>();
            if (field.StaticInitializer is not null)
            {
                _scopeStack.Push(new Scope
                {
                    CurrentType = SignatureAsTypeReference(@class),
                    CurrentFunction = null
                });
                CompileExpression(field.StaticInitializer);
                var scope = _scopeStack.Pop();

                staticInitializerInstructions = scope.Instructions;
            }
            
            fields.Add(new ReefField
            {
                IsStatic = field.IsStatic,
                IsPublic = field.IsPublic,
                StaticInitializerInstructions = staticInitializerInstructions,
                DisplayName = field.Name,
                Type = GetTypeReference(field.Type)
            });
        }
        
        var definition = new ReefTypeDefinition
        {
            Id = @class.Id,
            DisplayName = @class.Name,
            IsValueType = false,
            Variants = [new ReefVariant
            {
                DisplayName = "!ClassVariant",
                Fields = fields,
            }],
            TypeParameters = @class.TypeParameters.Select(x => x.GenericName).ToArray(),
            Methods = methods,
        };
        
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
                TypeArguments = [..instantiatedClass.TypeArguments.Select(GetTypeReference)]
            },
            TypeChecker.InstantiatedUnion instantiatedUnion => new ConcreteReefTypeReference
            {
                Name = instantiatedUnion.Signature.Name,
                DefinitionId = instantiatedUnion.Signature.Id,
                TypeArguments = [..instantiatedUnion.TypeArguments.Select(GetTypeReference)]
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
            if (Instructions[^1] is LoadUnitConstant loadUnit)
            {
                Instructions.Remove(loadUnit);
            }
            else
            {
                Instructions.Add(new Drop(NextAddress()));
            }
        }
    }
    
    private void CompileBinaryOperatorExpression(
        BinaryOperatorExpression binaryOperatorExpression)
    {
        var right = binaryOperatorExpression.BinaryOperator.Right 
                    ?? throw new InvalidOperationException("Expected right operand");
        if (binaryOperatorExpression.BinaryOperator.OperatorType == BinaryOperatorType.ValueAssignment)
        {
            switch (binaryOperatorExpression.BinaryOperator.Left)
            {
                case StaticMemberAccessExpression staticMemberAccessExpression:
                {
                    CompileExpression(right);
                    
                    var ownerType = staticMemberAccessExpression.OwnerType
                                    ?? throw new InvalidOperationException("Expected static member access owner");

                    Instructions.Add(new StoreStaticField(
                        NextAddress(),
                        GetTypeReference(ownerType),
                        staticMemberAccessExpression.StaticMemberAccess.ItemIndex
                            ?? throw new InvalidOperationException("Expected static member access item index")));
                    break;
                }
                case MemberAccessExpression memberAccessExpression:
                {
                    // load the owner
                    CompileExpression(memberAccessExpression.MemberAccess.Owner);
                    
                    // load the value
                    CompileExpression(right);
                    
                    // variant index 0 because it's never valid to assign to a union field
                    Instructions.Add(new StoreField(
                        NextAddress(),
                        0,
                        memberAccessExpression.MemberAccess.ItemIndex
                            ?? throw new InvalidOperationException("Expected member access item index")));
                    
                    break;
                }
                case ValueAccessorExpression {ValueAccessor: {AccessType: ValueAccessType.Variable, Token.Type: TokenType.Identifier}} valueAccessorExpression:
                {
                    CompileExpression(right);

                    if (valueAccessorExpression.ReferencedVariable is not TypeChecker.LocalVariable localVariable)
                    {
                        throw new InvalidOperationException("Expected local variable");
                    }
                    
                    AssignToLocal(localVariable);
                    break;
                }
                default:
                    throw new InvalidOperationException("Unexpected operator type");
            }

            return;
        }
        
        CompileExpression(
            binaryOperatorExpression.BinaryOperator.Left ??
            throw new InvalidOperationException("Expected valid left expression"));

        CompileExpression(
            binaryOperatorExpression.BinaryOperator.Right ??
            throw new InvalidOperationException("Expected valid right expression"));

        IInstruction instruction = binaryOperatorExpression.BinaryOperator.OperatorType switch
        {
            BinaryOperatorType.LessThan => new CompareIntLessThan(NextAddress()),
            BinaryOperatorType.GreaterThan => new CompareIntGreaterThan(NextAddress()),
            BinaryOperatorType.Plus => new IntPlus(NextAddress()),
            BinaryOperatorType.Minus => new IntMinus(NextAddress()),
            BinaryOperatorType.Multiply => new IntMultiply(NextAddress()),
            BinaryOperatorType.Divide => new IntDivide(NextAddress()),
            BinaryOperatorType.EqualityCheck => new CompareIntEqual(NextAddress()),
            _ => throw new ArgumentOutOfRangeException()
        };
        Instructions.Add(instruction);
    }

    private uint GetLocalIndex(TypeChecker.LocalVariable localVariable)
    {
        var currentFunction = _scopeStack.Peek().CurrentFunction ?? throw new InvalidOperationException("Expected to be in function");

        uint localIndex = 0;
        var found = false;
        foreach (var (i, local) in currentFunction.LocalVariables.Where(x => !x.ReferencedInClosure).Index())
        {
            if (local == localVariable)
            {
                localIndex = (uint)i;
                found = true;
                break;
            }
        }
        if (!found)
        {
            throw new InvalidOperationException("Expected local to be found in the current function");
        }

        if (currentFunction.LocalsTypeId.HasValue)
        {
            return localIndex + 1;
        }
        return localIndex;
    }

    private void AssignToLocal(TypeChecker.LocalVariable localVariable)
    {
        if (localVariable.ReferencedInClosure)
        {
            uint foundFieldIndex = 0;

            var currentFunction = _scopeStack.Peek().CurrentFunction
                                  ?? throw new InvalidOperationException("Expected current function");

            if (currentFunction == (localVariable.ContainingFunction ?? _mainFunction))
            {
                var found = false;
                foreach (var (fieldIndex, local) in currentFunction.LocalVariables.Where(x => x.ReferencedInClosure).Index())
                {
                    if (local != localVariable)
                    {
                        continue;
                    }

                    foundFieldIndex = (uint)fieldIndex;
                    found = true;
                    break;
                }
                
                if (!found)
                {
                    throw new InvalidOperationException("Expected to find local and field index");
                }
                Instructions.Add(new LoadLocal(NextAddress(), 0));
                Instructions.Add(new StoreField(NextAddress(), 0, foundFieldIndex));
            }
            else
            {
                var closureArgumentIndex = 0u;
                var closureFieldIndex = 0u;
                var found = false;
                foreach (var (argumentIndex, (_, fields)) in currentFunction.ClosureParameters.Index())
                {
                    foreach (var (field, fieldIndex) in fields)
                    {
                        if (ReferenceEquals(field, localVariable))
                        {
                            found = true;
                            closureArgumentIndex = (uint)argumentIndex;
                            closureFieldIndex = fieldIndex;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    throw new InvalidOperationException("Could not find referenced variable in owning function");
                }
                
                Instructions.Add(new LoadArgument(NextAddress(), closureArgumentIndex));
                Instructions.Add(new StoreField(NextAddress(), VariantIndex: 0, FieldIndex: closureFieldIndex));
            }
        }
        else
        {
            Instructions.Add(new StoreLocal(NextAddress(), GetLocalIndex(localVariable)));
        }
    }
    
    private void CompileBlockExpression(
        BlockExpression blockExpression)
    {
        foreach (var method in blockExpression.Block.Functions)
        {
            _methods.Add(CompileMethod(method.Signature ?? throw new InvalidOperationException("Expected function to have signature set")));
        }

        foreach (var expression in blockExpression.Block.Expressions)
        {
            CompileExpression(expression);
        }

        if (!blockExpression.Diverges)
        {
            Instructions.Add(new LoadUnitConstant(NextAddress()));
        }
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

        if (methodCallExpression.MethodCall.ArgumentList.Count > 0)
        {
            // move instruction to load function to after the argument list loading instructions
            if (Instructions.Count == 0)
            {
                throw new InvalidOperationException("Expected a function load instruction");
            }
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
        }
        
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
        else if (memberType == MemberType.Variant)
        {
            CompileStaticMemberAccessVariantReference(
                itemIndex,
                ownerType as TypeChecker.InstantiatedUnion ?? throw new InvalidOperationException("Expected type to be union"),
                ownerReference);
        }
    }

    private void CompileStaticMemberAccessVariantReference(
        uint variantIndex,
        TypeChecker.InstantiatedUnion unionType,
        ConcreteReefTypeReference unionReference)
    {
        var variant = unionType.Variants[(int)variantIndex];
        switch (variant)
        {
            case TypeChecker.ClassUnionVariant:
            {
                throw new InvalidOperationException("Should not be able to reference class variant");
            }
            case TypeChecker.TupleUnionVariant:
            {
                Instructions.Add(new LoadTypeFunction(
                    NextAddress(),
                    unionReference,
                    // tuple variant create functions are placed after all declared functions, and we need to skip any previous tuple union variants
                    (uint)(unionType.Signature.Functions.Count
                        + unionType.Signature.Variants.Take((int)variantIndex).Count(x => x is TypeChecker.TupleUnionVariant))));
                break;
            }
            case TypeChecker.UnitUnionVariant:
            {
                // create variant object
                Instructions.Add(new CreateObject(NextAddress(), unionReference));
                Instructions.Add(new CopyStack(NextAddress()));
                // store variant identifier
                Instructions.Add(new LoadIntConstant(NextAddress(), (int)variantIndex));
                Instructions.Add(new StoreField(NextAddress(), variantIndex, 0));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(variant));
        }
    }
    
    private void CompileTupleExpression(
        TupleExpression tupleExpression)
    {
        if (tupleExpression.Values.Count == 1)
        {
            CompileExpression(tupleExpression.Values[0]);
            return;
        }

        var typeReference = GetTypeReference(tupleExpression.ResolvedType
                         ?? throw new InvalidOperationException("Expected type"));

        if (typeReference is not ConcreteReefTypeReference concreteTypeReference)
        {
            throw new InvalidOperationException("Expected type to be concrete");
        }
        
        Instructions.Add(new CreateObject(NextAddress(), concreteTypeReference));
        foreach (var (index, member) in tupleExpression.Values.Index())
        {
            Instructions.Add(new CopyStack(NextAddress()));
            CompileExpression(member);
            Instructions.Add(new StoreField(NextAddress(), 0, (uint)index));
        }
    }
    
    private void CompileUnaryOperatorExpression(
        UnaryOperatorExpression unaryOperatorExpression)
    {
        var operand = unaryOperatorExpression.UnaryOperator.Operand
                                   ?? throw new InvalidOperationException("Expected value");
        switch (unaryOperatorExpression.UnaryOperator.OperatorType)
        {
            case UnaryOperatorType.FallOut:
                CompileFallout(operand);
                break;
            case UnaryOperatorType.Not:
            {
                CompileExpression(operand);
                Instructions.Add(new BoolNot(NextAddress()));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void CompileFallout(IExpression expression)
    {
        CompileExpression(expression);
        Instructions.Add(new CopyStack(NextAddress()));
        // load variant identifier
        Instructions.Add(new LoadField(NextAddress(), 0, 0));
        Instructions.Add(new LoadIntConstant(NextAddress(), 1));
        Instructions.Add(new CompareIntEqual(NextAddress()));
        var branchAddress = NextAddress();
        var returnAddress = new InstructionAddress(Index: branchAddress.Index + 1);
        var afterAddress = new InstructionAddress(returnAddress.Index + 1);
        Instructions.Add(new BranchIfFalse(branchAddress, afterAddress));
        Instructions.Add(new Return(returnAddress));
        // extract out the value of the successful variant
        Instructions.Add(new LoadField(afterAddress, 0, 1));
    }
    
    private void CompileUnionClassVariantInitializerExpression(
        UnionClassVariantInitializerExpression unionClassVariantInitializerExpression)
    {
        var unionType = GetTypeReference(unionClassVariantInitializerExpression.ResolvedType
            ?? throw new InvalidOperationException("Expected type to be set"));
        if (unionType is not ConcreteReefTypeReference concreteTypeReference)
        {
            throw new InvalidOperationException("Expected union type to be concrete type reference");
        }
        Instructions.Add(new CreateObject(NextAddress(), concreteTypeReference));
        Instructions.Add(new CopyStack(NextAddress()));
        var variantIndex = unionClassVariantInitializerExpression.UnionInitializer.VariantIndex
                            ?? throw new InvalidOperationException("Expected variant index");
        Instructions.Add(new LoadIntConstant(NextAddress(), (int)variantIndex));
        Instructions.Add(new StoreField(NextAddress(), variantIndex, 0));
        foreach (var fieldInitializer in unionClassVariantInitializerExpression.UnionInitializer.FieldInitializers)
        {
            Instructions.Add(new CopyStack(NextAddress()));
            CompileExpression(fieldInitializer.Value ?? throw new InvalidOperationException("Expected Value to be set"));
            var fieldIndex = fieldInitializer.TypeField?.FieldIndex
                ?? throw new InvalidOperationException("Expected FieldIndex to be set");
            Instructions.Add(new StoreField(NextAddress(), variantIndex, fieldIndex + 1));
        }
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
        if (instantiatedFunction.OwnerType is not null)
        {
            Instructions.Add(new LoadTypeFunction(
                NextAddress(),
                GetTypeReference(instantiatedFunction.OwnerType) as ConcreteReefTypeReference ?? throw new InvalidOperationException("Expected concrete type reference"),
                instantiatedFunction.FunctionIndex ?? throw new InvalidOperationException("Expected function index")));
            return;
        }

        if (instantiatedFunction.AccessedOuterVariables.Count > 0)
        {
            var currentFunction = _scopeStack.Peek().CurrentFunction
                                  ?? throw new InvalidOperationException(
                                      "Expected to be in a function when referencing outer variables");

            foreach (var (parameterTypeId, _) in instantiatedFunction.ClosureParameters)
            {
                if (parameterTypeId == currentFunction.LocalsTypeId)
                {
                    // load the current functions locals type
                    Instructions.Add(new LoadLocal(NextAddress(), 0));
                    continue;
                }

                foreach (var (i, (currentFunctionParameterTypeId, _)) in currentFunction.ClosureParameters.Index())
                {
                    if (currentFunctionParameterTypeId == parameterTypeId)
                    {
                        // load the expected closure parameter from the current function so it can be passed on
                        Instructions.Add(new LoadArgument(NextAddress(), (uint)i));
                    }
                }
            }
        }
            
        Instructions.Add(new LoadGlobalFunction(NextAddress(), new FunctionReference
        {
            Name = instantiatedFunction.Name,
            TypeArguments = instantiatedFunction.TypeArguments.Select(GetTypeReference).ToArray(),
            DefinitionId = instantiatedFunction.FunctionId
        }));
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

        if (referencedVariable.ReferencedInClosure)
        {
            var ownerFunction = referencedVariable switch
            {
                TypeChecker.FieldVariable => throw new InvalidOperationException("Accessed outer variable cannot be a field"),
                TypeChecker.FunctionParameter functionParameter => functionParameter.ContainingFunction,
                TypeChecker.LocalVariable localVariable => localVariable.ContainingFunction
                                                           ?? _mainFunction
                                                           ?? throw new InvalidOperationException("Expected function"),
                _ => throw new ArgumentOutOfRangeException(nameof(referencedVariable))
            };
            
            var currentFunction = scope.CurrentFunction ?? throw new InvalidOperationException("Expected to be in a function");

            if (ownerFunction == currentFunction)
            {
                Instructions.Add(new LoadLocal(NextAddress(), 0));
                var fieldIndex = currentFunction.LocalsTypeFields.Index().First(x => x.Item == referencedVariable).Index;
                Instructions.Add(new LoadField(NextAddress(), 0, (uint)fieldIndex));
                
                return;
            }

            var closureArgumentIndex = 0u;
            var closureFieldIndex = 0u;
            var found = false;
            foreach (var (argumentIndex, (_, fields)) in currentFunction.ClosureParameters.Index())
            {
                foreach (var (field, fieldIndex) in fields)
                {
                    if (field == referencedVariable)
                    {
                        found = true;
                        closureArgumentIndex = (uint)argumentIndex;
                        closureFieldIndex = fieldIndex;
                        break;
                    }
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("Could not find referenced variable in owning function");
            }
                
            Instructions.Add(new LoadArgument(NextAddress(), closureArgumentIndex));
            Instructions.Add(new LoadField(NextAddress(), VariantIndex: 0, FieldIndex: closureFieldIndex));
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
            case TypeChecker.FunctionParameter(var fn, _, _, _, var parameterIndex):
            {
                var adjustedParameterIndex = parameterIndex
                                     // increment parameter index by one to allow for the `this` parameter
                                     + (fn.IsGlobal || fn.IsStatic ? 0 : 1)
                                     // increment parameter index by the number of closure parameters we've got
                                     + fn.ClosureParameters.Count;
                
                Instructions.Add(new LoadArgument(NextAddress(), (uint)adjustedParameterIndex));
                break;
            }
            case TypeChecker.LocalVariable localVariable:
            {
                Instructions.Add(new LoadLocal(NextAddress(), GetLocalIndex(localVariable)));
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
        
        AssignToLocal(localVariable);
    }

    private static ConcreteReefTypeReference SignatureAsTypeReference(TypeChecker.ITypeSignature definition)
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
