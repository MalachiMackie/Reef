using System.Diagnostics;
using Reef.Core.Expressions;
using Reef.Core.LoweredExpressions.New;
using MethodCall = Reef.Core.LoweredExpressions.New.MethodCall;

namespace Reef.Core.Abseil.New;

public partial class NewProgramAbseil
{
    private uint _controlFlowDepth;

    private interface IExpressionResult
    {
        IOperand ToOperand();
    }

    private sealed record OperandResult(IOperand Value) : IExpressionResult
    {
        public IOperand ToOperand() => Value;
    }

    private sealed record PlaceResult(IPlace Value) : IExpressionResult
    {
        public IOperand ToOperand() => new Copy(Value);
    }
    
    private IExpressionResult NewLowerExpression(IExpression expression, IPlace? destination)
    {
        return expression switch
        {
            BinaryOperatorExpression binaryOperatorExpression => LowerBinaryExpression(
                binaryOperatorExpression, destination),
            BlockExpression blockExpression => LowerBlock(blockExpression, destination),
            WhileExpression whileExpression => throw new NotImplementedException(),
            BreakExpression breakExpression => throw new NotImplementedException(),
            ContinueExpression continueExpression => throw new NotImplementedException(),
            IfExpressionExpression ifExpressionExpression => throw new NotImplementedException(),
            MatchesExpression matchesExpression => LowerMatches(matchesExpression, destination),
            MatchExpression matchExpression => LowerMatch(matchExpression, destination),
            MemberAccessExpression memberAccessExpression => LowerMemberAccess(memberAccessExpression,
                destination),
            MethodCallExpression methodCallExpression => LowerMethodCall(methodCallExpression, destination),
            MethodReturnExpression methodReturnExpression => LowerReturn(methodReturnExpression),
            ObjectInitializerExpression objectInitializerExpression => LowerObjectInitializer(
                objectInitializerExpression, destination),
            StaticMemberAccessExpression staticMemberAccessExpression => LowerStaticMemberAccess(
                staticMemberAccessExpression, destination),
            TupleExpression tupleExpression => LowerTuple(tupleExpression, destination),
            UnaryOperatorExpression unaryOperatorExpression => LowerUnaryOperator(unaryOperatorExpression,
                destination),
            UnionClassVariantInitializerExpression unionClassVariantInitializerExpression =>
                LowerUnionClassVariantInitializer(unionClassVariantInitializerExpression, destination),
            ValueAccessorExpression valueAccessorExpression => LowerValueAccessor(valueAccessorExpression,
                destination),
            VariableDeclarationExpression variableDeclarationExpression => LowerVariableDeclaration(
                variableDeclarationExpression),
            _ => throw new ArgumentOutOfRangeException(nameof(expression))
        };
    }
    
    private IExpressionResult LowerMatchPatterns(
            List<(IPattern Pattern, IExpression Expression)> patterns,
            IExpressionResult accessExpression,
            IExpression? otherwise,
            IPlace? destination,
            BasicBlockId afterBasicBlockId)
    {
        var classPatterns = new List<(ClassPattern Pattern, IExpression MatchArmExpression)>();
        var unionPatterns = new List<(IPattern Pattern, IExpression MatchArmExpression)>();

        foreach (var (pattern, armExpression) in patterns)
        {
            switch (pattern)
            {
                case DiscardPattern:
                    // otherwise = armExpression;
                    // if (patterns.Count == 1)
                    // {
                    //     return otherwise;
                    // }
                    //
                    // continue;

                    otherwise = armExpression;
                    if (patterns.Count == 1)
                    {
                        return NewLowerExpression(otherwise, destination);
                    }

                    continue;
                case VariableDeclarationPattern variableDeclarationPattern:
                    throw new NotImplementedException();
                // {
                //     otherwise = new BlockExpression(
                //         [
                //             new VariableDeclarationAndAssignmentExpression(
                //                 variableDeclarationPattern.VariableName.StringValue,
                //                 accessExpression,
                //                 false),
                //             armExpression
                //         ],
                //         armExpression.ResolvedType,
                //         true);
                //     if (patterns.Count == 1)
                //     {
                //         return otherwise;
                //     }
                //
                //     continue;
                // }
                // for now, type pattern is guaranteed to be the only arm that matches
                // when we eventually get interfaces, this needs to change
                // Debug.Assert(patterns.Count == 1);
                case TypePattern { VariableName.StringValue: var variableName }:
                    throw new NotImplementedException();
                    // return new BlockExpression(
                    //     [
                    //         new VariableDeclarationAndAssignmentExpression(
                    //             variableName,
                    //             accessExpression,
                    //             false),
                    //         armExpression
                    //     ],
                    //     armExpression.ResolvedType,
                    //     true);
                case TypePattern:
                    throw new NotImplementedException();
                    // return armExpression;
                case ClassPattern classPattern:
                    throw new NotImplementedException();
                    // classPatterns.Add((classPattern, armExpression));
                    // continue;
                case UnionVariantPattern or UnionTupleVariantPattern or UnionClassVariantPattern:
                    unionPatterns.Add((pattern, armExpression));
                    continue;
                default:
                    throw new UnreachableException($"{pattern.GetType()}");
            }
        }

        if (classPatterns.Count > 0)
        {
            throw new NotImplementedException();
            // // for now, class patterns are mutually exclusive with union patterns until we have
            // // interfaces
            // Debug.Assert(unionPatterns.Count == 0);
            //
            // LoweredConcreteTypeReference? typeReference = null;
            // foreach (var pattern in classPatterns.Select(x => x.Pattern))
            // {
            //     var patternTypeReference = (GetTypeReference(pattern.TypeReference.NotNull()) as LoweredConcreteTypeReference).NotNull();
            //     typeReference ??= patternTypeReference;
            //
            //     Debug.Assert(EqualTypeReferences(typeReference, patternTypeReference));
            // }
            //
            // // check all class patterns are for the same type. This needs to change when we have interfaces
            //
            // var dataType = GetDataType(typeReference!.DefinitionId, typeReference.TypeArguments);
            //
            // var dataTypeFields = dataType.Variants[0].Fields;
            // if (dataTypeFields.Count == 0)
            // {
            //     throw new NotImplementedException();
            // }
            //
            // IReadOnlyCollection<IPatternMatchingNode>? previousNodes = null;
            // var originalNode = new FieldNode(
            //     dataType.Variants[0].Fields[0].Name,
            //     [],
            //     [..classPatterns.Select(x => ((IPattern)x.Pattern, x.MatchArmExpression))])
            // {
            //     Otherwise = otherwise
            // };
            // IReadOnlyCollection<IPatternMatchingNode>? nodes = [originalNode];
            //
            // foreach (var field in dataType.Variants[0].Fields)
            // {
            //     if (nodes is null)
            //     {
            //         Debug.Assert(previousNodes is not null);
            //         foreach (var previousNode in previousNodes)
            //         {
            //             foreach (var b in previousNode.UniquePatterns)
            //             {
            //                 b.NextField = new FieldNode(field.Name, [], b.OriginalPatterns){Otherwise = otherwise};
            //             }
            //         }
            //         nodes = [..previousNodes.SelectMany(x => x.UniquePatterns.Select(y => y.NextField.NotNull()))];
            //     }
            //
            //     foreach (var (_, uniquePatterns, originalPatterns) in nodes.OfType<FieldNode>())
            //     {
            //         foreach (var (pattern, armExpression) in originalPatterns)
            //         {
            //             var classPattern = (pattern as ClassPattern).NotNull();
            //             var fieldPattern = classPattern.FieldPatterns.FirstOrDefault(x => x.FieldName.StringValue == field.Name);
            //             if (fieldPattern is null)
            //             {
            //                 throw new NotImplementedException();
            //             }
            //
            //             var foundUniquePattern = uniquePatterns.FirstOrDefault(x => PatternsEquivalent(x.Pattern, fieldPattern.Pattern.NotNull()));
            //
            //             if (foundUniquePattern is null)
            //             {
            //                 foundUniquePattern = new(fieldPattern.Pattern.NotNull(), []);
            //                 uniquePatterns.Add(foundUniquePattern);
            //             }
            //             foundUniquePattern.OriginalPatterns.Add((classPattern, armExpression));
            //         }
            //     }
            //     previousNodes = nodes;
            //     nodes = null;
            //     
            // }
            //
            // return ProcessNode(originalNode);
            //
            // ILoweredExpression ProcessNode(FieldNode node)
            // {
            //     var nextPatterns = new List<(IPattern, ILoweredExpression)>();
            //     foreach (var uniquePattern in node.UniquePatterns)
            //     {
            //         ILoweredExpression exp;
            //         if (uniquePattern.NextField is null)
            //         {
            //             Debug.Assert(uniquePattern.OriginalPatterns.Count == 1);
            //             var (originalPattern, originalExpression) = uniquePattern.OriginalPatterns[0];
            //             var variableName = (originalPattern as ClassPattern).NotNull().VariableName?.StringValue;
            //             if (variableName is not null)
            //             {
            //                 originalExpression = new BlockExpression(
            //                     [
            //                         new VariableDeclarationAndAssignmentExpression(variableName, accessExpression, false),
            //                         originalExpression
            //                     ],
            //                     originalExpression.ResolvedType,
            //                     true);
            //             }
            //
            //             exp = originalExpression;
            //         }
            //         else if (uniquePattern.NextField is FieldNode nextField)
            //         {
            //             exp = ProcessNode(nextField);
            //         }
            //         else
            //         {
            //             throw new InvalidOperationException($"Expected structural node. Got {uniquePattern.NextField.GetType()}");
            //         }
            //         nextPatterns.Add((uniquePattern.Pattern, exp));
            //     }
            //     
            //     var fieldType = dataType.Variants[0].Fields.First(x => x.Name == node.FieldName).Type;
            //
            //     var locals = _currentFunction.NotNull().LoweredMethod.Locals;
            //     var localName = $"Local{locals.Count}";
            //     locals.Add(new MethodLocal(localName, fieldType));
            //
            //     var localDeclaration = new VariableDeclarationAndAssignmentExpression(
            //         localName,
            //         new FieldAccessExpression(
            //             accessExpression,
            //             node.FieldName,
            //             "_classVariant",
            //             true,
            //             fieldType),
            //         false);
            //     var localAccess = new LocalVariableAccessor(localName, true, fieldType);
            //     
            //     var innerExpression = LowerMatchPatterns(
            //         nextPatterns,
            //         localAccess,
            //         node.Otherwise);
            //     
            //     return new BlockExpression(
            //         [
            //             localDeclaration,
            //             innerExpression
            //         ],
            //         innerExpression.ResolvedType,
            //         true);
            // }
        }
        
        if (unionPatterns.Count > 0)
        {
            // for now, class patterns are mutually exclusive with union patterns until we have
            // interfaces
            Debug.Assert(classPatterns.Count == 0);
            
            NewLoweredConcreteTypeReference? typeReference = null;
            foreach (var pattern in unionPatterns.Select(x => x.Pattern))
            {
                var patternTypeReference = (GetTypeReference(pattern.TypeReference.NotNull()) as NewLoweredConcreteTypeReference).NotNull();
                typeReference ??= patternTypeReference;
            
                Debug.Assert(EqualTypeReferences(typeReference, patternTypeReference));
            }
            
            // check all class patterns are for the same type. This needs to change when we have interfaces
            
            var dataType = GetDataType(typeReference!.DefinitionId);
            
            var originalNode = new TopLevelNode([], unionPatterns){Otherwise = otherwise};
            
            IReadOnlyList<IPatternMatchingNode> nodes = [originalNode];
            
            foreach (var variant in dataType.Variants)
            {
                foreach (var node in nodes)
                {
                    foreach (var (originalPattern, armExpression) in node.OriginalPatterns)
                    {
                        var (patternVariantName, variableName) = originalPattern switch
                        {
                            UnionVariantPattern variantPattern => (variantPattern.VariantName.NotNull().StringValue, variantPattern.VariableName),
                            UnionClassVariantPattern classVariantPattern => (classVariantPattern.VariantName.StringValue, classVariantPattern.VariableName),
                            UnionTupleVariantPattern tupleVariantPattern => (tupleVariantPattern.VariantName.StringValue, tupleVariantPattern.VariableName),
                            _ => throw new UnreachableException()
                        };
            
                        if (patternVariantName == variant.Name)
                        {
                            var tupleVariantPattern = new UnionVariantPattern(
                                null!,
                                Token.Identifier(patternVariantName, SourceSpan.Default),
                                variableName,
                                false,
                                SourceRange.Default)
                            {
                                TypeReference = originalPattern.TypeReference.NotNull()
                            };
                            
                            if (node.UniquePatterns.FirstOrDefault(x => PatternsEquivalent(x.Pattern, tupleVariantPattern)) is { } uniquePattern)
                            {
                                uniquePattern.OriginalPatterns.Add((originalPattern, armExpression));
                            }
                            else
                            {
                                node.UniquePatterns.Add(new B(
                                    tupleVariantPattern,
                                    [(originalPattern, armExpression)]));
                            }
                        }
                    }
                }
                
                var variantNodes = nodes;
                var previousNodes = variantNodes;
            
                foreach (var field in variant.Fields.Where(x => x.Name != VariantIdentifierFieldName))
                {
                    var newVariantNodes = new List<IPatternMatchingNode>();
                    foreach (var previousNode in previousNodes)
                    {
                        foreach (var b in previousNode.UniquePatterns)
                        {
                            b.NextField = new VariantFieldNode(variant.Name, field.Name, [],
                                b.OriginalPatterns){Otherwise = otherwise};
                            newVariantNodes.Add(b.NextField);
                        }
                    }
            
                    variantNodes = newVariantNodes;
            
                    foreach (var node in variantNodes)
                    {
                        var originalPatterns = node.OriginalPatterns;
                        var uniquePatterns = node.UniquePatterns;
                        foreach (var (originalPattern, armExpression) in originalPatterns)
                        {
                            B? foundUniquePattern = null;
                            IPattern? fieldPattern = null;
                            switch (originalPattern)
                            {
                                case UnionClassVariantPattern classVariantPattern:
                                    if (variant.Name != classVariantPattern.VariantName.StringValue)
                                    {
                                        continue;
                                    }
            
                                    fieldPattern =
                                        classVariantPattern.FieldPatterns.First(x =>
                                            x.FieldName.StringValue == field.Name).Pattern.NotNull();
                                    foundUniquePattern = uniquePatterns.FirstOrDefault(x =>
                                        PatternsEquivalent(x.Pattern, fieldPattern));
                                    break;
                                case UnionTupleVariantPattern tupleVariantPattern:
                                    if (variant.Name != tupleVariantPattern.VariantName.StringValue)
                                    {
                                        continue;
                                    }
            
                                    fieldPattern = tupleVariantPattern.TupleParamPatterns
                                        .Select((x, i) => ($"Item{i}", Pattern: x))
                                        .First(x => x.Item1 == field.Name).Pattern;
                                    foundUniquePattern = uniquePatterns.FirstOrDefault(x =>
                                        PatternsEquivalent(x.Pattern, fieldPattern));
                                    break;
                                case UnionVariantPattern variantPattern:
                                {
                                    if (variant.Name != variantPattern.VariantName.NotNull().StringValue)
                                    {
                                        continue;
                                    }
            
                                    node.Otherwise = armExpression;
                                    continue;
                                }
                            }
            
                            if (foundUniquePattern is null)
                            {
                                foundUniquePattern = new B(fieldPattern.NotNull(), []);
                                uniquePatterns.Add(foundUniquePattern);
                            }
            
                            foundUniquePattern.OriginalPatterns.Add((originalPattern, armExpression));
                        }
                    }
            
                    previousNodes = variantNodes;
                }
            }
            
            ProcessNode(originalNode);

            return destination is not null ? new PlaceResult(destination) : throw new NotImplementedException();
            
            void ProcessNode(IPatternMatchingNode node)
            {
                var nextPatterns = new List<(IPattern, IExpression?, TypeChecking.TypeChecker.LocalVariable?, IPatternMatchingNode?)>();
                foreach (var uniquePattern in node.UniquePatterns)
                {
                    IExpression? expression = null;
                    TypeChecking.TypeChecker.LocalVariable? localVariable = null;
                    IPatternMatchingNode? nextField = null;
                    if (uniquePattern.NextField is {UniquePatterns.Count: > 0})
                    {
                        nextField = uniquePattern.NextField;
                    }
                    else
                    {
                        Debug.Assert(uniquePattern.OriginalPatterns.Count == 1);
                        (var originalPattern, expression) = uniquePattern.OriginalPatterns[0];
                        localVariable = originalPattern switch
                        {
                            UnionVariantPattern variantPattern => variantPattern.Variable,
                            UnionClassVariantPattern classVariantPattern => classVariantPattern.Variable,
                            UnionTupleVariantPattern tupleVariantPattern => tupleVariantPattern.Variable,
                            _ => null
                        };
                    }
            
                    nextPatterns.Add((uniquePattern.Pattern, expression, localVariable, nextField));
                }
            
                switch (node)
                {
                    case TopLevelNode:
                    {
                        var innerResults = new Dictionary<int, BasicBlockId>();
                        var initialBasicBlock = _basicBlocks[^1];
                        
                        IPlace valuePlace;
                        if (accessExpression is PlaceResult { Value: var place })
                        {
                            valuePlace = place;
                        }
                        else
                        {
                            var localName = LocalName((uint)_locals.Count);
                            _locals.Add(new NewMethodLocal(localName, null, typeReference));
                            valuePlace = new Local(localName);
                            _basicBlockStatements.Add(new Assign(
                                valuePlace,
                                new Use(accessExpression.ToOperand())));
                        }

                        foreach (var (pattern, expression, localVariable, nextField) in nextPatterns)
                        {
                            var patternVariantName = pattern switch
                            {
                                UnionVariantPattern variantPattern => variantPattern.VariantName.NotNull().StringValue,
                                UnionClassVariantPattern classVariantPattern => classVariantPattern.VariantName.StringValue,
                                UnionTupleVariantPattern tupleVariantPattern => tupleVariantPattern.VariantName.StringValue,
                                _ => throw new UnreachableException()
                            };
                            
                            var innerTypeReference = (GetTypeReference(pattern.TypeReference.NotNull()) as NewLoweredConcreteTypeReference)
                                .NotNull();
                            var innerDataType = GetDataType(innerTypeReference.DefinitionId);
                            
                            var variantIndex = innerDataType.Variants.Index().First(x => x.Item.Name == patternVariantName).Index;

                            var nextBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                            _basicBlockStatements = [];
                            _basicBlocks.Add(new BasicBlock(nextBasicBlockId, _basicBlockStatements));

                            if (localVariable is not null)
                            {
                                _basicBlockStatements.Add(new Assign(
                                    GetLocalVariablePlace(localVariable),
                                    new Use(accessExpression.ToOperand())));
                            }

                            if (expression is not null)
                            {
                                // todo: do I need to save this result?
                                NewLowerExpression(expression.NotNull(), destination);
                            }
                            else
                            {
                                ProcessNode(nextField.NotNull());
                            }

                            _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);
                            
                            innerResults[variantIndex] = nextBasicBlockId;
                        }

                        BasicBlockId otherwiseId;
                        if (node.Otherwise is not null)
                        {
                            otherwiseId = new BasicBlockId($"bb{_basicBlocks.Count}");
                            _basicBlockStatements = [];
                            _basicBlocks.Add(new BasicBlock(otherwiseId, _basicBlockStatements));
                            NewLowerExpression(node.Otherwise, destination);
                            
                            _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);
                        }
                        else
                        {
                            otherwiseId = afterBasicBlockId;
                        }
                        
                        var variantName = dataType.Variants[0].Name;
                        
                        initialBasicBlock.Terminator = new SwitchInt(
                            new Copy(new Field(valuePlace, VariantIdentifierFieldName, variantName)),
                            innerResults,
                            otherwiseId);
                        
                        break;
                    }
                    case VariantFieldNode fieldNode:
                    {
                        var variant = dataType.Variants.First(x => x.Name == fieldNode.VariantName);
                        
                        IPlace valuePlace;
                        if (accessExpression is PlaceResult { Value: var place })
                        {
                            valuePlace = place;
                        }
                        else
                        {
                            var localName = LocalName((uint)_locals.Count);
                            _locals.Add(new NewMethodLocal(localName, null, typeReference));
                            valuePlace = new Local(localName);
                            _basicBlockStatements.Add(new Assign(
                                valuePlace,
                                new Use(accessExpression.ToOperand())));
                        }
                        
                        LowerMatchPatterns(
                            [..nextPatterns.Where(x => x.Item2 is not null).Select(x => (x.Item1, x.Item2.NotNull()))],
                            new PlaceResult(new Field(valuePlace, fieldNode.FieldName, variant.Name)),
                            node.Otherwise,
                            destination,
                            afterBasicBlockId);

                        break;
                    }
                    default:
                        throw new UnreachableException();
                }
            }
        }

        throw new UnreachableException();
    }

    private interface IPatternMatchingNode
    {
        public List<B> UniquePatterns { get; }
        public List<(IPattern, IExpression)> OriginalPatterns { get; }
        public IExpression? Otherwise { get; set; }
    }

    private record TopLevelNode(
        List<B> UniquePatterns,
        List<(IPattern, IExpression)> OriginalPatterns) : IPatternMatchingNode
    {
        public IExpression? Otherwise { get; set; }
    }

    private record VariantFieldNode(
        string VariantName,
        string FieldName,
        List<B> UniquePatterns,
        List<(IPattern, IExpression)> OriginalPatterns) : IPatternMatchingNode
    {
        public IExpression? Otherwise { get; set; }
    }

    private record FieldNode(
        string FieldName,
        List<B> UniquePatterns,
        List<(IPattern, IExpression)> OriginalPatterns) : IPatternMatchingNode
    {
        public IExpression? Otherwise { get; set; }
    }

    // todo: better name
    private record B(IPattern Pattern, List<(IPattern, IExpression)> OriginalPatterns)
    {
        public IPatternMatchingNode? NextField { get; set; }
    };

    private bool PatternsEquivalent(IPattern a, IPattern b)
    {
        switch (a, b)
        {
            case (DiscardPattern, VariableDeclarationPattern):
            case (DiscardPattern, DiscardPattern):
            case (VariableDeclarationPattern, VariableDeclarationPattern):
            {
                return true;
            }
            case (UnionVariantPattern left, UnionVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());
                return EqualTypeReferences(leftType, rightType)
                    && left.VariantName.NotNull().StringValue == right.VariantName.NotNull().StringValue;
            }
            case (UnionClassVariantPattern left, UnionVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());
                return EqualTypeReferences(leftType, rightType)
                       && left.VariantName.NotNull().StringValue == right.VariantName.NotNull().StringValue
                       && left.FieldPatterns.Count == 0;
            }
            case (UnionVariantPattern left, UnionClassVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());
                return EqualTypeReferences(leftType, rightType)
                       && left.VariantName.NotNull().StringValue == right.VariantName.NotNull().StringValue
                       && right.FieldPatterns.Count == 0;
            }
            case (UnionClassVariantPattern left, UnionClassVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());
                var rightFieldsByName = right.FieldPatterns.ToDictionary(x => x.FieldName.StringValue);

                return EqualTypeReferences(leftType, rightType)
                       && left.FieldPatterns.Count == right.FieldPatterns.Count
                       && left.VariantName.StringValue == right.VariantName.StringValue
                       && left.FieldPatterns.All(x => rightFieldsByName.TryGetValue(x.FieldName.StringValue, out var rightFieldPattern)
                                                      && PatternsEquivalent(x.Pattern.NotNull(),
                                                          rightFieldPattern.Pattern.NotNull()));
            }
            case (UnionVariantPattern left, UnionTupleVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());

                return EqualTypeReferences(leftType, rightType)
                       && right.TupleParamPatterns.Count == 0;
            }
            case (UnionTupleVariantPattern left, UnionVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());

                return EqualTypeReferences(leftType, rightType)
                       && left.TupleParamPatterns.Count == 0;
            }
            case (UnionTupleVariantPattern left, UnionTupleVariantPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());

                return EqualTypeReferences(leftType, rightType)
                       && left.TupleParamPatterns.Zip(right.TupleParamPatterns)
                           .All(x => PatternsEquivalent(x.First, x.Second));
            }
            case (TypePattern left, TypePattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());

                return EqualTypeReferences(leftType, rightType);
            }
            case (TypePattern left, ClassPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());

                return EqualTypeReferences(leftType, rightType)
                       && right.FieldPatterns.Count == 0;
            }
            case (ClassPattern left, TypePattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());

                return EqualTypeReferences(leftType, rightType)
                       && left.FieldPatterns.Count == 0;
            }
            case (ClassPattern left, ClassPattern right):
            {
                var leftType = GetTypeReference(left.TypeReference.NotNull());
                var rightType = GetTypeReference(right.TypeReference.NotNull());
                var rightFieldsByName = right.FieldPatterns.ToDictionary(x => x.FieldName.StringValue);

                return EqualTypeReferences(leftType, rightType)
                       && left.FieldPatterns.Count == right.FieldPatterns.Count
                       && left.FieldPatterns.All(x => rightFieldsByName.TryGetValue(x.FieldName.StringValue, out var rightFieldPattern)
                                                      && PatternsEquivalent(x.Pattern.NotNull(),
                                                          rightFieldPattern.Pattern.NotNull()));
            }
            default:
                return false;
        }
    }

    private IExpressionResult LowerMatch(MatchExpression e, IPlace? destination)
    {
        var accessResult = NewLowerExpression(e.Value, destination: null);

        var afterBasicBlockId = new BasicBlockId("after");
        
        var result = LowerMatchPatterns(
            [..e.Arms.Select(x => (x.Pattern, x.Expression.NotNull()))],
            accessResult,
            null,
            destination,
            afterBasicBlockId);

        _basicBlockStatements = [];
        afterBasicBlockId.Id = $"bb{_basicBlocks.Count}";
        _basicBlocks.Add(new BasicBlock(afterBasicBlockId, _basicBlockStatements));

        return result;
    }
    
    private IExpressionResult LowerMatchesPattern(
            IExpressionResult value,
            IPattern pattern,
            IPlace? destination)
    {
        var boolType = GetTypeReference(TypeChecking.TypeChecker.InstantiatedClass.Boolean);
        switch (pattern)
        {
            case DiscardPattern:
            {
                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(destination, new Use(new BoolConstant(true))));
                }

                return new OperandResult(new BoolConstant(true));
            }
            case VariableDeclarationPattern { Variable: var variable }:
            {
                var localPlace = GetLocalVariablePlace(variable.NotNull());
                _basicBlockStatements.Add(new Assign(localPlace, new Use(value.ToOperand())));
                
                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(destination, new Use(new BoolConstant(true))));
                }

                return new OperandResult(new BoolConstant(true));
            }
            case TypePattern { Variable: var variable, TypeReference: var typeReference }:
            {
                var loweredTypeReference = GetTypeReference(typeReference.NotNull());
                if (loweredTypeReference is not NewLoweredConcreteTypeReference)
                {
                    throw new NotImplementedException(
                        "Only concrete type patterns are currently supported. This needs to be implemented when interfaces are added");
                }

                if (variable is not null)
                {
                    _basicBlockStatements.Add(new Assign(GetLocalVariablePlace(variable), new Use(value.ToOperand())));
                }

                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(destination, new Use(new BoolConstant(true))));
                }

                return new OperandResult(new BoolConstant(true));
            }
            case UnionVariantPattern
            {
                Variable: var variable,
                TypeReference: var unionType,
                VariantName: var variantName
            }:
            {
                IPlace? valuePlace = null;
                if (variable is not null)
                {
                    valuePlace = GetLocalVariablePlace(variable);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                }
                
                var type = (GetTypeReference(unionType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, boolType));
                    destination = new Local(localName);
                }

                if (value is PlaceResult { Value: var place })
                {
                    // always prefer the original value place
                    valuePlace = place;
                }
                else if (valuePlace is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }
                
                _basicBlockStatements.Add(
                    new Assign(
                        destination,
                        new BinaryOperation(
                            new Copy(new Field(valuePlace, VariantIdentifierFieldName, variant.Name)),
                            new UIntConstant((uint)variantIndex, 2),
                            BinaryOperationKind.Equal)));

                return new PlaceResult(destination);
            }
            case UnionTupleVariantPattern
            {
                VariantName: var variantName,
                Variable: var variable,
                TupleParamPatterns: var tupleParamPatterns,
                TypeReference: var unionType,
            }:
            {
                IPlace? valuePlace = null;
                if (variable is not null)
                {
                    valuePlace = GetLocalVariablePlace(variable);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                }
                
                var type = (GetTypeReference(unionType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, boolType));
                    destination = new Local(localName);
                }

                if (value is PlaceResult { Value: var place })
                {
                    // always prefer the original value place
                    valuePlace = place;
                }
                else if (valuePlace is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }
                
                _basicBlockStatements.Add(
                    new Assign(
                        destination,
                        new BinaryOperation(
                            new Copy(new Field(valuePlace, VariantIdentifierFieldName, variant.Name)),
                            new UIntConstant((uint)variantIndex, 2),
                            BinaryOperationKind.Equal)));

                Debug.Assert(tupleParamPatterns.Count > 0);
                var initialBasicBlock = _basicBlocks[^1];


                _basicBlockStatements = [];
                var nextBasicBlock = new BasicBlock(
                    new BasicBlockId($"bb{_basicBlocks.Count}"),
                    _basicBlockStatements);
                _basicBlocks.Add(nextBasicBlock);

                var afterBasicBlockId = new BasicBlockId("after");

                initialBasicBlock.Terminator = new SwitchInt(
                    new Copy(destination),
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, afterBasicBlockId }
                    },
                    nextBasicBlock.Id);
                
                foreach (var (i, tupleParamPattern) in tupleParamPatterns.Index())
                {
                    LowerMatchesPattern(
                        new PlaceResult(new Field(valuePlace, $"Item{i}", variant.Name)),
                        tupleParamPattern,
                        destination);

                    _basicBlockStatements = [];
                    var nextBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                    if (i + 1 < tupleParamPatterns.Count)
                    {
                        nextBasicBlock.Terminator = new SwitchInt(
                            new Copy(destination),
                            new Dictionary<int, BasicBlockId>
                            {
                                { 0, afterBasicBlockId },
                            },
                            nextBasicBlockId);
                    }
                    else
                    {
                        nextBasicBlock.Terminator = new GoTo(nextBasicBlockId);
                    }

                    _basicBlocks.Add(nextBasicBlock = new BasicBlock(nextBasicBlockId, _basicBlockStatements));
                }

                afterBasicBlockId.Id = nextBasicBlock.Id.Id;

                return new PlaceResult(destination);
            }
            case UnionClassVariantPattern
            {
                VariantName: var variantName,
                Variable: var variable,
                FieldPatterns: var fieldPatterns,
                TypeReference: var unionType,
            }:
            {
                IPlace? valuePlace = null;
                if (variable is not null)
                {
                    valuePlace = GetLocalVariablePlace(variable);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                }
                
                var type = (GetTypeReference(unionType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, boolType));
                    destination = new Local(localName);
                }

                if (value is PlaceResult { Value: var place })
                {
                    // always prefer the original value place
                    valuePlace = place;
                }
                else if (valuePlace is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }
                
                _basicBlockStatements.Add(
                    new Assign(
                        destination,
                        new BinaryOperation(
                            new Copy(new Field(valuePlace, VariantIdentifierFieldName, variant.Name)),
                            new UIntConstant((uint)variantIndex, 2),
                            BinaryOperationKind.Equal)));

                if (fieldPatterns.Count == 0)
                {
                    return new PlaceResult(destination);
                }

                var initialBasicBlock = _basicBlocks[^1];

                _basicBlockStatements = [];
                var nextBasicBlock = new BasicBlock(
                    new BasicBlockId($"bb{_basicBlocks.Count}"),
                    _basicBlockStatements);
                _basicBlocks.Add(nextBasicBlock);

                var afterBasicBlockId = new BasicBlockId("after");

                initialBasicBlock.Terminator = new SwitchInt(
                    new Copy(destination),
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, afterBasicBlockId }
                    },
                    nextBasicBlock.Id);
                
                foreach (var (i, fieldPattern) in fieldPatterns.Index())
                {
                    LowerMatchesPattern(
                        new PlaceResult(new Field(valuePlace, fieldPattern.FieldName.ToString(), variant.Name)),
                        fieldPattern.Pattern ?? 
                        new VariableDeclarationPattern(fieldPattern.FieldName, SourceRange.Default, IsMut: false){Variable = fieldPattern.Variable.NotNull()},
                        destination);

                    _basicBlockStatements = [];
                    var nextBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                    if (i + 1 < fieldPatterns.Count)
                    {
                        nextBasicBlock.Terminator = new SwitchInt(
                            new Copy(destination),
                            new Dictionary<int, BasicBlockId>
                            {
                                { 0, afterBasicBlockId },
                            },
                            nextBasicBlockId);
                    }
                    else
                    {
                        nextBasicBlock.Terminator = new GoTo(nextBasicBlockId);
                    }

                    _basicBlocks.Add(nextBasicBlock = new BasicBlock(nextBasicBlockId, _basicBlockStatements));
                }

                afterBasicBlockId.Id = nextBasicBlock.Id.Id;

                return new PlaceResult(destination);
            }
        case ClassPattern
            {
                Variable: var variable,
                FieldPatterns: var fieldPatterns,
                TypeReference: var unionType,
            }:
            {
                IPlace? valuePlace = null;
                if (variable is not null)
                {
                    valuePlace = GetLocalVariablePlace(variable);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                }
                
                var type = (GetTypeReference(unionType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, boolType));
                    destination = new Local(localName);
                }

                if (value is PlaceResult { Value: var place })
                {
                    // always prefer the original value place
                    valuePlace = place;
                }
                else if (valuePlace is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }

                if (fieldPatterns.Count == 0)
                {
                    _basicBlockStatements.Add(
                        new Assign(
                            destination,
                            new Use(new BoolConstant(true))));

                    return new PlaceResult(destination);
                }
                
                var afterBasicBlockId = new BasicBlockId("after");

                foreach (var (i, fieldPattern) in fieldPatterns.Index())
                {
                    LowerMatchesPattern(
                        new PlaceResult(new Field(valuePlace, fieldPattern.FieldName.ToString(), ClassVariantName)),
                        fieldPattern.Pattern ?? 
                        new VariableDeclarationPattern(fieldPattern.FieldName, SourceRange.Default, IsMut: false){Variable = fieldPattern.Variable.NotNull()},
                        destination);

                    _basicBlockStatements = [];
                    var basicBlock = _basicBlocks[^1];
                    var nextBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                    if (i + 1 < fieldPatterns.Count)
                    {
                        basicBlock.Terminator = new SwitchInt(
                            new Copy(destination),
                            new Dictionary<int, BasicBlockId>
                            {
                                { 0, afterBasicBlockId },
                            },
                            nextBasicBlockId);
                    }
                    else
                    {
                        basicBlock.Terminator = new GoTo(nextBasicBlockId);
                    }

                    _basicBlocks.Add(new BasicBlock(nextBasicBlockId, _basicBlockStatements));
                    afterBasicBlockId.Id = nextBasicBlockId.Id;
                }

                return new PlaceResult(destination);
            }
        }
        throw new NotImplementedException(); 
    }

    private IExpressionResult LowerMatches(
        MatchesExpression e,
        IPlace? destination)
    {
        var valueResult = NewLowerExpression(e.ValueExpression, destination: null);

        return LowerMatchesPattern(valueResult, e.Pattern.NotNull(), destination);
    }
    
    private IExpressionResult LowerUnionClassVariantInitializer(
        UnionClassVariantInitializerExpression e,
        IPlace? destination)
    {
        var typeReference = (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();
        
        var dataType = _types[typeReference.DefinitionId];
        
        var variantIdentifier = dataType.Variants.Index()
            .First(x => x.Item.Name == e.UnionInitializer.VariantIdentifier.StringValue).Index;

        return CreateObject(
            typeReference,
            e.UnionInitializer.VariantIdentifier.StringValue,
            e.UnionInitializer.FieldInitializers.Select(x =>
                new CreateObjectField(x.FieldName.StringValue, x.Value))
                .Prepend(new CreateObjectField(VariantIdentifierFieldName, new UIntConstant((ulong)variantIdentifier, 2))),
            destination);
    }
    
    private IExpressionResult LowerStaticMemberAccess(
            StaticMemberAccessExpression e,
            IPlace? destination)
    {
        switch (e.StaticMemberAccess.MemberType)
        {
            case MemberType.Variant:
            {
                var unionType = GetTypeReference(e.OwnerType.NotNull())
                    as NewLoweredConcreteTypeReference ?? throw new UnreachableException();

                var dataType = _types[unionType.DefinitionId];
                var variantName = e.StaticMemberAccess.MemberName.NotNull().StringValue;
                var (variantIdentifier, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName);

                if (variant.Fields is [{ Name: VariantIdentifierFieldName }])
                {
                    return CreateObject(
                        unionType,
                        variantName: e.StaticMemberAccess.MemberName.NotNull().StringValue,
                        [
                            new CreateObjectField(VariantIdentifierFieldName,
                                new UIntConstant((ulong)variantIdentifier, 2))
                        ],
                        destination);
                }
                
                // we're statically accessing this variant, and there's at least one field. It must be a tuple variant
                // because you can't access a class variant directly outside creating it. We're returning a 
                // function object for this tuple create function

                if (e.ResolvedType is not TypeChecking.TypeChecker.FunctionObject)
                {
                    throw new InvalidOperationException($"Expected a function object, got a {e.ResolvedType?.GetType()}");
                }
                
                var ownerTypeArguments = unionType.TypeArguments;

                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();

                var functionObjectType =
                    (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

                return CreateObject(
                    functionObjectType,
                    ClassVariantName,
                    [
                        new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                            GetFunctionReference(
                                fn.FunctionId,
                                [..fn.TypeArguments.Select(GetTypeReference)],
                                ownerTypeArguments))),
                    ],
                    destination);

            }
            case MemberType.Function:
            {
                var ownerTypeArguments = (GetTypeReference(e.OwnerType.NotNull()) as NewLoweredConcreteTypeReference).NotNull().TypeArguments;
                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();

                return CreateObject(
                    (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull(),
                    ClassVariantName,
                    [new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                        GetFunctionReference(fn.FunctionId,
                            [..fn.TypeArguments.Select(GetTypeReference)],
                            ownerTypeArguments)))],
                    destination);
            }
            case MemberType.Field:
            {
                var staticField = new StaticField(
                    (GetTypeReference(e.OwnerType.NotNull()) as NewLoweredConcreteTypeReference).NotNull(),
                    e.StaticMemberAccess.MemberName.NotNull().StringValue);

                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(
                        destination,
                        new Use(new Copy(staticField))));
                }
                
                return new PlaceResult(destination ?? staticField);
            }
            default:
                throw new UnreachableException();
        }
    }
    
    private IExpressionResult LowerMemberAccess(
            MemberAccessExpression e,
            IPlace? destination)
    {
        var ownerResult = NewLowerExpression(e.MemberAccess.Owner, null);
        
        switch (e.MemberAccess.MemberType.NotNull())
        {
            case MemberType.Field:
            {
                IPlace ownerPlace;

                switch (ownerResult)
                {
                    case OperandResult{Value: var operand}:
                    {
                        var localName = LocalName((uint)_locals.Count);
                        _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(e.MemberAccess.OwnerType.NotNull())));
            
                        ownerPlace = new Local(localName);
            
                        _basicBlockStatements.Add(new Assign(
                            ownerPlace,
                            new Use(operand)));
                        break;
                    }
                    case PlaceResult{Value: var place}:
                        ownerPlace = place;
                        break;
                    default:
                        throw new UnreachableException();
                }
                
                var field = new Field(ownerPlace, e.MemberAccess.MemberName.NotNull().StringValue, ClassVariantName);

                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(
                        destination,
                        new Use(new Copy(field))));
                }
                
                return new PlaceResult(destination ?? field);
            }
            case MemberType.Function:
                {
                    var ownerTypeArguments = (GetTypeReference(e.MemberAccess.OwnerType.NotNull()) as NewLoweredConcreteTypeReference)
                        .NotNull().TypeArguments;

                    var fn = e.MemberAccess.InstantiatedFunction.NotNull();

                    var functionObjectType =
                        (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

                    return CreateObject(
                        functionObjectType,
                        ClassVariantName,
                        [
                            new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                                    GetFunctionReference(
                                        fn.FunctionId,
                                        [..fn.TypeArguments.Select(GetTypeReference)],
                                        ownerTypeArguments))),
                            new CreateObjectField("FunctionParameter", ownerResult.ToOperand())
                        ],
                        destination);
                }
            case MemberType.Variant:
                throw new InvalidOperationException("Can never access a variant through instance member access");
            default:
                throw new UnreachableException($"{e.MemberAccess.MemberType}");
        }
    }

    private IExpressionResult LowerObjectInitializer(ObjectInitializerExpression objectInitializerExpression, IPlace? destination)
    {
        var typeReference = (GetTypeReference(objectInitializerExpression.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

        return CreateObject(
            typeReference,
            ClassVariantName,
            objectInitializerExpression.ObjectInitializer.FieldInitializers.Select(x =>
                new CreateObjectField(x.FieldName.StringValue, x.Value)),
            destination);
    }

    private sealed class CreateObjectField
    {
        public string FieldName { get; }
        public IExpression? Expression { get; }
        public IOperand? Operand { get; }

        public CreateObjectField(string fieldName, IExpression? expression)
        {
            FieldName = fieldName;
            Expression = expression;
            Operand = null;
        }
        
        public CreateObjectField(string fieldName, IOperand? operand)
        {
            FieldName = fieldName;
            Operand = operand;
            Expression = null;
        }
    }

    private IExpressionResult CreateObject(
        NewLoweredConcreteTypeReference type,
        string variantName,
        IEnumerable<CreateObjectField> fields,
        IPlace? destination)
    {
        // always assign to a local, so fields get assign within the stack, then if needed, copy to it's destination
        
        var localName = LocalName((uint)_locals.Count);
        var localDestination = destination as Local ?? new Local(localName);

        if (destination is not Local)
        {
            _locals.Add(new NewMethodLocal(localName, null, type));
        }
        
        _basicBlockStatements.Add(new Assign(
            localDestination,
            new CreateObject(type)));

        foreach (var createObjectField in fields)
        {
            var field = new Field(localDestination, createObjectField.FieldName, variantName);
            if (createObjectField.Expression is {} expression)
            {
                NewLowerExpression(expression, field);
            }
            else if (createObjectField.Operand is {} operand)
            {
                _basicBlockStatements.Add(new Assign(
                    field,
                    new Use(operand)));
            }
        }

        if (destination is Local)
        {
            return new PlaceResult(destination);
        }

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(
                destination,
                new Use(new Copy(localDestination))));
        }

        return new PlaceResult(destination ?? localDestination);
    }

    private IExpressionResult LowerTuple(TupleExpression tupleExpression, IPlace? destination)
    {
        if (tupleExpression.Values.Count == 1)
        {
            return NewLowerExpression(tupleExpression.Values[0], destination);
        }

        var typeReference = (GetTypeReference(tupleExpression.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

        return CreateObject(
            typeReference,
            ClassVariantName,
            tupleExpression.Values.Select((x, i) => new CreateObjectField($"Item{i}", x)),
            destination);
    }

    private IExpressionResult LowerReturn(MethodReturnExpression methodReturnExpression)
    {
        if (methodReturnExpression.MethodReturn.Expression is not null)
        {
            NewLowerExpression(methodReturnExpression.MethodReturn.Expression, new Local(ReturnValueLocalName));
        }

        if (_controlFlowDepth > 0)
        {
            _basicBlocks[^1].Terminator = new TempGoToReturn();
            _basicBlockStatements = [];
            _basicBlocks.Add(new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements));
        }
        else
        {
            _basicBlocks[^1].Terminator = new Return();
        }

        return new OperandResult(new UnitConstant());
    }

    private IExpressionResult LowerMethodCall(MethodCallExpression e, IPlace? destination)
    {
        var returnType = GetTypeReference(e.ResolvedType.NotNull());
        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            var local = new NewMethodLocal(localName, null, returnType);
            _locals.Add(local);
            
            destination = new Local(localName);
        }
        
        var instantiatedFunction = e.MethodCall.Method switch
        {
            MemberAccessExpression { MemberAccess.InstantiatedFunction: var fn } => fn,
            StaticMemberAccessExpression { StaticMemberAccess.InstantiatedFunction: var fn } => fn,
            ValueAccessorExpression { FunctionInstantiation: var fn } => fn,
            _ => null
        };

        IReadOnlyList<IOperand> originalArguments = [..e.MethodCall.ArgumentList.Select(x => NewLowerExpression(x, destination: null).ToOperand())];

        var arguments = new List<IOperand>(e.MethodCall.ArgumentList.Count);
        NewLoweredFunctionReference functionReference;

        // calling function object instead of normal function
        if (instantiatedFunction is null)
        {
            var functionObjectResult = NewLowerExpression(e.MethodCall.Method, destination: null);
            
            var methodType = (GetTypeReference(e.MethodCall.Method.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

            var fn = _importedPrograms.SelectMany(x =>
                x.Methods.Where(y => y.Name == $"Function`{e.MethodCall.ArgumentList.Count + 1}__Call"))
                .First();
            
            functionReference = GetFunctionReference(
                    fn.Id,
                    [],
                    methodType.TypeArguments);

            arguments.Add(functionObjectResult.ToOperand());
            
            arguments.AddRange(originalArguments);

            var lastBasicBlock = _basicBlocks[^1];
            _basicBlockStatements = [];
            var newBasicBlock = new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements);
            _basicBlocks.Add(newBasicBlock);

            lastBasicBlock.Terminator = new MethodCall(functionReference, arguments, destination, newBasicBlock.Id);

            return new PlaceResult(destination);
        }
        
        IReadOnlyList<INewLoweredTypeReference> ownerTypeArguments = [];
        if (e.MethodCall.Method is MemberAccessExpression memberAccess)
        {
            var owner = NewLowerExpression(memberAccess.MemberAccess.Owner, null);
            arguments.Add(owner.ToOperand());
            ownerTypeArguments = GetTypeReference(memberAccess.MemberAccess.Owner.ResolvedType.NotNull()).NotNull() is NewLoweredConcreteTypeReference concrete
                ? concrete.TypeArguments
                : throw new UnreachableException("Shouldn't ever be able to call a method on a generic parameter");
        }
        else if (instantiatedFunction.ClosureTypeId is not null)
        {
            var type = GetDataType(instantiatedFunction.ClosureTypeId);
            
            var localName = LocalName((uint)_locals.Count);
            _locals.Add(new NewMethodLocal(
                localName,
                null,
                new NewLoweredConcreteTypeReference(
                    type.Name,
                    type.Id,
                    [])));
            CreateClosureObject(instantiatedFunction, new Local(localName));
            arguments.Add(new Copy(new Local(localName)));
        }
        else if (instantiatedFunction is { IsStatic: false, OwnerType: not null }
                 && _currentType is not null
                 && EqualTypeReferences(GetTypeReference(instantiatedFunction.OwnerType), _currentType)
                 && _currentFunction is not null
                 && EqualTypeReferences(_currentFunction.Value.LoweredMethod.ParameterLocals[0].Type, _currentType))
        {
            arguments.Add(new Copy(new Local(ParameterLocalName(parameterIndex: 0))));
        }

        if (e.MethodCall.Method is StaticMemberAccessExpression staticMemberAccess)
        {
            ownerTypeArguments = (GetTypeReference(staticMemberAccess.OwnerType.NotNull())
                as NewLoweredConcreteTypeReference).NotNull().TypeArguments;
        }
        else if (e.MethodCall.Method is ValueAccessorExpression valueAccessor)
        {
            if (_currentType is not null)
            {
                ownerTypeArguments = _currentType.TypeArguments;
            }
            else if (valueAccessor.FunctionInstantiation.NotNull()
                    .OwnerType is {} ownerType)
            {
                var ownerTypeReference = GetTypeReference(ownerType);
                if (ownerTypeReference is NewLoweredConcreteTypeReference
                    {
                        TypeArguments: var ownerReferenceTypeArguments
                    })
                {
                    ownerTypeArguments = ownerReferenceTypeArguments;
                }
            }
        }

        functionReference = GetFunctionReference(instantiatedFunction.FunctionId,
            [..instantiatedFunction.TypeArguments.Select(GetTypeReference)],
            ownerTypeArguments);

        arguments.AddRange(originalArguments);

        {
            var lastBasicBlock = _basicBlocks[^1];
            _basicBlockStatements = [];
            var newBasicBlock = new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements);
            _basicBlocks.Add(newBasicBlock);

            lastBasicBlock.Terminator = new MethodCall(functionReference, arguments, destination, newBasicBlock.Id);

            return new PlaceResult(destination);
        }
    }

    private void CreateClosureObject(TypeChecking.TypeChecker.InstantiatedFunction instantiatedFunction, IPlace destination)
    {
        Debug.Assert(instantiatedFunction.ClosureTypeId is not null);

        var closureType = _types[instantiatedFunction.ClosureTypeId];
        var closureTypeReference = new NewLoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []);
        
        Debug.Assert(_currentFunction.HasValue);
        
        _basicBlockStatements.Add(
            new Assign(
                destination,
                new CreateObject(closureTypeReference)));

        var assignedFields = new HashSet<string>();

        foreach (var variable in instantiatedFunction.AccessedOuterVariables)
        {
            switch (variable)
            {
                case TypeChecking.TypeChecker.LocalVariable localVariable:
                    {
                        if (localVariable.ContainingFunction != _currentFunction.Value.FunctionSignature)
                        {
                            Debug.Assert(localVariable.ContainingFunction is not null);
                            Debug.Assert(localVariable.ContainingFunction.LocalsTypeId is not null);
                            Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId is not null);

                            var currentClosureType = _types[
                                _currentFunction.Value.FunctionSignature.ClosureTypeId
                            ];
                            var currentClosureTypeReference = new NewLoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                    EqualTypeReferences(_currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                        currentClosureTypeReference));

                            var otherLocalsType = _types[
                                localVariable.ContainingFunction.LocalsTypeId
                            ];

                            if (assignedFields.Add(otherLocalsType.Name))
                            {
                                _basicBlockStatements.Add(
                                    new Assign(
                                        new Field(destination, otherLocalsType.Name, ClassVariantName),
                                        new Use(new Copy(
                                            new Field(
                                                new Local(ParameterLocalName(0)),
                                                otherLocalsType.Name,
                                                ClassVariantName)))));
                            }

                            break;
                        }
                        
                        Debug.Assert(_currentFunction.Value.FunctionSignature.LocalsTypeId is not null);
                        var localsType = _types[_currentFunction.Value.FunctionSignature.LocalsTypeId];

                        if (assignedFields.Add(localsType.Name))
                        {
                            _basicBlockStatements.Add(
                                new Assign(
                                    new Field(destination, localsType.Name, ClassVariantName),
                                    new Use(new Copy(new Local(LocalsObjectLocalName)))));
                        }
                        
                        break;
                    }
                case TypeChecking.TypeChecker.ThisVariable:
                case TypeChecking.TypeChecker.FieldVariable:
                    {
                        Debug.Assert(_currentType is not null);

                        if (_currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var currentClosureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                            var currentClosureTypeReference = new NewLoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                    currentClosureTypeReference));

                            if (assignedFields.Add(ClosureThisFieldName))
                            {
                                _basicBlockStatements.Add(
                                    new Assign(
                                        new Field(
                                            destination,
                                            ClosureThisFieldName,
                                            ClassVariantName),
                                        new Use(new Copy(new Field(
                                            new Local(ParameterLocalName(0)),
                                            ThisParameterName,
                                            ClassVariantName)))));
                            }
                            break;
                        }

                        Debug.Assert(
                            EqualTypeReferences(
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                _currentType));

                        if (assignedFields.Add(ClosureThisFieldName))
                        {
                            _basicBlockStatements.Add(
                                new Assign(
                                    new Field(destination, ClosureThisFieldName, ClassVariantName),
                                    new Use(new Copy(new Local(ParameterLocalName(0))))));
                        }
                        break;
                    }
                case TypeChecking.TypeChecker.FunctionSignatureParameter parameter:
                    {
                        if (parameter.ContainingFunction != _currentFunction.Value.FunctionSignature)
                        {
                            Debug.Assert(parameter.ContainingFunction is not null);
                            Debug.Assert(parameter.ContainingFunction.LocalsTypeId is not null);
                            Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId is not null);

                            var currentClosureType = _types[
                                _currentFunction.Value.FunctionSignature.ClosureTypeId
                            ];
                            var currentClosureTypeReference = new NewLoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                    EqualTypeReferences(_currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                        currentClosureTypeReference));

                            var otherLocalsType = GetDataType(parameter.ContainingFunction.LocalsTypeId);

                            if (assignedFields.Add(otherLocalsType.Name))
                            {
                                _basicBlockStatements.Add(new Assign(
                                    new Field(destination, otherLocalsType.Name, ClassVariantName),
                                    new Use(new Copy(new Field(
                                        new Local(ParameterLocalName(0)),
                                        otherLocalsType.Name,
                                        ClassVariantName)))));
                            }

                            break;
                        }
                        Debug.Assert(_currentFunction.Value.FunctionSignature.LocalsTypeId is not null);
                        var localsType = GetDataType(_currentFunction.Value.FunctionSignature.LocalsTypeId);

                        if (assignedFields.Add(localsType.Name))
                        {
                            _basicBlockStatements.Add(new Assign(
                                new Field(destination, localsType.Name, ClassVariantName),
                                new Use(new Copy(new Local(LocalsObjectLocalName)))));
                        }

                        break;
                    }
            }
        }
    }

    private IExpressionResult LowerBlock(BlockExpression blockExpression, IPlace? destination)
    {
        IExpressionResult? result = null;
        foreach (var innerExpression in blockExpression.Block.Expressions)
        {
            result = NewLowerExpression(innerExpression, destination: null);
        }

        // if no result, then it must just be a unit constant
        result ??= new OperandResult(new UnitConstant());

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(
                destination,
                new Use(result.ToOperand())));
        }

        return result;
    }

    private IExpressionResult LowerUnaryOperator(UnaryOperatorExpression unaryOperatorExpression, IPlace? destination)
    {
        if (unaryOperatorExpression.UnaryOperator.OperatorType == UnaryOperatorType.FallOut)
        {
            throw new NotImplementedException();
        }
        
        var valueOperand = NewLowerExpression(unaryOperatorExpression.UnaryOperator.Operand.NotNull(), destination: null).NotNull();

        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(unaryOperatorExpression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }

        _basicBlockStatements.Add(
            new Assign(
                destination,
                new UnaryOperation(valueOperand.ToOperand(), unaryOperatorExpression.UnaryOperator.OperatorType switch
                {
                    UnaryOperatorType.Not => UnaryOperationKind.Not,
                    _ => throw new UnreachableException()
                })));

        return new PlaceResult(destination);
    }

    private IExpressionResult LowerValueAccessor(ValueAccessorExpression e, IPlace? destination)
    {
        if (e is { ValueAccessor.AccessType: ValueAccessType.Variable, FunctionInstantiation: { } fn })
        {
            // function access already assigns the value to destination, so handle it separately
            return FunctionAccess(fn, (e.ResolvedType as TypeChecking.TypeChecker.FunctionObject).NotNull(),
                destination);
        }
        
        var operand = e switch
        {
            { ValueAccessor: { AccessType: ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new OperandResult(new StringConstant(stringLiteral)),
            { ValueAccessor: { AccessType: ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }, ResolvedType: var resolvedType} =>
                new OperandResult(IsIntSigned(resolvedType.NotNull())
                    ? new IntConstant(intValue, GetIntSize(resolvedType.NotNull()))
                    : new UIntConstant((ulong)intValue, GetIntSize(resolvedType.NotNull()))),
            { ValueAccessor: { AccessType: ValueAccessType.Literal, Token.Type: TokenType.True }} => new OperandResult(new BoolConstant(true)),
            { ValueAccessor: { AccessType: ValueAccessType.Literal, Token.Type: TokenType.False }} => new OperandResult(new BoolConstant(false)),
            { ValueAccessor.AccessType: ValueAccessType.Variable, ReferencedVariable: {} variable} => VariableAccess(variable),
            _ => throw new UnreachableException($"{e}")
        };

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(destination, new Use(operand.ToOperand())));
        }

        return operand;
        
        IExpressionResult FunctionAccess(
                TypeChecking.TypeChecker.InstantiatedFunction innerFn,
                TypeChecking.TypeChecker.FunctionObject typeReference,
                IPlace? innerDestination)
        {
            var ownerTypeArguments = _currentType?.TypeArguments ?? [];

            var functionObjectParameters = new List<CreateObjectField>
            {
                new ("FunctionReference",
                    new FunctionPointerConstant(
                        GetFunctionReference(
                            innerFn.FunctionId,
                            [..innerFn.TypeArguments.Select(GetTypeReference)],
                            ownerTypeArguments)))
            };
            
            if (innerFn.ClosureTypeId is not null)
            {
                var dataType = GetDataType(innerFn.ClosureTypeId).NotNull();
                
                var localName = LocalName((uint)_locals.Count);
                
                // todo - I think closure type might be able to be generic, so will need to pass in all type parameters here
                _locals.Add(new NewMethodLocal(localName, null, new NewLoweredConcreteTypeReference(
                    dataType.Name,
                    dataType.Id,
                    [])));

                CreateClosureObject(innerFn, new Local(localName));
                
                functionObjectParameters.Add(new CreateObjectField("FunctionParameter", new Copy(new Local(localName))));
            }
            else if (innerFn is { IsStatic: false, OwnerType: not null }
                     && _currentType is not null
                     && EqualTypeReferences(GetTypeReference(innerFn.OwnerType), _currentType)
                     && _currentFunction is not null
                     && EqualTypeReferences(_currentFunction.Value.LoweredMethod.ParameterLocals[0].Type, _currentType))
            {
                functionObjectParameters.Add(new CreateObjectField("FunctionParameter", new Copy(new Local(ParameterLocalName(0)))));
            }

            return CreateObject(
                (GetTypeReference(typeReference) as NewLoweredConcreteTypeReference).NotNull(),
                ClassVariantName,
                functionObjectParameters,
                innerDestination);
        }

        IExpressionResult VariableAccess(
            TypeChecking.TypeChecker.IVariable variable)
        {
            switch (variable)
            {
                case TypeChecking.TypeChecker.LocalVariable localVariable:
                {
                    return new PlaceResult(GetLocalVariablePlace(localVariable));
                }
                case TypeChecking.TypeChecker.ThisVariable thisVariable:
                    {
                        Debug.Assert(_currentFunction is not null);
                        Debug.Assert(_currentType is not null);

                        if (thisVariable.ReferencedInClosure
                                && _currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                            var closureTypeReference = new  NewLoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureType.Id,
                                        []);
                            Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                    closureTypeReference));

                            return new PlaceResult(
                                new Field(
                                    new Local(ParameterLocalName(0)),
                                    ClosureThisFieldName,
                                    ClassVariantName));
                        }

                        Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                        Debug.Assert(EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                    _currentType));

                        return new PlaceResult(new Local(ParameterLocalName(0)));
                    }
                case TypeChecking.TypeChecker.FieldVariable fieldVariable
                    when fieldVariable.ContainingSignature.Id == _currentType?.DefinitionId
                        && _currentFunction is not null:
                {
                    if (fieldVariable.IsStaticField)
                    {
                        return new PlaceResult(new StaticField(_currentType, fieldVariable.Name.StringValue));
                    }
                    
                    if (_currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                    {
                        var loweredMethod = _currentFunction.Value.LoweredMethod;
                        var fnSignature = _currentFunction.Value.FunctionSignature;
                        var closureType = _types[fnSignature.ClosureTypeId];
                        var closureTypeReference = new NewLoweredConcreteTypeReference(closureType.Name, closureType.Id, []);
                    
                        // we're a closure, so reference the value through the "this" field
                        // of the closure type
                        Debug.Assert(loweredMethod.ParameterLocals.Count > 0);
                        Debug.Assert(
                                EqualTypeReferences(
                                    loweredMethod.ParameterLocals[0].Type,
                                    closureTypeReference));

                        return new PlaceResult(new Field(
                            new Field(
                                new Local(ParameterLocalName(0)),
                                ClosureThisFieldName,
                                ClassVariantName),
                            fieldVariable.Name.StringValue,
                            ClassVariantName)
                        );
                    }
                    
                    if (_currentFunction.Value.LoweredMethod.ParameterLocals.Count == 0
                            || !EqualTypeReferences(
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                _currentType))
                    {
                        throw new InvalidOperationException("Expected to be in instance function");
                    }
                    
                    // todo: assert we're in a class and have _classVariant

                    return new PlaceResult(
                        new Field(
                            new Local(ParameterLocalName(0)),
                            fieldVariable.Name.StringValue,
                            ClassVariantName));
                }
                case TypeChecking.TypeChecker.FunctionSignatureParameter argument:
                {
                    Debug.Assert(_currentFunction is not null);
                    
                    var argumentIndex = argument.ParameterIndex;
                    if (!argument.ReferencedInClosure)
                    {
                        if (argument.ContainingFunction.AccessedOuterVariables.Count > 0
                                || (argument.ContainingFunction.OwnerType is not null
                                    && !argument.ContainingFunction.IsStatic))
                        {
                            argumentIndex++;
                        }

                        return new PlaceResult(new Local(ParameterLocalName(argumentIndex)));
                    }
                    
                    var currentFunction = _currentFunction.NotNull();
                    var containingFunction = argument.ContainingFunction.NotNull();
                    var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                    if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                    {
                        return new PlaceResult(new Field(new Local(LocalsObjectLocalName), argument.Name.StringValue,
                            ClassVariantName));
                    }

                    return new PlaceResult(new Field(
                        new Field(
                            new Local(ParameterLocalName(0)),
                            containingFunctionLocals.Name,
                            ClassVariantName),
                        argument.Name.StringValue,
                        ClassVariantName));
                }
            }

            throw new UnreachableException($"{variable.GetType()}");
        }
    }

    private IPlace GetLocalVariablePlace(TypeChecking.TypeChecker.LocalVariable variable)
    {
        if (!variable.ReferencedInClosure)
        {
            var local = _locals.First(x => x.UserGivenName == variable.Name.StringValue);
            return new Local(local.CompilerGivenName);
        }

        var currentFunction = _currentFunction.NotNull();
        var containingFunction = variable.ContainingFunction.NotNull();
        var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
        if (containingFunction.Id == currentFunction.FunctionSignature.Id)
        {
            return new Field(
                new Local(LocalsObjectLocalName),
                variable.Name.StringValue,
                ClassVariantName);
        }
                        
        return new Field(
            new Field(
                new Local("_param0"),
                containingFunctionLocals.Name,
                ClassVariantName),
            variable.Name.StringValue,
            ClassVariantName);  
    }

    private static byte GetIntSize(TypeChecking.TypeChecker.ITypeReference type)
    {
        if (type is TypeChecking.TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType)
        {
            type = unspecifiedSizedIntType.ResolvedIntType.NotNull();
        }
        if (type is not TypeChecking.TypeChecker.InstantiatedClass klass)
        {
            throw new InvalidOperationException($"{type} must be instantiated class");
        }
        var typeId = klass.Signature.Id;
        if (typeId == DefId.Int64 || typeId == DefId.UInt64)
        {
            return 8;
        }

        if (typeId == DefId.Int32 || typeId == DefId.UInt32)
        {
            return 4;
        }

        if (typeId == DefId.Int16 || typeId == DefId.UInt16)
        {
            return 2;
        }

        if (typeId == DefId.Int8 || typeId == DefId.UInt8)
        {
            return 1;
        }

        throw new UnreachableException();
    }
    
    private static bool IsIntSigned(TypeChecking.TypeChecker.ITypeReference type)
    {
        if (type is TypeChecking.TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType)
        {
            type = unspecifiedSizedIntType.ResolvedIntType.NotNull();
        }
        
        if (type is not TypeChecking.TypeChecker.InstantiatedClass klass)
        {
            throw new InvalidOperationException($"{type} must be instantiated class");
        }
        var typeId = klass.Signature.Id;
        if (new[] { DefId.Int8, DefId.Int16, DefId.Int32, DefId.Int64 }.Contains(typeId))
        {
            return true;
        }
        if (new[] { DefId.UInt8, DefId.UInt16, DefId.UInt32, DefId.UInt64 }.Contains(typeId))
        {
            return false;
        }

        throw new UnreachableException();
    }

    private IExpressionResult LowerVariableDeclaration(VariableDeclarationExpression e)
    {
        var result = new OperandResult(new UnitConstant());
        
        var variableName = e.VariableDeclaration.Variable.NotNull()
            .Name.StringValue;

        var referencedInClosure = e.VariableDeclaration.Variable!.ReferencedInClosure;

        if (e.VariableDeclaration.Value is null)
        {
            // noop
            return result;
        }

        if (!referencedInClosure)
        {
            var variable = _locals.First(x =>
                x.UserGivenName == e.VariableDeclaration.Variable.NotNull().Name.StringValue);
            NewLowerExpression(e.VariableDeclaration.Value, destination: new Local(variable.CompilerGivenName));
            return result;
        }

        NewLowerExpression(e.VariableDeclaration.Value, destination: new Field(new Local(LocalsObjectLocalName), FieldName: variableName, ClassVariantName));

        return result;
    }
    
    private IExpressionResult LowerBinaryExpression(BinaryOperatorExpression binaryOperatorExpression, IPlace? destination)
    {
        if (binaryOperatorExpression.BinaryOperator.OperatorType == BinaryOperatorType.ValueAssignment)
        {
            var leftResult = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null);
            if (leftResult is not PlaceResult { Value: var leftPlace })
            {
                throw new InvalidOperationException("Value Assignment left operand must be a place");
            }
        
            NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: leftPlace);

            if (destination is not null)
            {
                _basicBlockStatements.Add(new Assign(destination, new Use(new Copy(leftPlace))));
            }

            return new PlaceResult(destination ?? leftPlace);
        }
        
        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(binaryOperatorExpression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }
        
        switch (binaryOperatorExpression.BinaryOperator.OperatorType)
        {
            case BinaryOperatorType.BooleanAnd:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), null).NotNull();

                if (_basicBlocks.Count == 0)
                {
                    _basicBlocks.Add(new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements));
                }
                var previousBasicBlock = _basicBlocks[^1];

                var trueBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                var falseBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 1}");
                var afterBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 2}");
                
                previousBasicBlock.Terminator = new SwitchInt(
                    leftOperand.ToOperand(),
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, falseBasicBlockId }
                    },
                    trueBasicBlockId);
                
                _basicBlockStatements = [];
                var trueBasicBlock = new BasicBlock(trueBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(trueBasicBlock);

                _controlFlowDepth++;
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null)
                    .NotNull();
                _controlFlowDepth--;
                
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand.ToOperand())));
                
                _basicBlockStatements = [new Assign(destination, new Use(new BoolConstant(false)))];
                var falseBasicBlock = new BasicBlock(falseBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(falseBasicBlock);

                _basicBlockStatements = [];
                _basicBlocks.Add(new BasicBlock(afterBasicBlockId, _basicBlockStatements));
                break;
            }
            case BinaryOperatorType.BooleanOr:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null).NotNull();

                if (_basicBlocks.Count == 0)
                {
                    _basicBlocks.Add(new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements));
                }
                var previousBasicBlock = _basicBlocks[^1];

                var falseBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                var trueBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 1}");
                var afterBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 2}");
                
                previousBasicBlock.Terminator = new SwitchInt(
                    leftOperand.ToOperand(),
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, falseBasicBlockId }
                    },
                    trueBasicBlockId);
                
                _basicBlockStatements = [];
                var falseBasicBlock = new BasicBlock(falseBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(falseBasicBlock);

                _controlFlowDepth++;
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null)
                    .NotNull();
                _controlFlowDepth--;
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand.ToOperand())));
                
                _basicBlockStatements = [new Assign(destination, new Use(new BoolConstant(true)))];
                var trueBasicBlock = new BasicBlock(trueBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(trueBasicBlock);

                _basicBlockStatements = [];
                _basicBlocks.Add(new BasicBlock(afterBasicBlockId, _basicBlockStatements));
                break;
            }
            default:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null).NotNull();
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null).NotNull();
                
                var binaryOperatorKind = binaryOperatorExpression.BinaryOperator.OperatorType switch
                {
                    BinaryOperatorType.LessThan => BinaryOperationKind.LessThan,
                    BinaryOperatorType.GreaterThan => BinaryOperationKind.GreaterThan,
                    BinaryOperatorType.Plus => BinaryOperationKind.Add,
                    BinaryOperatorType.Minus => BinaryOperationKind.Subtract,
                    BinaryOperatorType.Multiply => BinaryOperationKind.Multiply,
                    BinaryOperatorType.Divide => BinaryOperationKind.Divide,
                    BinaryOperatorType.EqualityCheck => BinaryOperationKind.Equal,
                    BinaryOperatorType.NegativeEqualityCheck => BinaryOperationKind.NotEqual,
                    _ => throw new ArgumentOutOfRangeException()
                };

                _basicBlockStatements.Add(new Assign(
                    destination,
                    new BinaryOperation(leftOperand.ToOperand(), rightOperand.ToOperand(), binaryOperatorKind)));
                break;
            }
        }

        return new PlaceResult(destination);
    }
}