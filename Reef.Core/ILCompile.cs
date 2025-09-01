using Reef.IL;
using Reef.Core.TypeChecking;
using Reef.Core.Expressions;

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
        var nextAddress = (uint)Instructions.Count;
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
                functionIndex: null
            )
            {
                ReturnType = TypeChecker.InstantiatedClass.Unit,
                LocalVariables = [.. program.TopLevelLocalVariables],
                LocalFunctions = [..program.Functions.Select(x => x.Signature!).Where(x => !x.IsStatic)],
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
            var closureTypeFields = new List<(ConcreteReefTypeReference parameterTypeReference, List<(TypeChecker.IVariable, uint fieldIndex)> parameterVariables)>();
            foreach (var variable in function.AccessedOuterVariables)
            {
                var ownerFunction = variable switch
                {
                    TypeChecker.FieldVariable => throw new InvalidOperationException(
                        "Accessed outer variable cannot be a function"),
                    TypeChecker.FunctionSignatureParameter parameterVariable =>
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
                foreach (var (parameterTypeReference, parameterVariables) in closureTypeFields)
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
                    closureTypeFields.Add((reference, foundParameterVariables));
                }

                var fieldIndex = ownerLocalsTypeFields.Index().First(x => x.Item == variable).Index;

                foundParameterVariables.Add((variable, (uint)fieldIndex));
            }

            var closureType = new ReefTypeDefinition
            {
                Id = Guid.NewGuid(),
                TypeParameters = [],
                DisplayName = $"{function.Name}_Closure",
                IsValueType = false,
                Methods = [],
                Variants = [
                    new ReefVariant
                    {
                        DisplayName = "!ClassVariant",
                        Fields = closureTypeFields.Select((x, i) => new ReefField
                        {
                            Type = x.parameterTypeReference,
                            DisplayName = $"Field_{i}",
                            IsPublic = true,
                            IsStatic = false,
                            StaticInitializerInstructions = []
                        }).ToArray()
                    }
                ]
            };

            function.ClosureTypeId = closureType.Id;

            _types.Add(closureType);

            parameters.Add(new ReefMethod.Parameter
            {
                Type = new ConcreteReefTypeReference
                {
                    Name = closureType.DisplayName,
                    DefinitionId = closureType.Id,
                    TypeArguments = []
                },
                DisplayName = "closureParameter"
            });

            function.ClosureTypeFields = closureTypeFields
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
                Instructions.Add(new LoadLocal(NextAddress(), 0));
                Instructions.Add(new LoadArgument(NextAddress(), argumentIndex!.Value));
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
        List<ReefMethod> methods = [.. union.Functions.Select(CompileMethod)];

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
                                DisplayName = TypeChecker.ClassSignature.TupleFieldName(i),
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
                                DisplayName = TypeChecker.ClassSignature.TupleFieldName(memberIndex),
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
                        reefVariant = new ReefVariant { DisplayName = variant.Name, Fields = [variantIdentifierField] };
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
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
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
                case ValueAccessorExpression { ValueAccessor: { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Identifier } } valueAccessorExpression:
                    {
                        if (valueAccessorExpression.ReferencedVariable is not TypeChecker.LocalVariable localVariable)
                        {
                            throw new InvalidOperationException("Expected local variable");
                        }

                        AssignToLocal(localVariable, right);
                        break;
                    }
                default:
                    throw new InvalidOperationException("Unexpected operator type");
            }

            return;
        }

        if (binaryOperatorExpression.BinaryOperator.OperatorType == BinaryOperatorType.BooleanAnd)
        {
            CompileBooleanAnd(
                binaryOperatorExpression.BinaryOperator.Left ?? throw new InvalidOperationException("Expected left expression"),
                binaryOperatorExpression.BinaryOperator.Right ?? throw new InvalidOperationException("Expected left expression"));
            return;
        }

        if (binaryOperatorExpression.BinaryOperator.OperatorType == BinaryOperatorType.BooleanOr)
        {
            CompileBooleanOr(
                binaryOperatorExpression.BinaryOperator.Left ?? throw new InvalidOperationException("Expected left expression"),
                binaryOperatorExpression.BinaryOperator.Right ?? throw new InvalidOperationException("Expected left expression"));
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

    private void CompileBooleanAnd(IExpression left, IExpression right)
    {
        CompileExpression(left);
        var branchIfFalseIndex1 = Instructions.Count;
        Instructions.Add(new BranchIfFalse(NextAddress(), null!));
        CompileExpression(right);
        var branchIfFalseIndex2 = Instructions.Count;
        Instructions.Add(new BranchIfFalse(NextAddress(), null!));
        Instructions.Add(new LoadBoolConstant(NextAddress(), true));
        var branchToEndIndex = Instructions.Count;
        Instructions.Add(new Branch(NextAddress(), null!));
        Instructions[branchIfFalseIndex1] = new BranchIfFalse(Instructions[branchIfFalseIndex1].Address,
            new InstructionAddress((uint)Instructions.Count));
        Instructions[branchIfFalseIndex2] = new BranchIfFalse(Instructions[branchIfFalseIndex2].Address,
            new InstructionAddress((uint)Instructions.Count));
        Instructions.Add(new LoadBoolConstant(NextAddress(), false));
        Instructions[branchToEndIndex] = new Branch(Instructions[branchToEndIndex].Address,
            new InstructionAddress((uint)Instructions.Count));
    }

    private void CompileBooleanOr(IExpression left, IExpression right)
    {
        CompileExpression(left);
        var branchIfTrueIndex1 = Instructions.Count;
        Instructions.Add(new BranchIfTrue(NextAddress(), null!));
        CompileExpression(right);
        var branchIfTrueIndex2 = Instructions.Count;
        Instructions.Add(new BranchIfTrue(NextAddress(), null!));
        Instructions.Add(new LoadBoolConstant(NextAddress(), false));
        var branchToEndIndex = Instructions.Count;
        Instructions.Add(new Branch(NextAddress(), null!));
        Instructions[branchIfTrueIndex1] = new BranchIfTrue(Instructions[branchIfTrueIndex1].Address,
            new InstructionAddress((uint)Instructions.Count));
        Instructions[branchIfTrueIndex2] = new BranchIfTrue(Instructions[branchIfTrueIndex2].Address,
            new InstructionAddress((uint)Instructions.Count));
        Instructions.Add(new LoadBoolConstant(NextAddress(), true));
        Instructions[branchToEndIndex] = new Branch(Instructions[branchToEndIndex].Address,
            new InstructionAddress((uint)Instructions.Count));
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

    private void AssignToLocal(TypeChecker.LocalVariable localVariable, IExpression expression)
    {
        if (!localVariable.ReferencedInClosure)
        {
            CompileExpression(expression);
            Instructions.Add(new StoreLocal(NextAddress(), GetLocalIndex(localVariable)));
            return;
        }

        uint foundFieldIndex = 0;

        var currentFunction = _scopeStack.Peek().CurrentFunction
                              ?? throw new InvalidOperationException("Expected current function");

        if (currentFunction == (localVariable.ContainingFunction ?? _mainFunction))
        {
            var foundLocalField = false;
            foreach (var (fieldIndex, local) in currentFunction.LocalVariables.Where(x => x.ReferencedInClosure)
                         .Index())
            {
                if (local != localVariable)
                {
                    continue;
                }

                foundFieldIndex = (uint)fieldIndex;
                foundLocalField = true;
                break;
            }

            if (!foundLocalField)
            {
                throw new InvalidOperationException("Expected to find local and field index");
            }

            Instructions.Add(new LoadLocal(NextAddress(), 0));
            CompileExpression(expression);
            Instructions.Add(new StoreField(NextAddress(), 0, foundFieldIndex));
            return;
        }

        var closureArgumentIndex = 0u;
        var closureFieldIndex = 0u;
        var found = false;
        foreach (var (argumentIndex, (_, fields)) in currentFunction.ClosureTypeFields.Index())
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

        // load closure object
        Instructions.Add(new LoadArgument(NextAddress(), 0));
        // load closure object field
        Instructions.Add(new LoadField(NextAddress(), 0, closureArgumentIndex));
        CompileExpression(expression);
        // store into closure object field
        Instructions.Add(new StoreField(NextAddress(), VariantIndex: 0, FieldIndex: closureFieldIndex));
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
    }

    private void CompileIfExpression(
        IfExpressionExpression ifExpression)
    {
        CompileExpression(ifExpression.IfExpression.CheckExpression);

        var jumpToEndIndices = new List<int>();
        var previousCheckIndex = Instructions.Count;

        Instructions.Add(new BranchIfFalse(NextAddress(), null!));
        CompileExpression(ifExpression.IfExpression.Body ?? throw new InvalidOperationException("Expected if body"));

        foreach (var elseIf in ifExpression.IfExpression.ElseIfs)
        {
            jumpToEndIndices.Add(Instructions.Count);
            Instructions.Add(new Branch(NextAddress(), null!));

            Instructions[previousCheckIndex] = new BranchIfFalse(
                new InstructionAddress((uint)previousCheckIndex),
                NextAddress());

            CompileExpression(elseIf.CheckExpression);

            previousCheckIndex = Instructions.Count;
            Instructions.Add(new BranchIfFalse(NextAddress(), null!));

            CompileExpression(elseIf.Body ?? throw new InvalidOperationException("Expected else if body"));
        }

        jumpToEndIndices.Add(Instructions.Count);
        Instructions.Add(new Branch(NextAddress(), null!));

        Instructions[previousCheckIndex] = new BranchIfFalse(
            new InstructionAddress((uint)previousCheckIndex),
            NextAddress());

        if (ifExpression.IfExpression.ElseBody is not null)
        {
            CompileExpression(ifExpression.IfExpression.ElseBody);
        }
        else
        {
            Instructions.Add(new LoadUnitConstant(NextAddress()));
        }

        var after = NextAddress();

        foreach (var index in jumpToEndIndices)
        {
            Instructions[index] = new Branch(Instructions[index].Address, after);
        }
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

        if (memberType == MemberType.Function)
        {
            if (memberAccessExpression.MemberAccess.InstantiatedFunction is not { } instantiatedFunction)
            {
                throw new InvalidOperationException("Expected instantiated function");
            }

            CreateFunctionObject(instantiatedFunction, memberAccessExpression.MemberAccess.Owner);
            return;
        }
        if (memberType == MemberType.Field)
        {
            CompileExpression(memberAccessExpression.MemberAccess.Owner);
            Instructions.Add(new LoadField(NextAddress(), 0, memberIndex));
            return;
        }

        throw new InvalidOperationException("Unexpected member type");
    }

    private void CompileMethodCallExpression(
        MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression.MethodCall.Method.ResolvedType is not TypeChecker.FunctionObject functionObject)
        {
            throw new InvalidOperationException("Expected function object");
        }

        var instantiatedFunction = methodCallExpression.MethodCall.Method switch
        {
            MemberAccessExpression { MemberAccess.InstantiatedFunction: var fn } => fn,
            StaticMemberAccessExpression { StaticMemberAccess.InstantiatedFunction: var fn } => fn,
            ValueAccessorExpression { FunctionInstantiation: var fn } => fn,
            _ => null
        };

        // calling function object
        if (instantiatedFunction is null)
        {
            // load the function object
            CompileExpression(methodCallExpression.MethodCall.Method);

            // load all the arguments
            foreach (var argument in methodCallExpression.MethodCall.ArgumentList)
            {
                CompileExpression(argument);
            }

            var functionSignature = TypeChecker.ClassSignature.Function(functionObject.Parameters.Count);
            var functionReference = new ConcreteReefTypeReference
            {
                DefinitionId = functionSignature.Id,
                Name = functionSignature.Name,
                TypeArguments = functionObject.Parameters.Select(x => x.Type).Append(functionObject.ReturnType).Select(GetTypeReference).ToArray()
            };

            // load Fn()::Call function
            Instructions.Add(new LoadTypeFunction(
                NextAddress(),
                functionReference,
                FunctionIndex: 0,
                TypeArguments: []));

            Instructions.Add(new Call(NextAddress()));
            if (!methodCallExpression.ValueUseful)
            {
                Instructions.Add(new Drop(NextAddress()));
            }
            return;
        }

        // we're directly calling a function

        // load any arguments that need to go first (eg 'this' or closure object)
        if (methodCallExpression.MethodCall.Method is MemberAccessExpression memberAccessExpression)
        {
            // load the method owner
            CompileExpression(memberAccessExpression.MemberAccess.Owner);
        }
        else if (instantiatedFunction.ClosureTypeId.HasValue)
        {
            CreateClosureObject(
                instantiatedFunction.ClosureTypeId.Value,
                instantiatedFunction.ClosureTypeFields.Select(x => x.fieldTypeId));
        }
        else if (instantiatedFunction is { IsStatic: false, OwnerSignature: { } ownerSignature }
                 && _scopeStack.Peek().CurrentFunction?.OwnerType == ownerSignature)
        {
            Instructions.Add(new LoadArgument(NextAddress(), 0));
        }

        // load all the arguments 
        foreach (var argument in methodCallExpression.MethodCall.ArgumentList)
        {
            CompileExpression(argument);
        }

        LoadFunctionPointer(instantiatedFunction);

        Instructions.Add(new Call(NextAddress()));
        if (!methodCallExpression.ValueUseful)
        {
            Instructions.Add(new Drop(NextAddress()));
        }
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
            if (staticMemberAccessExpression.StaticMemberAccess.InstantiatedFunction is not { } instantiatedFunction)
            {
                throw new InvalidOperationException("Expected instantiated function");
            }
            CreateFunctionObject(instantiatedFunction, instanceOwnerExpression: null);
        }
        else if (memberType == MemberType.Field)
        {
            Instructions.Add(new LoadStaticField(NextAddress(), ownerReference, VariantIndex: 0, itemIndex));
        }
        else if (memberType == MemberType.Variant)
        {
            CompileStaticMemberAccessVariantReference(
                staticMemberAccessExpression,
                itemIndex,
                ownerType as TypeChecker.InstantiatedUnion ?? throw new InvalidOperationException("Expected type to be union"),
                ownerReference);
        }
    }

    private void CompileStaticMemberAccessVariantReference(
        StaticMemberAccessExpression expression,
        uint variantIndex,
        TypeChecker.InstantiatedUnion unionType,
        ConcreteReefTypeReference unionReference)
    {
        var variant = unionType.Variants[(int)variantIndex];
        switch (variant)
        {
            case TypeChecker.ClassUnionVariant:
                throw new InvalidOperationException("Should not be able to reference class variant");
            case TypeChecker.TupleUnionVariant:
                {
                    if (expression.StaticMemberAccess.InstantiatedFunction is not { } fn)
                    {
                        throw new InvalidOperationException("Expected instantiated function");
                    }

                    CreateFunctionObject(fn, instanceOwnerExpression: null);
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

        if (!valueAccessorExpression.ValueUseful)
        {
            Instructions.Add(new Drop(NextAddress()));
        }
    }

    private void CreateClosureObject(Guid closureTypeId, IEnumerable<Guid> closureTypeFieldTypeIds)
    {
        var closureType = _types.First(x => x.Id == closureTypeId);
        Instructions.Add(new CreateObject(NextAddress(), new ConcreteReefTypeReference
        {
            Name = closureType.DisplayName,
            DefinitionId = closureType.Id,
            TypeArguments = []
        }));

        var currentFunction = _scopeStack.Peek().CurrentFunction
                              ?? throw new InvalidOperationException(
                                  "Expected to be in a function when referencing outer variables");

        foreach (var (fieldIndex, parameterTypeId) in closureTypeFieldTypeIds.Index())
        {
            Instructions.Add(new CopyStack(NextAddress()));
            if (parameterTypeId == currentFunction.LocalsTypeId)
            {
                // load the current functions locals type
                Instructions.Add(new LoadLocal(NextAddress(), 0));
            }
            else if (currentFunction.ClosureTypeId.HasValue)
            {
                // parameter must be locals for a function further up, so grab it out of our closureParameter
                Instructions.Add(new LoadArgument(NextAddress(), 0));
                foreach (var (i, (currentFunctionParameterTypeId, _)) in currentFunction.ClosureTypeFields.Index())
                {
                    if (currentFunctionParameterTypeId == parameterTypeId)
                    {
                        // load the expected closure parameter from the current function so it can be passed on
                        Instructions.Add(new LoadField(NextAddress(), 0, (uint)i));
                        break;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Expected to have closure parameter");
            }
            Instructions.Add(new StoreField(NextAddress(), 0, (uint)fieldIndex));
        }
    }

    private void LoadFunctionPointer(TypeChecker.InstantiatedFunction instantiatedFunction)
    {
        if (instantiatedFunction.OwnerType is not null)
        {
            Instructions.Add(new LoadTypeFunction(
                NextAddress(),
                GetTypeReference(instantiatedFunction.OwnerType) as ConcreteReefTypeReference ??
                throw new InvalidOperationException("Expected concrete type reference"),
                instantiatedFunction.FunctionIndex ??
                throw new InvalidOperationException("Expected function index"),
                instantiatedFunction.TypeArguments.Select(GetTypeReference).ToArray()));
        }
        else
        {
            Instructions.Add(new LoadGlobalFunction(NextAddress(), new FunctionDefinitionReference
            {
                Name = instantiatedFunction.Name,
                TypeArguments = instantiatedFunction.TypeArguments.Select(GetTypeReference).ToArray(),
                DefinitionId = instantiatedFunction.FunctionId
            }));
        }
    }

    private void CreateFunctionObject(TypeChecker.InstantiatedFunction instantiatedFunction, IExpression? instanceOwnerExpression)
    {
        var signature = TypeChecker.ClassSignature.Function(instantiatedFunction.Parameters.Count);

        var functionType = new ConcreteReefTypeReference
        {
            DefinitionId = signature.Id,
            Name = signature.Name,
            TypeArguments = instantiatedFunction.Parameters.Select(x => x.Type)
                .Append(instantiatedFunction.ReturnType)
                .Select(GetTypeReference)
                .ToArray()
        };

        Instructions.Add(new CreateObject(NextAddress(), functionType));
        Instructions.Add(new CopyStack(NextAddress()));

        LoadFunctionPointer(instantiatedFunction);

        // store the function pointer into field 0
        Instructions.Add(new StoreField(NextAddress(), 0, 0));

        var currentFunction = _scopeStack.Peek().CurrentFunction;

        if (instantiatedFunction.ClosureTypeId.HasValue)
        {
            Instructions.Add(new CopyStack(NextAddress()));

            CreateClosureObject(
                instantiatedFunction.ClosureTypeId.Value,
                instantiatedFunction.ClosureTypeFields.Select(x => x.fieldTypeId));

            // store the closure object into field 1
            Instructions.Add(new StoreField(NextAddress(), 0, 1));
        }
        else if (instanceOwnerExpression is not null && !instantiatedFunction.IsStatic)
        {
            Instructions.Add(new CopyStack(NextAddress()));

            CompileExpression(instanceOwnerExpression);

            // store `this` into field 1
            Instructions.Add(new StoreField(NextAddress(), 0, 1));
        }
        else if (instantiatedFunction is { IsStatic: false, OwnerSignature: { } ownerSignature } &&
                 currentFunction?.OwnerType == ownerSignature)
        {
            Instructions.Add(new CopyStack(NextAddress()));
            Instructions.Add(new LoadArgument(NextAddress(), 0));
            Instructions.Add(new StoreField(NextAddress(), 0, 1));
        }
    }

    private void CompileIdentifierValueAccessor(ValueAccessorExpression valueAccessorExpression)
    {
        if (valueAccessorExpression is { FunctionInstantiation: { } instantiatedFunction, ReferencedVariable: null })
        {
            CreateFunctionObject(instantiatedFunction, instanceOwnerExpression: null);
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
                TypeChecker.FunctionSignatureParameter functionParameter => functionParameter.ContainingFunction,
                TypeChecker.LocalVariable localVariable => localVariable.ContainingFunction
                                                           ?? _mainFunction
                                                           ?? throw new InvalidOperationException("Expected function"),
                _ => throw new ArgumentOutOfRangeException(nameof(referencedVariable))
            };

            var currentFunction = scope.CurrentFunction ?? throw new InvalidOperationException("Expected to be in a function");

            if (ownerFunction == currentFunction)
            {
                // load value from locals object
                Instructions.Add(new LoadLocal(NextAddress(), 0));
                var fieldIndex = currentFunction.LocalsTypeFields.Index().First(y => y.Item == referencedVariable).Index;
                Instructions.Add(new LoadField(NextAddress(), 0, (uint)fieldIndex));
            }
            else
            {
                var foundClosureTypeFieldIndex = 0u;
                var referencedLocalsFieldIndex = 0u;
                var found = false;
                foreach (var (closureTypeFieldIndex, (_, fields)) in currentFunction.ClosureTypeFields.Index())
                {
                    foreach (var (field, localsFieldIndex) in fields)
                    {
                        if (field == referencedVariable)
                        {
                            found = true;
                            foundClosureTypeFieldIndex = (uint)closureTypeFieldIndex;
                            referencedLocalsFieldIndex = localsFieldIndex;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    throw new InvalidOperationException("Could not find referenced variable in owning function");
                }

                Instructions.Add(new LoadArgument(NextAddress(), 0));
                Instructions.Add(new LoadField(NextAddress(), VariantIndex: 0, FieldIndex: foundClosureTypeFieldIndex));
                Instructions.Add(new LoadField(NextAddress(), VariantIndex: 0, FieldIndex: referencedLocalsFieldIndex));
            }
        }
        else
        {
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
                case TypeChecker.FunctionSignatureParameter(var fn, _, _, _, var parameterIndex):
                    {
                        var adjustedParameterIndex = parameterIndex
                                             // increment parameter index by one to allow for the `this` parameter
                                             + (fn.IsGlobal || fn.IsStatic ? 0 : 1)
                                             // increment parameter index by one to allow for the closure parameter
                                             + (fn.ClosureTypeId.HasValue ? 1 : 0);

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

        AssignToLocal(localVariable, variableDeclarationExpression.VariableDeclaration.Value);
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
