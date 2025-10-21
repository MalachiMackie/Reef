using System.Diagnostics;
using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    public ILoweredExpression LowerExpression(
            Expressions.IExpression expression)
    {
        return expression switch
        {
            Expressions.ValueAccessorExpression e => LowerValueAccessorExpression(e),
            Expressions.VariableDeclarationExpression e => LowerVariableDeclarationExpression(e),
            Expressions.BinaryOperatorExpression e => LowerBinaryOperatorExpression(e),
            Expressions.UnaryOperatorExpression e => LowerUnaryOperatorExpression(e),
            Expressions.BlockExpression e => LowerBlockExpression(e), 
            Expressions.ObjectInitializerExpression e => LowerObjectInitializationExpression(e), 
            Expressions.UnionClassVariantInitializerExpression e =>
                LowerUnionClassVariantInitializerExpression(e), 
            Expressions.StaticMemberAccessExpression e => LowerStaticMemberAccess(e), 
            Expressions.MemberAccessExpression e => LowerMemberAccessExpression(e),
            Expressions.MethodCallExpression e => LowerMethodCallExpression(e),
            Expressions.MethodReturnExpression e => LowerMethodReturnExpression(e),
            Expressions.TupleExpression e => LowerTupleExpression(e),
            Expressions.IfExpressionExpression e => LowerIfExpression(e),
            Expressions.MatchesExpression e => LowerMatchesExpression(e),
            Expressions.MatchExpression e => LowerMatchExpression(e),
            _ => throw new NotImplementedException($"{expression.GetType()}")
        };
    }

    private ILoweredExpression LowerMatchPatterns(
            List<(IPattern Pattern, ILoweredExpression Expression)> patterns,
            ILoweredExpression accessExpression,
            ILoweredExpression? otherwise)
    {
        var classPatterns = new List<(ClassPattern Pattern, ILoweredExpression MatchArmExpression)>();
        var unionPatterns = new List<(IPattern Pattern, ILoweredExpression MatchArmExpression)>();

        foreach (var (pattern, armExpression) in patterns)
        {
            switch (pattern)
            {
                case DiscardPattern:
                    otherwise = armExpression;
                    if (patterns.Count == 1)
                    {
                        return otherwise;
                    }

                    continue;
                case VariableDeclarationPattern variableDeclarationPattern:
                {
                    otherwise = new BlockExpression(
                        [
                            new VariableDeclarationAndAssignmentExpression(
                                variableDeclarationPattern.VariableName.StringValue,
                                accessExpression,
                                false),
                            armExpression
                        ],
                        armExpression.ResolvedType,
                        true);
                    if (patterns.Count == 1)
                    {
                        return otherwise;
                    }

                    continue;
                }
                // for now, type pattern is guaranteed to be the only arm that matches
                // when we eventually get interfaces, this needs to change
                // Debug.Assert(patterns.Count == 1);
                case TypePattern { VariableName.StringValue: var variableName }:
                    return new BlockExpression(
                        [
                            new VariableDeclarationAndAssignmentExpression(
                                variableName,
                                accessExpression,
                                false),
                            armExpression
                        ],
                        armExpression.ResolvedType,
                        true);
                case TypePattern:
                    return armExpression;
                case ClassPattern classPattern:
                    classPatterns.Add((classPattern, armExpression));
                    continue;
                case UnionVariantPattern or UnionTupleVariantPattern or UnionClassVariantPattern:
                    unionPatterns.Add((pattern, armExpression));
                    continue;
                default:
                    throw new UnreachableException($"{pattern.GetType()}");
            }
        }

        if (classPatterns.Count > 0)
        {
            // for now, class patterns are mutually exclusive with union patterns until we have
            // interfaces
            Debug.Assert(unionPatterns.Count == 0);

            LoweredConcreteTypeReference? typeReference = null;
            foreach (var pattern in classPatterns.Select(x => x.Pattern))
            {
                var patternTypeReference = (GetTypeReference(pattern.TypeReference.NotNull()) as LoweredConcreteTypeReference).NotNull();
                typeReference ??= patternTypeReference;

                Debug.Assert(EqualTypeReferences(typeReference, patternTypeReference));
            }

            // check all class patterns are for the same type. This needs to change when we have interfaces

            var dataType = GetDataType(typeReference!.DefinitionId, typeReference.TypeArguments);

            var dataTypeFields = dataType.Variants[0].Fields;
            if (dataTypeFields.Count == 0)
            {
                throw new NotImplementedException();
            }

            IReadOnlyCollection<IPatternMatchingNode>? previousNodes = null;
            var originalNode = new FieldNode(
                dataType.Variants[0].Fields[0].Name,
                [],
                [..classPatterns.Select(x => ((IPattern)x.Pattern, x.MatchArmExpression))])
            {
                Otherwise = otherwise
            };
            IReadOnlyCollection<IPatternMatchingNode>? nodes = [originalNode];

            foreach (var field in dataType.Variants[0].Fields)
            {
                if (nodes is null)
                {
                    Debug.Assert(previousNodes is not null);
                    foreach (var previousNode in previousNodes)
                    {
                        foreach (var b in previousNode.UniquePatterns)
                        {
                            b.NextField = new FieldNode(field.Name, [], b.OriginalPatterns){Otherwise = otherwise};
                        }
                    }
                    nodes = [..previousNodes.SelectMany(x => x.UniquePatterns.Select(y => y.NextField.NotNull()))];
                }

                foreach (var (_, uniquePatterns, originalPatterns) in nodes.OfType<FieldNode>())
                {
                    foreach (var (pattern, armExpression) in originalPatterns)
                    {
                        var classPattern = (pattern as ClassPattern).NotNull();
                        var fieldPattern = classPattern.FieldPatterns.FirstOrDefault(x => x.FieldName.StringValue == field.Name);
                        if (fieldPattern is null)
                        {
                            throw new NotImplementedException();
                        }

                        var foundUniquePattern = uniquePatterns.FirstOrDefault(x => PatternsEquivalent(x.Pattern, fieldPattern.Pattern.NotNull()));

                        if (foundUniquePattern is null)
                        {
                            foundUniquePattern = new(fieldPattern.Pattern.NotNull(), []);
                            uniquePatterns.Add(foundUniquePattern);
                        }
                        foundUniquePattern.OriginalPatterns.Add((classPattern, armExpression));
                    }
                }
                previousNodes = nodes;
                nodes = null;
                
            }

            return ProcessNode(originalNode);

            ILoweredExpression ProcessNode(FieldNode node)
            {
                var nextPatterns = new List<(IPattern, ILoweredExpression)>();
                foreach (var uniquePattern in node.UniquePatterns)
                {
                    ILoweredExpression exp;
                    if (uniquePattern.NextField is null)
                    {
                        Debug.Assert(uniquePattern.OriginalPatterns.Count == 1);
                        var (originalPattern, originalExpression) = uniquePattern.OriginalPatterns[0];
                        var variableName = (originalPattern as ClassPattern).NotNull().VariableName?.StringValue;
                        if (variableName is not null)
                        {
                            originalExpression = new BlockExpression(
                                [
                                    new VariableDeclarationAndAssignmentExpression(variableName, accessExpression, false),
                                    originalExpression
                                ],
                                originalExpression.ResolvedType,
                                true);
                        }

                        exp = originalExpression;
                    }
                    else if (uniquePattern.NextField is FieldNode nextField)
                    {
                        exp = ProcessNode(nextField);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Expected structural node. Got {uniquePattern.NextField.GetType()}");
                    }
                    nextPatterns.Add((uniquePattern.Pattern, exp));
                }
                
                var fieldType = dataType.Variants[0].Fields.First(x => x.Name == node.FieldName).Type;

                var locals = _currentFunction.NotNull().LoweredMethod.Locals;
                var localName = $"Local{locals.Count}";
                locals.Add(new MethodLocal(localName, fieldType));

                var localDeclaration = new VariableDeclarationAndAssignmentExpression(
                    localName,
                    new FieldAccessExpression(
                        accessExpression,
                        node.FieldName,
                        "_classVariant",
                        true,
                        fieldType),
                    false);
                var localAccess = new LocalVariableAccessor(localName, true, fieldType);
                
                var innerExpression = LowerMatchPatterns(
                    nextPatterns,
                    localAccess,
                    node.Otherwise);
                
                return new BlockExpression(
                    [
                        localDeclaration,
                        innerExpression
                    ],
                    innerExpression.ResolvedType,
                    true);
            }
        }
        
        if (unionPatterns.Count > 0)
        {
            // for now, class patterns are mutually exclusive with union patterns until we have
            // interfaces
            Debug.Assert(classPatterns.Count == 0);

            LoweredConcreteTypeReference? typeReference = null;
            foreach (var pattern in unionPatterns.Select(x => x.Pattern))
            {
                var patternTypeReference = (GetTypeReference(pattern.TypeReference.NotNull()) as LoweredConcreteTypeReference).NotNull();
                typeReference ??= patternTypeReference;

                Debug.Assert(EqualTypeReferences(typeReference, patternTypeReference));
            }

            // check all class patterns are for the same type. This needs to change when we have interfaces

            var dataType = GetDataType(typeReference!.DefinitionId, typeReference.TypeArguments);

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

                foreach (var field in variant.Fields.Where(x => x.Name != "_variantIdentifier"))
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

            return ProcessNode(originalNode);

            ILoweredExpression ProcessNode(IPatternMatchingNode node)
            {
                var nextPatterns = new List<(IPattern, ILoweredExpression)>();
                foreach (var uniquePattern in node.UniquePatterns)
                {
                    ILoweredExpression expression;
                    if (uniquePattern.NextField is {UniquePatterns.Count: > 0})
                    {
                        expression = ProcessNode(uniquePattern.NextField);
                    }
                    else
                    {
                        Debug.Assert(uniquePattern.OriginalPatterns.Count == 1);
                        (var originalPattern, expression) = uniquePattern.OriginalPatterns[0];
                        var variableName = originalPattern switch
                        {
                            UnionVariantPattern variantPattern => variantPattern.VariableName?.StringValue,
                            UnionClassVariantPattern classVariantPattern => classVariantPattern.VariableName
                                ?.StringValue,
                            UnionTupleVariantPattern tupleVariantPattern => tupleVariantPattern.VariableName
                                ?.StringValue,
                            _ => null
                        };

                        if (variableName is not null)
                        {
                            expression = new BlockExpression(
                                [
                                    new VariableDeclarationAndAssignmentExpression(
                                        variableName,
                                        accessExpression,
                                        false),
                                    expression
                                ],
                                expression.ResolvedType,
                                true);
                        }
                    }

                    nextPatterns.Add((uniquePattern.Pattern, expression));
                }

                switch (node)
                {
                    case TopLevelNode:
                    {
                        var innerResults = new Dictionary<int, ILoweredExpression>();
                        foreach (var (pattern, expression) in nextPatterns)
                        {
                            var variantName = pattern switch
                            {
                                UnionVariantPattern variantPattern => variantPattern.VariantName.NotNull().StringValue,
                                UnionClassVariantPattern classVariantPattern => classVariantPattern.VariantName.StringValue,
                                UnionTupleVariantPattern tupleVariantPattern => tupleVariantPattern.VariantName.StringValue,
                                _ => throw new UnreachableException()
                            };
                            
                            var innerTypeReference = (GetTypeReference(pattern.TypeReference.NotNull()) as LoweredConcreteTypeReference)
                                .NotNull();
                            var innerDataType = GetDataType(innerTypeReference.DefinitionId,
                                innerTypeReference.TypeArguments);
                            
                            var variantIndex = innerDataType.Variants.Index().First(x => x.Item.Name == variantName).Index;
                            innerResults[variantIndex] = expression;
                        }

                        return new SwitchIntExpression(
                            new FieldAccessExpression(
                                accessExpression,
                                "_variantIdentifier",
                                dataType.Variants[0].Name,
                                true,
                                GetTypeReference(InstantiatedClass.UInt16)),
                            innerResults,
                            node.Otherwise ?? new UnreachableExpression(),
                            true,
                            innerResults.First().Value.ResolvedType);
                    }
                    case VariantFieldNode fieldNode:
                    {
                        var variant = dataType.Variants.First(x => x.Name == fieldNode.VariantName);
                        var fieldType = variant.Fields.First(x => x.Name == fieldNode.FieldName).Type;

                        var locals = _currentFunction.NotNull().LoweredMethod.Locals;
                        var localName = $"Local{locals.Count}";
                        locals.Add(new MethodLocal(localName, fieldType));

                        var localDeclaration = new VariableDeclarationAndAssignmentExpression(
                            localName,
                            new FieldAccessExpression(
                                accessExpression,
                                fieldNode.FieldName,
                                variant.Name,
                                true,
                                fieldType),
                            false);
                        var localAccess = new LocalVariableAccessor(localName, true, fieldType);

                        var innerExpression =  LowerMatchPatterns(
                            nextPatterns,
                            localAccess,
                            node.Otherwise);

                        return new BlockExpression(
                            [
                                localDeclaration,
                                innerExpression
                            ],
                            innerExpression.ResolvedType,
                            true);
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
        public List<(IPattern, ILoweredExpression)> OriginalPatterns { get; }
        public ILoweredExpression? Otherwise { get; set; }
    }

    private record TopLevelNode(
        List<B> UniquePatterns,
        List<(IPattern, ILoweredExpression)> OriginalPatterns) : IPatternMatchingNode
    {
        public ILoweredExpression? Otherwise { get; set; }
    }

    private record VariantFieldNode(
        string VariantName,
        string FieldName,
        List<B> UniquePatterns,
        List<(IPattern, ILoweredExpression)> OriginalPatterns) : IPatternMatchingNode
    {
        public ILoweredExpression? Otherwise { get; set; }
    }

    private record FieldNode(
        string FieldName,
        List<B> UniquePatterns,
        List<(IPattern, ILoweredExpression)> OriginalPatterns) : IPatternMatchingNode
    {
        public ILoweredExpression? Otherwise { get; set; }
    }

    private record B(IPattern Pattern, List<(IPattern, ILoweredExpression)> OriginalPatterns)
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

    private ILoweredExpression LowerMatchExpression(Expressions.MatchExpression e)
    {
        var accessExpression = LowerExpression(e.Value);
        
        var locals = _currentFunction.NotNull().LoweredMethod.Locals;
        var localName = $"Local{locals.Count}";
        var localType = accessExpression.ResolvedType;
        locals.Add(new MethodLocal(localName, localType));

        var localDeclaration = new VariableDeclarationAndAssignmentExpression(
            localName,
            accessExpression,
            false);
        var localAccessor = new LocalVariableAccessor(localName, true, localType);
        var expression =  LowerMatchPatterns(
            [..e.Arms.Select(x => (x.Pattern, LowerExpression(x.Expression.NotNull())))],
            localAccessor,
            null);

        return new BlockExpression(
            [
                localDeclaration,
                expression
            ],
            expression.ResolvedType,
            e.ValueUseful);
    }

    private BlockExpression LowerMatchesPattern(
            ILoweredExpression e,
            IPattern pattern,
            bool valueUseful)
    {
        var locals = _currentFunction.NotNull().LoweredMethod.Locals;
        var operandType = e.ResolvedType;
        var boolType = GetTypeReference(InstantiatedClass.Boolean);
        switch (pattern)
        {
            case DiscardPattern:
                {
                    var localName = $"Local{locals.Count}";
                    locals.Add(new MethodLocal(
                                localName,
                                operandType));
                    return new BlockExpression(
                            [
                                new VariableDeclarationAndAssignmentExpression(
                                    localName,
                                    e,
                                    false),
                                // true constant because discard always matches
                                new BoolConstantExpression(ValueUseful: true, Value: true)
                            ],
                            boolType,
                            valueUseful);
                }
            case VariableDeclarationPattern {VariableName.StringValue: var variableName}:
                {
                    // variable declaration patterns already have their locals added 
                    var localName = variableName;
                    return new BlockExpression(
                            [
                                new VariableDeclarationAndAssignmentExpression(
                                    localName,
                                    e,
                                    false),
                                // variable declaration patterns always match 
                                new BoolConstantExpression(ValueUseful: true, Value: true)
                            ],
                            boolType,
                            valueUseful);
                }
            case TypePattern { VariableName: var variableName }: 
                 {
                     string localName;
                     if (variableName is not null)
                     {
                         localName = variableName.StringValue;
                     }
                     else
                     {
                         localName = $"Local{locals.Count}";
                         locals.Add(new MethodLocal(
                                     localName,
                                     operandType));
                     }

                     return new BlockExpression(
                            [
                                new VariableDeclarationAndAssignmentExpression(
                                    localName,
                                    e,
                                    false),

                                // TODO: for now, type patterns always evaluate to true.
                                // In the future,this will only be true when the operands concrete
                                // type is known.When the operand is some dynamic dispatch
                                // interface, we will need some way of checking the concrete
                                // type at runtime
                                new BoolConstantExpression(ValueUseful: true, Value: true)
                            ],
                            boolType,
                            valueUseful);
                 }
            case UnionVariantPattern {
                VariableName: var variableName,
                TypeReference: var unionType,
                VariantName: var variantName
            }:
            {
                string localName;
                if (variableName is not null)
                {
                    localName = variableName.StringValue;
                }
                else
                {
                    locals.Add(new MethodLocal(
                            localName = $"Local{locals.Count}",
                            operandType));
                }
                var type = (GetTypeReference(unionType.NotNull()) as LoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId, type.TypeArguments);
                var variantIndex = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue)
                    .Index;

                return new BlockExpression(
                    [
                        new VariableDeclarationAndAssignmentExpression(
                            localName,
                            e,
                            false),
                        new UInt16EqualsExpression(
                            ValueUseful: true,
                            new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    localName,
                                    true,
                                    operandType),
                                "_variantIdentifier",
                                variantName!.StringValue,
                                true,
                                GetTypeReference(InstantiatedClass.UInt16)),
                            new UInt16ConstantExpression(
                                true,
                                (ushort)variantIndex))
                    ],
                    boolType,
                    valueUseful);
            }
            case UnionTupleVariantPattern
            {
                VariantName: var variantName,
                VariableName: var variableName,
                TupleParamPatterns: var tupleParamPatterns,
                TypeReference: var unionType,
            }:
            {
                string localName;
                if (variableName is not null)
                {
                    localName = variableName.StringValue;
                }
                else
                {
                    locals.Add(new MethodLocal(
                            localName = $"Local{locals.Count}",
                            operandType));
                }
                var type = (GetTypeReference(unionType.NotNull()) as LoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId, type.TypeArguments);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);
                
                // type checker should have checked there's at least tuple member
                Debug.Assert(tupleParamPatterns.Count > 0);

                var variantIdentifierCheck = new UInt16EqualsExpression(
                            ValueUseful: true,
                            new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    localName,
                                    true,
                                    operandType),
                                "_variantIdentifier",
                                variantName.StringValue,
                                true,
                                GetTypeReference(InstantiatedClass.UInt16)),
                            new UInt16ConstantExpression(
                                true,
                                (ushort)variantIndex));

                var localAccessor = new LocalVariableAccessor(localName, true, operandType);

                var tuplePatternExpressions = tupleParamPatterns.Select(
                        (x, i) => LowerMatchesPattern(
                            new FieldAccessExpression(
                                localAccessor,
                                $"Item{i}",
                                variantName.StringValue,
                                true,
                                // skip the first _variantIdentifier field
                                variant.Fields[i + 1].Type),
                            x,
                            valueUseful: true));

                var checkExpressions = tuplePatternExpressions.Prepend<ILoweredExpression>(variantIdentifierCheck)
                    .ToArray();

                Debug.Assert(checkExpressions.Length >= 2);

                // collate all the check expressions into a recurse bool and tree
                var lastExpression = checkExpressions[^1];
                for (var i = checkExpressions.Length - 2; i >= 0; i--)
                {
                    lastExpression = new BoolAndExpression(
                        true,
                        checkExpressions[i],
                        lastExpression);
                }

                return new BlockExpression(
                        [
                            new VariableDeclarationAndAssignmentExpression(
                                localName,
                                e,
                                false),
                            lastExpression
                        ],
                        boolType,
                        valueUseful);
            }
        case UnionClassVariantPattern
            {
                VariantName: var variantName,
                VariableName: var variableName,
                FieldPatterns: var fieldPatterns,
                TypeReference: var unionType,
            }:
            {
                string localName;
                if (variableName is not null)
                {
                    localName = variableName.StringValue;
                }
                else
                {
                    locals.Add(new MethodLocal(
                            localName = $"Local{locals.Count}",
                            operandType));
                }
                var type = (GetTypeReference(unionType.NotNull()) as LoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId, type.TypeArguments);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                var variantIdentifierCheck = new UInt16EqualsExpression(
                            ValueUseful: true,
                            new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    localName,
                                    true,
                                    operandType),
                                "_variantIdentifier",
                                variantName.StringValue,
                                true,
                                GetTypeReference(InstantiatedClass.UInt16)),
                            new UInt16ConstantExpression(
                                true,
                                (ushort)variantIndex));

                var localAccessor = new LocalVariableAccessor(localName, true, operandType);

                var checkExpressions = new List<ILoweredExpression>(fieldPatterns.Count + 1)
                {
                    variantIdentifierCheck
                };
                foreach (var fieldPattern in fieldPatterns)
                {
                    var fieldName = fieldPattern.FieldName.StringValue;
                    checkExpressions.Add(LowerMatchesPattern(
                        new FieldAccessExpression(
                            localAccessor,
                            fieldName,
                            variantName.StringValue,
                            true,
                            variant.Fields.First(x => x.Name == fieldName).Type),
                        fieldPattern.Pattern.NotNull(),
                        true));
                }

                // collate all the check expressions into a recurse bool and tree
                var lastExpression = checkExpressions[^1];
                for (var i = checkExpressions.Count - 2; i >= 0; i--)
                {
                    lastExpression = new BoolAndExpression(
                        true,
                        checkExpressions[i],
                        lastExpression);
                }

                return new BlockExpression(
                        [
                            new VariableDeclarationAndAssignmentExpression(
                                localName,
                                e,
                                false),
                            lastExpression
                        ],
                        boolType,
                        valueUseful);
            }
        case ClassPattern
            {
                VariableName: var variableName,
                FieldPatterns: var fieldPatterns,
                TypeReference: var unionType,
            }:
            {
                string localName;
                if (variableName is not null)
                {
                    localName = variableName.StringValue;
                }
                else
                {
                    locals.Add(new MethodLocal(
                            localName = $"Local{locals.Count}",
                            operandType));
                }
                var type = (GetTypeReference(unionType.NotNull()) as LoweredConcreteTypeReference).NotNull();
                var dataType = GetDataType(type.DefinitionId, type.TypeArguments);

                var localAccessor = new LocalVariableAccessor(localName, true, operandType);

                var checkExpressions = new List<ILoweredExpression>(fieldPatterns.Count);
                foreach (var fieldPattern in fieldPatterns)
                {
                    var fieldName = fieldPattern.FieldName.StringValue;
                    checkExpressions.Add(LowerMatchesPattern(
                        new FieldAccessExpression(
                            localAccessor,
                            fieldName,
                            "_classVariant",
                            true,
                            dataType.Variants[0].Fields.First(x => x.Name == fieldName).Type),
                        fieldPattern.Pattern.NotNull(),
                        true));
                }

                // collate all the check expressions into a recurse bool and tree
                var lastExpression = checkExpressions[^1];
                for (var i = checkExpressions.Count - 2; i >= 0; i--)
                {
                    lastExpression = new BoolAndExpression(
                        true,
                        checkExpressions[i],
                        lastExpression);
                }

                return new BlockExpression(
                        [
                            new VariableDeclarationAndAssignmentExpression(
                                localName,
                                e,
                                false),
                            lastExpression
                        ],
                        boolType,
                        valueUseful);
            }
        }
        throw new NotImplementedException(); 
    }

    private ILoweredExpression LowerMatchesExpression(
        Expressions.MatchesExpression e)
    {
        var valueExpression = LowerExpression(e.ValueExpression);

        return LowerMatchesPattern(valueExpression, e.Pattern.NotNull(), e.ValueUseful);
    }

    private SwitchIntExpression LowerIfExpression(
        Expressions.IfExpressionExpression e)
    {
        var elseBody = e.IfExpression.ElseBody is not null
            ? LowerExpression(e.IfExpression.ElseBody)
            : new NoopExpression();

        var checksAndBodies = e.IfExpression.ElseIfs
            .Select(x => (LowerExpression(x.CheckExpression), LowerExpression(x.Body.NotNull())));

        var lastExpression = elseBody;

        var typeReference = GetTypeReference(e.ResolvedType.NotNull());

        foreach (var (check, body) in checksAndBodies.Reverse())
        {
            lastExpression = new SwitchIntExpression(
                new CastBoolToIntExpression(check, true),
                new()
                { { 0, lastExpression } },
                body,
                e.ValueUseful,
                typeReference);
        }

        return new SwitchIntExpression(
            new CastBoolToIntExpression(LowerExpression(e.IfExpression.CheckExpression), true),
            new()
            { { 0, lastExpression } },
            LowerExpression(e.IfExpression.Body.NotNull()),
            e.ValueUseful,
            typeReference);
    }

    private ILoweredExpression LowerTupleExpression(
            Expressions.TupleExpression e)
    {
        if (e.Values.Count == 1)
        {
            return LowerExpression(e.Values[0]);
        }

        var tupleType = GetTypeReference(e.ResolvedType.NotNull()) as LoweredConcreteTypeReference;
        Debug.Assert(tupleType is not null, "tuple type is not concrete");

        return new CreateObjectExpression(
            tupleType,
            "_classVariant",
            e.ValueUseful,
            e.Values.Index().ToDictionary(x => $"Item{x.Index}", x => LowerExpression(x.Item)));
    }

    private MethodReturnExpression LowerMethodReturnExpression(
            Expressions.MethodReturnExpression e)
    {
        return new MethodReturnExpression(
                e.MethodReturn.Expression is not null
                    ? LowerExpression(e.MethodReturn.Expression)
                    : new UnitConstantExpression(true));
    }

    private CreateObjectExpression CreateClosureObject(
        InstantiatedFunction instantiatedFunction)
    {
        Debug.Assert(instantiatedFunction.ClosureTypeId is not null);

        var closureType = _types[instantiatedFunction.ClosureTypeId];
        var closureTypeReference = new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []);

        var fieldInitializers = new Dictionary<string, ILoweredExpression>();

        Debug.Assert(_currentFunction.HasValue);

        foreach (var variable in instantiatedFunction.AccessedOuterVariables)
        {
            switch (variable)
            {
                case LocalVariable localVariable:
                    {
                        if (localVariable.ContainingFunction != _currentFunction.Value.FunctionSignature)
                        {
                            Debug.Assert(localVariable.ContainingFunction is not null);
                            Debug.Assert(localVariable.ContainingFunction.LocalsTypeId is not null);
                            Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId is not null);

                            var currentClosureType = _types[
                                _currentFunction.Value.FunctionSignature.ClosureTypeId
                            ];
                            var currentClosureTypeReference = new LoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                    EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0],
                                        currentClosureTypeReference));

                            var otherLocalsType = _types[
                                localVariable.ContainingFunction.LocalsTypeId
                            ];
                            var otherLocalsTypeReference = new LoweredConcreteTypeReference(
                                    otherLocalsType.Name,
                                    otherLocalsType.Id,
                                    []);

                            fieldInitializers.TryAdd(
                                otherLocalsType.Name,
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0,
                                        true,
                                        currentClosureTypeReference),
                                    otherLocalsType.Name,
                                    "_classVariant",
                                    true,
                                    otherLocalsTypeReference));

                            break;
                        }
                        Debug.Assert(_currentFunction.Value.FunctionSignature.LocalsTypeId is not null);
                        var localsType = _types[_currentFunction.Value.FunctionSignature.LocalsTypeId];

                        fieldInitializers.TryAdd(
                            localsType.Name,
                            new LocalVariableAccessor(
                                "__locals",
                                true,
                                new LoweredConcreteTypeReference(
                                    localsType.Name,
                                    localsType.Id,
                                    [])));
                        break;
                    }
                case ThisVariable:
                case FieldVariable:
                    {
                        Debug.Assert(_currentType is not null);

                        if (_currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var currentClosureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                            var currentClosureTypeReference = new LoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    currentClosureTypeReference));

                            fieldInitializers.TryAdd(
                                "this",
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0, true, currentClosureTypeReference),
                                    "this",
                                    "_classVariant",
                                    true,
                                    _currentType));
                            break;
                        }

                        Debug.Assert(
                            EqualTypeReferences(
                                _currentFunction.Value.LoweredMethod.Parameters[0],
                                _currentType));
                        fieldInitializers.TryAdd(
                            "this",
                            new LoadArgumentExpression(
                                0,
                                true,
                                _currentType));
                        break;
                    }
                case FunctionSignatureParameter parameter:
                    {
                        if (parameter.ContainingFunction != _currentFunction.Value.FunctionSignature)
                        {
                            Debug.Assert(parameter.ContainingFunction is not null);
                            Debug.Assert(parameter.ContainingFunction.LocalsTypeId is not null);
                            Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId is not null);

                            var currentClosureType = _types[
                                _currentFunction.Value.FunctionSignature.ClosureTypeId
                            ];
                            var currentClosureTypeReference = new LoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                    EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0],
                                        currentClosureTypeReference));

                            var otherLocalsType = _types[
                                parameter.ContainingFunction.LocalsTypeId
                            ];
                            var otherLocalsTypeReference = new LoweredConcreteTypeReference(
                                    otherLocalsType.Name,
                                    otherLocalsType.Id,
                                    []);

                            fieldInitializers.TryAdd(
                                otherLocalsType.Name,
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0,
                                        true,
                                        currentClosureTypeReference),
                                    otherLocalsType.Name,
                                    "_classVariant",
                                    true,
                                    otherLocalsTypeReference));

                            break;
                        }
                        Debug.Assert(_currentFunction.Value.FunctionSignature.LocalsTypeId is not null);
                        var localsType = _types[_currentFunction.Value.FunctionSignature.LocalsTypeId];

                        fieldInitializers.TryAdd(
                            localsType.Name,
                            new LocalVariableAccessor(
                                "__locals",
                                true,
                                new LoweredConcreteTypeReference(
                                    localsType.Name,
                                    localsType.Id,
                                    [])));
                        break;
                    }
            }
        }

        return new CreateObjectExpression(
                    closureTypeReference,
                    "_classVariant",
                    true,
                    fieldInitializers);
    }

    private MethodCallExpression LowerMethodCallExpression(Expressions.MethodCallExpression e)
    {
        var instantiatedFunction = e.MethodCall.Method switch
        {
            Expressions.MemberAccessExpression { MemberAccess.InstantiatedFunction: var fn } => fn,
            Expressions.StaticMemberAccessExpression { StaticMemberAccess.InstantiatedFunction: var fn } => fn,
            Expressions.ValueAccessorExpression { FunctionInstantiation: var fn } => fn,
            _ => null
        };

        IReadOnlyList<ILoweredExpression> originalArguments = [..e.MethodCall.ArgumentList.Select(LowerExpression)];

        var arguments = new List<ILoweredExpression>(e.MethodCall.ArgumentList.Count);
        LoweredFunctionReference functionReference;

        // calling function object instead of normal function
        if (instantiatedFunction is null)
        {
            var methodExpression = LowerExpression(e.MethodCall.Method);

            var methodType = (methodExpression.ResolvedType as LoweredConcreteTypeReference).NotNull();

            var fn = _importedPrograms.SelectMany(x =>
                x.Methods.Where(y => y.Name == $"Function`{e.MethodCall.ArgumentList.Count + 1}__Call"))
                .First();
            
            functionReference = GetFunctionReference(
                    fn.Id,
                    [],
                    methodType.TypeArguments);

            arguments.Add(LowerExpression(e.MethodCall.Method));
            
            arguments.AddRange(originalArguments);

            return new MethodCallExpression(
                functionReference,
                arguments,
                e.ValueUseful,
                GetTypeReference(e.ResolvedType.NotNull()));
        }
        
        IReadOnlyList<ILoweredTypeReference> ownerTypeArguments = [];
        if (e.MethodCall.Method is Expressions.MemberAccessExpression memberAccess)
        {
            var owner = LowerExpression(memberAccess.MemberAccess.Owner);
            arguments.Add(owner);
            ownerTypeArguments = owner.ResolvedType is LoweredConcreteTypeReference concrete
                ? concrete.TypeArguments
                : throw new UnreachableException("Shouldn't ever be able to call a method on a generic parameter");
        }
        else if (instantiatedFunction.ClosureTypeId is not null)
        {
            var createClosure = CreateClosureObject(instantiatedFunction);
            arguments.Add(createClosure);
        }
        else if (instantiatedFunction is { IsStatic: false, OwnerType: not null }
                 && _currentType is not null
                 && EqualTypeReferences(GetTypeReference(instantiatedFunction.OwnerType), _currentType)
                 && _currentFunction is not null
                 && EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0], _currentType))
        {
            arguments.Add(
                    new LoadArgumentExpression(0, true, _currentType));
        }

        if (e.MethodCall.Method is Expressions.StaticMemberAccessExpression staticMemberAccess)
        {
            ownerTypeArguments = (GetTypeReference(staticMemberAccess.OwnerType.NotNull())
                as LoweredConcreteTypeReference).NotNull().TypeArguments;
        }
        else if (e.MethodCall.Method is Expressions.ValueAccessorExpression valueAccessor)
        {
            if (_currentType is not null)
            {
                ownerTypeArguments = _currentType.TypeArguments;
            }
            else if (valueAccessor.FunctionInstantiation.NotNull()
                    .OwnerType is {} ownerType)
            {
                var ownerTypeReference = GetTypeReference(ownerType);
                if (ownerTypeReference is LoweredConcreteTypeReference
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

        return new MethodCallExpression(
                functionReference,
                arguments,
                e.ValueUseful,
                GetTypeReference(e.ResolvedType.NotNull()));
    }

    private ILoweredExpression LowerMemberAccessExpression(
            Expressions.MemberAccessExpression e)
    {
        var owner = LowerExpression(e.MemberAccess.Owner);
        switch (e.MemberAccess.MemberType.NotNull())
        {
            case Expressions.MemberType.Field:
                {
                    // todo: assert we're in a class variant
                    return new FieldAccessExpression(
                            owner,
                            e.MemberAccess.MemberName.NotNull().StringValue,
                            "_classVariant",
                            e.ValueUseful,
                            GetTypeReference(e.ResolvedType.NotNull()));
                }
            case Expressions.MemberType.Function:
                {
                    var ownerTypeArguments = (owner.ResolvedType as LoweredConcreteTypeReference)
                        .NotNull().TypeArguments;

                    var fn = e.MemberAccess.InstantiatedFunction.NotNull();
                    return new CreateObjectExpression(
                        (GetTypeReference(e.ResolvedType.NotNull()) as LoweredConcreteTypeReference).NotNull(),
                        "_classVariant",
                        e.ValueUseful,
                        new()
                        {
                            {
                                "FunctionReference",
                                new FunctionReferenceConstantExpression(
                                    GetFunctionReference(
                                        fn.FunctionId,
                                        [..fn.TypeArguments.Select(GetTypeReference)],
                                        ownerTypeArguments),
                                    true,
                                    new LoweredFunctionPointer(
                                        [..fn.Parameters.Select(x => GetTypeReference(x.Type))],
                                        GetTypeReference(fn.ReturnType)))
                            },
                            {
                                "FunctionParameter",
                                owner
                            }
                        });
                }
            case Expressions.MemberType.Variant:
                throw new InvalidOperationException("Can never access a variant through instance member access");
            default:
                throw new UnreachableException($"{e.MemberAccess.MemberType}");
        }
    }

    private BlockExpression LowerBlockExpression(Expressions.BlockExpression e)
    {
        return new BlockExpression(
                [..e.Block.Expressions.Select(LowerExpression)],
                GetTypeReference(e.ResolvedType.NotNull()),
                e.ValueUseful);
    }

    private CreateObjectExpression LowerUnionClassVariantInitializerExpression(
            Expressions.UnionClassVariantInitializerExpression e)
    {
        var type = GetTypeReference(e.ResolvedType.NotNull());
        if (type is not LoweredConcreteTypeReference concreteTypeReference)
        {
            throw new UnreachableException();
        }

        var dataType = _types[concreteTypeReference.DefinitionId];

        var variantIdentifier = dataType.Variants.Index()
            .First(x => x.Item.Name == e.UnionInitializer.VariantIdentifier.StringValue).Index;

        var fieldInitializers = e.UnionInitializer.FieldInitializers.ToDictionary(
                x => x.FieldName.StringValue,
                x => LowerExpression(x.Value.NotNull()));

        fieldInitializers["_variantIdentifier"] = new UInt16ConstantExpression(
                ValueUseful: true,
                (ushort)variantIdentifier);

        return new(
                concreteTypeReference,
                e.UnionInitializer.VariantIdentifier.StringValue,
                e.ValueUseful,
                fieldInitializers);
    }

    private ILoweredExpression LowerStaticMemberAccess(
            Expressions.StaticMemberAccessExpression e)
    {
        if (e.StaticMemberAccess.MemberType == Expressions.MemberType.Variant)
        {
            var unionType = GetTypeReference(e.OwnerType.NotNull())
                as LoweredConcreteTypeReference ?? throw new UnreachableException();

            var dataType = _types[unionType.DefinitionId];
            var variantName = e.StaticMemberAccess.MemberName.NotNull().StringValue;
            var (variantIdentifier, variant) = dataType.Variants.Index()
                .First(x => x.Item.Name == variantName);

            if (variant.Fields.Any(x => x.Name != "_variantIdentifier"))
            {
                // we're statically accessing this variant, and there's at least one field. It must be a tuple variant
                // because you can't access a class variant directly outside creating it. We're returning a 
                // function object for this tuple create function

                if (e.ResolvedType is not FunctionObject functionObject)
                {
                    throw new InvalidOperationException($"Expected a function object, got a {e.ResolvedType?.GetType()}");
                }

                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();
                
                var method = _methods.Keys.First(x => x.Id == fn.FunctionId);
                
                var functionObjectParameters = new Dictionary<string, ILoweredExpression>
                {
                    {
                        "FunctionReference",
                        new FunctionReferenceConstantExpression(
                            GetFunctionReference(
                                fn.FunctionId,
                                [],
                               unionType.TypeArguments),
                            true,
                            new LoweredFunctionPointer(
                                method.Parameters,
                                method.ReturnType))
                    }
                };

                return new CreateObjectExpression(
                    (GetTypeReference(functionObject) as LoweredConcreteTypeReference).NotNull(),
                    "_classVariant",
                    e.ValueUseful,
                    functionObjectParameters);               
            }

            var fieldInitializers = new Dictionary<string, ILoweredExpression>()
            {
                {
                    "_variantIdentifier",
                    new UInt16ConstantExpression(
                        ValueUseful: true,
                        (ushort)variantIdentifier)
                }
            };

            return new CreateObjectExpression(
                    unionType,
                    variantName,
                    e.ValueUseful,
                    fieldInitializers);
        }

        if (e.StaticMemberAccess.MemberType == Expressions.MemberType.Function)
        {
            var ownerTypeArguments = (GetTypeReference(e.OwnerType.NotNull()) as LoweredConcreteTypeReference).NotNull().TypeArguments;
            var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();
            return new CreateObjectExpression(
                (GetTypeReference(e.ResolvedType.NotNull()) as LoweredConcreteTypeReference).NotNull(),
                "_classVariant",
                e.ValueUseful,
                new()
                {
                    {
                        "FunctionReference",
                        new FunctionReferenceConstantExpression(
                            GetFunctionReference(
                                fn.FunctionId,
                                [..fn.TypeArguments.Select(GetTypeReference)],
                                ownerTypeArguments),
                            true,
                            new LoweredFunctionPointer(
                                [..fn.Parameters.Select(x => GetTypeReference(x.Type))],
                                GetTypeReference(fn.ReturnType)))
                    }
                });
        }

        if (e.StaticMemberAccess.MemberType == Expressions.MemberType.Field)
        {
            return new StaticFieldAccessExpression(
                (GetTypeReference(e.OwnerType.NotNull()) as LoweredConcreteTypeReference).NotNull(),
                e.StaticMemberAccess.MemberName.NotNull().StringValue,
                e.ValueUseful,
                GetTypeReference(e.ResolvedType.NotNull()));
        }

        throw new UnreachableException();
    }

    private CreateObjectExpression LowerObjectInitializationExpression(
            Expressions.ObjectInitializerExpression e)
    {
        var type = GetTypeReference(e.ResolvedType.NotNull());
        if (type is not LoweredConcreteTypeReference concreteTypeReference)
        {
            throw new UnreachableException();
        }

        var fieldInitializers = e.ObjectInitializer.FieldInitializers.ToDictionary(
                x => x.FieldName.StringValue,
                x => LowerExpression(x.Value.NotNull()));
        
        return new(concreteTypeReference,
                "_classVariant",
                e.ValueUseful,
                fieldInitializers);
    }

    private ILoweredExpression LowerVariableDeclarationExpression(Expressions.VariableDeclarationExpression e)
    {
        var variableName = e.VariableDeclaration.Variable.NotNull()
            .Name.StringValue;

        var referencedInClosure = e.VariableDeclaration.Variable!.ReferencedInClosure;

        if (e.VariableDeclaration.Value is null)
        {
            return new VariableDeclarationExpression(variableName, e.ValueUseful);
        }

        var loweredValue = LowerExpression(e.VariableDeclaration.Value);

        if (!referencedInClosure)
        {
            return new VariableDeclarationAndAssignmentExpression(
                    variableName,
                    loweredValue,
                    e.ValueUseful);
        }

        var localsTypeId = _currentFunction.NotNull().FunctionSignature.LocalsTypeId
            .NotNull();
        var localsType = _types[localsTypeId];
        
        var localsFieldAssignment = new FieldAssignmentExpression(
            new LocalVariableAccessor("__locals",
                true,
                new LoweredConcreteTypeReference(
                    localsType.Name,
                    localsType.Id,
                    [])),
            "_classVariant",
            variableName,
            loweredValue,
            // hard code this to false, because either `e.ValueUseful` was false,
            // or we're going to replace the value with a block
            false,
            loweredValue.ResolvedType);

        if (e.ValueUseful)
        {
            // because the value of this variable declaration expression is useful
            // (ie used in the parent expression), and we have just changed what that
            // resulting value would be (value declarations return unit, but field
            // assignments return the assigned value), we need to stick this assignment
            // in a block and put a unit constant back

            // I'm not sure if this is going to bite me in the butt because of any specific
            // block semantics (ie dropping values at the end of a block)
            var unit = new UnitConstantExpression(ValueUseful: true);
            return new BlockExpression(
                [
                    localsFieldAssignment,
                    unit
                ],
                unit.ResolvedType,
                ValueUseful: true);
        }

        return localsFieldAssignment;
    }

    private ILoweredExpression LowerUnaryOperatorExpression(
            Expressions.UnaryOperatorExpression e)
    {
        var operand = LowerExpression(e.UnaryOperator.Operand.NotNull());
        return e.UnaryOperator.OperatorType switch
        {
            Expressions.UnaryOperatorType.FallOut => LowerFallout(
                    LowerExpression(e.UnaryOperator.Operand.NotNull()),
                    e.ValueUseful,
                    GetTypeReference(e.ResolvedType.NotNull())),
            Expressions.UnaryOperatorType.Not => new BoolNotExpression(e.ValueUseful, operand),
            _ => throw new UnreachableException(),
        };
    }

    private BlockExpression LowerFallout(
        ILoweredExpression operand,
        bool valueUseful,
        ILoweredTypeReference resolvedType)
    {
        var okVariant = UnionSignature.Result
            .Variants
            .First(x => x.Name == "Ok");
        var errorVariant = (UnionSignature.Result
            .Variants
            .First(x => x.Name == "Error")
            as TypeChecking.TypeChecker.TupleUnionVariant).NotNull();

        Debug.Assert(_currentFunction.HasValue);
        var locals = _currentFunction.Value.LoweredMethod.Locals;
        var localName = $"Local{locals.Count}";
        locals.Add(new MethodLocal(localName, operand.ResolvedType));

        var returnType = (GetTypeReference(_currentFunction.Value.FunctionSignature.ReturnType)
            as LoweredConcreteTypeReference).NotNull();
        Debug.Assert(returnType.DefinitionId == UnionSignature.Result.Id);

        /*
         * var a = b()?;
         * // turns into:
         * var _tempLocal;
         * var a = {
         *   _tempLocal = b();
         *   if (_tempLocal._variantIdentifier == OkVariantIdentifier)
         *   {
         *     (_tempLocal as result::ok).Item0
         *   }
         *   else
         *   {
         *     return err((_tempLocal as result::error).Item0);
         *   }
         * }
         */

        return new BlockExpression(
            [
                new LocalAssignmentExpression(
                    localName,
                    operand,
                    operand.ResolvedType,
                    false),
                new SwitchIntExpression(
                    new FieldAccessExpression(
                        new LocalVariableAccessor(
                            localName, true, operand.ResolvedType),
                        "_variantIdentifier",
                        "Ok",
                        true,
                        new LoweredConcreteTypeReference(
                            ClassSignature.Int64.Name,
                            ClassSignature.Int64.Id,
                            [])),
                    new()
                    {
                        {
                            0, 
                            new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    localName, true, operand.ResolvedType),
                                "Item0",
                                okVariant.Name,
                                true,
                                resolvedType)
                        }
                    },
                    new MethodReturnExpression(
                        new MethodCallExpression(
                            GetFunctionReference(errorVariant.CreateFunction.Id, [], returnType.TypeArguments),
                            [
                                new FieldAccessExpression(
                                    new LocalVariableAccessor(
                                        localName, true, operand.ResolvedType),
                                    "Item0",
                                    errorVariant.Name,
                                    true,
                                    returnType.TypeArguments[1])
                            ],
                            true,
                            returnType)),
                    valueUseful,
                    resolvedType)
            ],
            resolvedType,
            valueUseful);
    }

    private ILoweredExpression LowerValueAccessorExpression(
            Expressions.ValueAccessorExpression e)
    {

        return e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new StringConstantExpression(e.ValueUseful, stringLiteral),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }} => new Int64ConstantExpression(e.ValueUseful, intValue),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.True }} => new BoolConstantExpression(e.ValueUseful, true),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.False }} => new BoolConstantExpression(e.ValueUseful, false),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, ReferencedVariable: {} variable} => VariableAccess(variable, e.ValueUseful),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, FunctionInstantiation: {} fn} => FunctionAccess(fn, (e.ResolvedType as FunctionObject).NotNull(), e.ValueUseful),
            _ => throw new UnreachableException($"{e}")
        };

        ILoweredExpression FunctionAccess(
                InstantiatedFunction fn,
                FunctionObject typeReference,
                bool valueUseful)
        {
            var method = _methods.Keys.First(x => x.Id == fn.FunctionId);

            var ownerTypeArguments = _currentType?.TypeArguments ?? [];

            var functionObjectParameters = new Dictionary<string, ILoweredExpression>
            {
                {
                    "FunctionReference",
                    new FunctionReferenceConstantExpression(
                            GetFunctionReference(
                                fn.FunctionId,
                                [..fn.TypeArguments.Select(GetTypeReference)],
                                ownerTypeArguments),
                            true,
                            new LoweredFunctionPointer(
                                method.Parameters,
                                method.ReturnType))
                }
            };

            if (fn.ClosureTypeId is not null)
            {
                functionObjectParameters.Add("FunctionParameter", CreateClosureObject(fn));
            }
            else if (fn is { IsStatic: false, OwnerType: not null }
                     && _currentType is not null
                     && EqualTypeReferences(GetTypeReference(fn.OwnerType), _currentType)
                     && _currentFunction is not null
                     && EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0], _currentType))
            {
                functionObjectParameters.Add("FunctionParameter", new LoadArgumentExpression(0, true, _currentType));
            }

            return new CreateObjectExpression(
                (GetTypeReference(typeReference) as LoweredConcreteTypeReference).NotNull(),
                "_classVariant",
                valueUseful,
                functionObjectParameters);
        }

        ILoweredExpression VariableAccess(
                IVariable variable,
                bool valueUseful)
        {
            var resolvedType = GetTypeReference(e.ResolvedType.NotNull());
            switch (variable)
            {
                case LocalVariable localVariable:
                    {
                        if (!localVariable.ReferencedInClosure)
                        {
                            return new LocalVariableAccessor(
                                    variable.Name.StringValue,
                                    valueUseful,
                                    resolvedType);
                        }

                        var currentFunction = _currentFunction.NotNull();
                        var containingFunction = localVariable.ContainingFunction.NotNull();
                        var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                        var localsTypeReference = new LoweredConcreteTypeReference(
                                        containingFunctionLocals.Name,
                                        containingFunctionLocals.Id,
                                        []);
                        if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                        {
                            return new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    "__locals",
                                    true,
                                    localsTypeReference),
                                localVariable.Name.StringValue,
                                "_classVariant",
                                e.ValueUseful,
                                resolvedType);
                        }
                        var closureTypeId = _currentFunction.NotNull()
                                .FunctionSignature.ClosureTypeId.NotNull();
                        var closureType = _types[closureTypeId];

                        return new FieldAccessExpression(
                            new FieldAccessExpression(
                                new LoadArgumentExpression(
                                    0,
                                    true,
                                    new LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureTypeId,
                                        [])),
                                containingFunctionLocals.Name,
                                "_classVariant",
                                true,
                                localsTypeReference),
                            localVariable.Name.StringValue,
                            "_classVariant",
                            e.ValueUseful,
                            resolvedType);
                    }
                case ThisVariable thisVariable:
                    {
                        Debug.Assert(_currentFunction is not null);
                        Debug.Assert(_currentType is not null); 
                        if (thisVariable.ReferencedInClosure
                                && _currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                            var closureTypeReference = new LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureType.Id,
                                        []);
                            Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    closureTypeReference));
                            return new FieldAccessExpression(
                                new LoadArgumentExpression(
                                    0,
                                    true,
                                    closureTypeReference),
                                "this",
                                "_classVariant",
                                valueUseful,
                                resolvedType);
                        }

                        Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                        Debug.Assert(EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    _currentType));

                        return new LoadArgumentExpression(
                                0, valueUseful, resolvedType);
                    }
                case FieldVariable fieldVariable
                    when fieldVariable.ContainingSignature.Id == _currentType?.DefinitionId
                        && _currentFunction is not null:
                    {
                        if (fieldVariable.IsStaticField)
                        {
                            return new StaticFieldAccessExpression(
                                    _currentType,
                                    fieldVariable.Name.StringValue,
                                    valueUseful,
                                    resolvedType);
                        }

                        if (_currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var loweredMethod = _currentFunction.Value.LoweredMethod;
                            var fnSignature = _currentFunction.Value.FunctionSignature;
                            var closureType = _types[fnSignature.ClosureTypeId];
                            var closureTypeReference = new LoweredConcreteTypeReference(closureType.Name, closureType.Id, []);

                            // we're a closure, so reference the value through the "this" field
                            // of the closure type
                            Debug.Assert(loweredMethod.Parameters.Count > 0);
                            Debug.Assert(
                                    EqualTypeReferences(
                                        loweredMethod.Parameters[0],
                                        closureTypeReference));
                            return new FieldAccessExpression(
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0,
                                        true,
                                        closureTypeReference),
                                    "this",
                                    "_classVariant",
                                    true,
                                    _currentType),
                                fieldVariable.Name.StringValue,
                                "_classVariant",
                                valueUseful,
                                resolvedType);
                        }

                        if (_currentFunction.Value.LoweredMethod.Parameters.Count == 0
                                || !EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    _currentType))
                        {
                            throw new InvalidOperationException("Expected to be in instance function");
                        }

                        // todo: assert we're in a class and have _classVariant

                        return new FieldAccessExpression(
                            new LoadArgumentExpression(
                                0,
                                true,
                                _currentType),
                            fieldVariable.Name.StringValue,
                            "_classVariant",
                            valueUseful,
                            resolvedType);
                    }
                case FunctionSignatureParameter argument:
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

                            return new LoadArgumentExpression(argumentIndex, valueUseful, resolvedType);
                        }

                        var currentFunction = _currentFunction.NotNull();
                        var containingFunction = argument.ContainingFunction.NotNull();
                        var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                        var localsTypeReference = new LoweredConcreteTypeReference(
                                        containingFunctionLocals.Name,
                                        containingFunctionLocals.Id,
                                        []);
                        if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                        {
                            return new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    "__locals",
                                    true,
                                    localsTypeReference),
                                argument.Name.StringValue,
                                "_classVariant",
                                e.ValueUseful,
                                resolvedType);
                        }
                        var closureTypeId = _currentFunction.NotNull()
                                .FunctionSignature.ClosureTypeId.NotNull();
                        var closureType = _types[closureTypeId];

                        return new FieldAccessExpression(
                            new FieldAccessExpression(
                                new LoadArgumentExpression(
                                    0,
                                    true,
                                    new LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureTypeId,
                                        [])),
                                containingFunctionLocals.Name,
                                "_classVariant",
                                true,
                                localsTypeReference),
                            argument.Name.StringValue,
                            "_classVariant",
                            e.ValueUseful,
                            resolvedType);
                    }
            }

            throw new UnreachableException($"{variable.GetType()}");
        }
    }

    private ILoweredExpression LowerBinaryOperatorExpression(
            Expressions.BinaryOperatorExpression e)
    {
        if (e.BinaryOperator.OperatorType == Expressions.BinaryOperatorType.ValueAssignment)
        {
            return LowerValueAssignment(
                    e.BinaryOperator.Left.NotNull(),
                    e.BinaryOperator.Right.NotNull(),
                    e.ValueUseful,
                    GetTypeReference(e.ResolvedType.NotNull()));
        }

        var left = LowerExpression(e.BinaryOperator.Left.NotNull());
        var right = LowerExpression(e.BinaryOperator.Right.NotNull());

        var leftType = (left.ResolvedType as LoweredConcreteTypeReference).NotNull();

        return e.BinaryOperator.OperatorType switch
        {
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.Int64
                => new Int64LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.Int32
                => new Int32LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.Int16
                => new Int16LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.Int8
                => new Int8LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.UInt64
                => new UInt64LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.UInt32
                => new UInt32LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.UInt16
                => new UInt16LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.LessThan when leftType.DefinitionId == DefId.UInt8
                => new UInt8LessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.Int64
                => new Int64GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.Int32
                => new Int32GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.Int16
                => new Int16GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.Int8
                => new Int8GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.UInt64
                => new UInt64GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.UInt32
                => new UInt32GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.UInt16
                => new UInt16GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan when leftType.DefinitionId == DefId.UInt8
                => new UInt8GreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.Int64
                => new Int64PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.Int32
                => new Int32PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.Int16
                => new Int16PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.Int8
                => new Int8PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.UInt64
                => new UInt64PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.UInt32
                => new UInt32PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.UInt16
                => new UInt16PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus when leftType.DefinitionId == DefId.UInt8
                => new UInt8PlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.Int64
                => new Int64MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.Int32
                => new Int32MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.Int16
                => new Int16MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.Int8
                => new Int8MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.UInt64
                => new UInt64MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.UInt32
                => new UInt32MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.UInt16
                => new UInt16MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus when leftType.DefinitionId == DefId.UInt8
                => new UInt8MinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.Int64
                => new Int64MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.Int32
                => new Int32MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.Int16
                => new Int16MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.Int8
                => new Int8MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.UInt64
                => new UInt64MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.UInt32
                => new UInt32MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.UInt16
                => new UInt16MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply when leftType.DefinitionId == DefId.UInt8
                => new UInt8MultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.Int64
                => new Int64DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.Int32
                => new Int32DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.Int16
                => new Int16DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.Int8
                => new Int8DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.UInt64
                => new UInt64DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.UInt32
                => new UInt32DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.UInt16
                => new UInt16DivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide when leftType.DefinitionId == DefId.UInt8
                => new UInt8DivideExpression(e.ValueUseful, left, right),
                // todo: handle more types of equality checks 
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.Int64
                => new Int64EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.Int32
                => new Int32EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.Int16
                => new Int16EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.Int8
                => new Int8EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.UInt64
                => new UInt64EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.UInt32
                => new UInt32EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.UInt16
                => new UInt16EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.UInt8
                => new UInt8EqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck when leftType.DefinitionId == DefId.Boolean
                => new BoolEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.Int64
                => new Int64NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.Int32
                => new Int32NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.Int16
                => new Int16NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.Int8
                => new Int8NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.UInt64
                => new UInt64NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.UInt32
                => new UInt32NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.UInt16
                => new UInt16NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.UInt8
                => new UInt8NotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.NegativeEqualityCheck when leftType.DefinitionId == DefId.Boolean
                => new BoolNotEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.BooleanAnd
                => new BoolAndExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.BooleanOr
                => new BoolOrExpression(e.ValueUseful, left, right),
            _ => throw new InvalidOperationException($"Invalid binary operator {e.BinaryOperator.OperatorType}"),
        };
    }

    private ILoweredExpression LowerValueAssignment(
            Expressions.IExpression left,
            Expressions.IExpression right,
            bool valueUseful,
            ILoweredTypeReference resolvedType)
    {
        if (left is Expressions.ValueAccessorExpression valueAccessor)
        {
            var variable = valueAccessor.ReferencedVariable.NotNull();
            if (variable is LocalVariable localVariable)
            {
                if (localVariable.ReferencedInClosure)
                {
                    var containingFunction = localVariable.ContainingFunction;
                    Debug.Assert(_currentFunction.HasValue);
                    Debug.Assert(containingFunction is not null);
                    Debug.Assert(containingFunction.LocalsTypeId is not null);
                    var localsType = _types[containingFunction.LocalsTypeId];
                    var localsTypeReference = new LoweredConcreteTypeReference(
                        localsType.Name,
                        localsType.Id,
                        []);

                    if (_currentFunction.Value.FunctionSignature == containingFunction)
                    {
                        return new FieldAssignmentExpression(
                            new LocalVariableAccessor(
                                "__locals",
                                true,
                                localsTypeReference),
                            "_classVariant",
                            localVariable.Name.StringValue,
                            LowerExpression(right),
                            valueUseful,
                            resolvedType);
                    }

                    Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId is not null);
                    var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                    var closureTypeReference = new LoweredConcreteTypeReference(
                            closureType.Name,
                            closureType.Id,
                            []);

                    Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                    Debug.Assert(EqualTypeReferences(
                            closureTypeReference,
                            _currentFunction.Value.LoweredMethod.Parameters[0]));

                    return new FieldAssignmentExpression(
                        new FieldAccessExpression(
                            new LoadArgumentExpression(
                                0, true, closureTypeReference),
                            localsType.Name,
                            "_classVariant",
                            true,
                            localsTypeReference),
                        "_classVariant",
                        localVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                return new LocalAssignmentExpression(
                        localVariable.Name.StringValue,
                        LowerExpression(right),
                        resolvedType,
                        valueUseful);
            }

            if (variable is FieldVariable fieldVariable)
            {
                Debug.Assert(_currentType is not null);
                if (fieldVariable.IsStaticField)
                {
                    return new StaticFieldAssignmentExpression(
                        _currentType,
                        fieldVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                if (fieldVariable.ReferencedInClosure
                    && _currentFunction is
                    {
                        FunctionSignature: { ClosureTypeId: not null} functionSignature
                    })
                {
                    var closureType = _types[functionSignature.ClosureTypeId];
                    var closureTypeReference = new LoweredConcreteTypeReference(
                            closureType.Name,
                            closureType.Id,
                            []);

                    return new FieldAssignmentExpression(
                        new FieldAccessExpression(
                            new LoadArgumentExpression(
                                0,
                                true,
                                closureTypeReference),
                            "this",
                            "_classVariant",
                            true,
                            _currentType),
                        "_classVariant",
                        fieldVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                Debug.Assert(fieldVariable.ContainingSignature.Id == _currentType.DefinitionId);

                

                Debug.Assert(_currentFunction is not null);
                Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                Debug.Assert(EqualTypeReferences(
                            _currentFunction.Value.LoweredMethod.Parameters[0],
                            _currentType));

                return new FieldAssignmentExpression(
                    new LoadArgumentExpression(0, true, _currentType),
                    "_classVariant",
                    fieldVariable.Name.StringValue,
                    LowerExpression(right),
                    valueUseful,
                    resolvedType);
            }

            throw new UnreachableException(variable.ToString());
        }

        if (left is Expressions.MemberAccessExpression memberAccess)
        {
            var memberOwner = LowerExpression(memberAccess.MemberAccess.Owner);

            return new FieldAssignmentExpression(
                memberOwner,
                "_classVariant",
                memberAccess.MemberAccess.MemberName.NotNull().StringValue,
                LowerExpression(right),
                valueUseful,
                resolvedType);
        }

        if (left is Expressions.StaticMemberAccessExpression staticMemberAccess)
        {
            if (GetTypeReference(staticMemberAccess.OwnerType.NotNull())
                    is not LoweredConcreteTypeReference concreteType)
            {
                throw new InvalidOperationException("Expected type to be concrete");
            }

            return new StaticFieldAssignmentExpression(
                concreteType,
                staticMemberAccess.StaticMemberAccess.MemberName.NotNull().StringValue,
                LowerExpression(right),
                valueUseful,
                resolvedType);
        }

        throw new UnreachableException();
    }
}
