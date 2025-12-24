using System.Diagnostics;
using Reef.Core.Expressions;
using Reef.Core.LoweredExpressions;
using MethodCall = Reef.Core.LoweredExpressions.MethodCall;
using TypeChecker = Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    private uint _controlFlowDepth;
    private readonly Stack<LoopBasicBlocks> _loopBasicBlocksStack = [];

    private record LoopBasicBlocks(BasicBlockId Beginning, BasicBlockId After);

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
        Debug.Assert(_basicBlocks[^1].Terminator is null);
        return expression switch
        {
            BinaryOperatorExpression binaryOperatorExpression => LowerBinaryExpression(
                binaryOperatorExpression, destination),
            BlockExpression blockExpression => LowerBlock(blockExpression, destination),
            WhileExpression whileExpression => LowerWhile(whileExpression),
            BreakExpression => LowerBreak(),
            ContinueExpression => LowerContinue(),
            IfExpressionExpression ifExpressionExpression => LowerIf(ifExpressionExpression, destination),
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

    private IExpressionResult LowerContinue()
    {
        var loopBasicBlocks = _loopBasicBlocksStack.Peek();
        _basicBlocks[^1].Terminator = new GoTo(loopBasicBlocks.Beginning);
        GetNextEmptyBasicBlock();
        
        return new OperandResult(new UnitConstant());
    }

    private IExpressionResult LowerBreak()
    {
        var loopBasicBlocks = _loopBasicBlocksStack.Peek();
        _basicBlocks[^1].Terminator = new GoTo(loopBasicBlocks.After);
        GetNextEmptyBasicBlock();
        
        return new OperandResult(new UnitConstant());
    }

    private IExpressionResult LowerWhile(WhileExpression expression)
    {
        var beginningBasicBlockId = GetNextEmptyBasicBlock();
        var afterBasicBlockId = new BasicBlockId("after");
        var bodyBasicBlockId = new BasicBlockId("body");
        
        _controlFlowDepth++;
        _loopBasicBlocksStack.Push(new LoopBasicBlocks(beginningBasicBlockId, afterBasicBlockId));
        
        var checkValue = NewLowerExpression(expression.Check.NotNull(), null);

        _basicBlocks[^1].Terminator = new SwitchInt(
            checkValue.ToOperand(),
            new Dictionary<int, BasicBlockId>
            {
                { 0, afterBasicBlockId }
            },
            bodyBasicBlockId);

        bodyBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
        NewLowerExpression(expression.Body.NotNull(), null);

        _basicBlocks[^1].Terminator = new GoTo(beginningBasicBlockId);

        afterBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

        _controlFlowDepth--;
        _loopBasicBlocksStack.Pop();
        return new OperandResult(new UnitConstant());
    }

    private IExpressionResult LowerIf(IfExpressionExpression expression, IPlace? destination)
    {
        if (destination is null && expression.ValueUseful)
        {
            var localName = LocalName((uint)_locals.Count);
            _locals.Add(new MethodLocal(localName, null, GetTypeReference(expression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }
        
        var valueOperand = NewLowerExpression(expression.IfExpression.CheckExpression, null).ToOperand();

        _controlFlowDepth++;

        var bodyBasicBlockId = new BasicBlockId("body");
        var afterBasicBlockId = new BasicBlockId("after");
        var elseBasicBlockId = expression.IfExpression.ElseBody is null ? afterBasicBlockId : new BasicBlockId("else");
        var elseIfBasicBlockIds = Enumerable.Range(0, expression.IfExpression.ElseIfs.Count).Select(x => new BasicBlockId($"elseIf{x}")).ToArray();
        
        _basicBlocks[^1].Terminator = new SwitchInt(
            valueOperand,
            new Dictionary<int, BasicBlockId>
            {
                { 0, elseIfBasicBlockIds.FirstOrDefault() ?? elseBasicBlockId }
            },
            bodyBasicBlockId);

        bodyBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
        NewLowerExpression(expression.IfExpression.Body.NotNull(), destination);

        foreach (var (i, elseIf) in expression.IfExpression.ElseIfs.Index())
        {
            _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);
            var basicBlockId = elseIfBasicBlockIds[i];
            basicBlockId.Id = GetNextEmptyBasicBlock().Id;

            var elseIfCheck = NewLowerExpression(elseIf.CheckExpression, null);
            
            var elseIfBodyBasicBlockId = new BasicBlockId("elseIfBody");

            _basicBlocks[^1].Terminator = new SwitchInt(
                elseIfCheck.ToOperand(),
                new Dictionary<int, BasicBlockId>
                {
                    { 0, i < elseIfBasicBlockIds.Length - 1 ? elseIfBasicBlockIds[i + 1] : elseBasicBlockId }
                },
                elseIfBodyBasicBlockId);

            elseIfBodyBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

            NewLowerExpression(elseIf.Body.NotNull(), destination);
        }

        if (expression.IfExpression.ElseBody is not null)
        {
            _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);
            elseBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

            NewLowerExpression(expression.IfExpression.ElseBody, destination);
        }

        afterBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

        _controlFlowDepth--;

        return destination is null ? new OperandResult(new UnitConstant()) : new PlaceResult(destination);
    }

    private BasicBlockId GetNextEmptyBasicBlock()
    {
        if (_basicBlocks[^1] is {Statements.Count: 0, Terminator: null, Id: var id})
        {
            return id;
        }

        _basicBlockStatements = [];
        id = new BasicBlockId($"bb{_basicBlocks.Count}");
        _basicBlocks.Add(new BasicBlock(id, _basicBlockStatements));

        return id;
    }
    
    private IExpressionResult LowerMatchPatterns(
            List<(IPattern Pattern, IExpression Expression)> patterns,
            IExpressionResult accessExpression,
            ILoweredTypeReference valueType,
            IPlace? destination,
            BasicBlockId afterBasicBlockId)
    {
        var tree = BuildMatchTree(patterns, accessExpression, valueType);

        LowerMatchTree(tree, destination, afterBasicBlockId, afterBasicBlockId);

        return destination is not null ? new PlaceResult(destination) : new OperandResult(new UnitConstant());
    }

    private void LowerMatchTree(
        List<INode> tree,
        IPlace? destination,
        BasicBlockId otherwiseBasicBlockId,
        BasicBlockId afterBasicBlockId)
    {
        Debug.Assert(tree.Count > 0);

        var typeNodes = new List<TypeNode>();
        var discardNodes = new List<DiscardNode>();
        var variantNodes = new List<VariantNode>();
        
        foreach (var node in tree)
        {
            switch (node)
            {
                case DiscardNode discardNode:
                {
                    discardNodes.Add(discardNode);
                    break;
                }
                case TypeNode typeNode:
                {
                    typeNodes.Add(typeNode);
                    break;
                }
                case VariantNode variantNode:
                {
                    variantNodes.Add(variantNode);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }
        
        Debug.Assert(discardNodes.Count <= 1);

        if (discardNodes.Count == 1)
        {
            var discardNode = discardNodes[0];
            if (variantNodes.Count == 0 && typeNodes.Count == 0)
            {
                foreach (var (variable, accessor) in GetAncestorVariableAssignments(discardNode))
                {
                    _basicBlockStatements.Add(
                        new Assign(GetLocalVariablePlace(variable), new Use(accessor.ToOperand())));
                }
                
                NewLowerExpression(discardNode.Expression.NotNull(), destination);
                return;
            }

            otherwiseBasicBlockId = new BasicBlockId("otherwise");
            
        }

        if (variantNodes.Count > 0)
        {
            var isBoxed = IsTypeReferenceBoxed(variantNodes[0].ExpressionType);
            var type = variantNodes[0].Type;
            var accessor = variantNodes[0].Accessor;
            IPlace accessorPlace;
            if (accessor is PlaceResult(var placeValue))
            {
                accessorPlace = placeValue;
            }
            else if (accessor is OperandResult)
            {
                accessorPlace = ExpressionResultIntoPlace(accessor, type);
            }
            else
            {
                throw new UnreachableException();
            }

            if (isBoxed && accessorPlace is not Deref)
            {
                accessorPlace = new Deref(accessorPlace);
            }
            
            // if we have variant nodes, then all the nodes must be variant nodes (except for any discard nodes), and they must all be for the same type
            Debug.Assert(typeNodes.Count == 0);
            Debug.Assert(variantNodes.Skip(1).All(x => EqualTypeReferences(x.Type, type) && x.Accessor.Equals(accessor)));

            var dataType = GetDataType(type.DefinitionId);
            var caseResults = new Dictionary<int, BasicBlockId>();
            var lastBasicBlock = _basicBlocks[^1];
            lastBasicBlock.Terminator = new SwitchInt(
                new Copy(new Field(accessorPlace, VariantIdentifierFieldName, dataType.Variants[0].Name)),
                caseResults,
                otherwiseBasicBlockId);
            
            foreach (var variantNode in variantNodes)
            {
                _basicBlocks[^1].Terminator ??= new GoTo(afterBasicBlockId);

                var basicBlockId = GetNextEmptyBasicBlock();

                var variantIndex = dataType.Variants.Index().First(x => x.Item.Name == variantNode.VariantName).Index;
                caseResults[variantIndex] = basicBlockId;

                if (variantNode.Expression is not null)
                {
                    /*
                     if there is an expression and also branches, then the expression is treated as the otherwise case.
                     This is based on the assumption that redundant patterns are not processed here.
                     eg:
                     match (a) {
                        MyUnion::A(int) => 1,
                        MyUnion::A => 2
                     }
                     This will end up with like 
                     TypeNode {
                        type: MyUnion,
                        branches: [
                            VariantNode {
                                variant: A,
                                expression: 2,
                                branches: [
                                    TypeNode {type: int, expression: 1}
                                ]
                            }
                        ]
                     }
                     */
                    
                    if (variantNode.Branches.Count > 0)
                    {
                        otherwiseBasicBlockId = new BasicBlockId("after");
                    }
                    else
                    {
                        foreach (var (variable, variableAccessor) in GetAncestorVariableAssignments(variantNode))
                        {
                            _basicBlockStatements.Add(
                                new Assign(GetLocalVariablePlace(variable), new Use(variableAccessor.ToOperand())));
                        }
                        NewLowerExpression(variantNode.Expression.NotNull(), destination);
                    }
                }

                if (variantNode.Branches.Count > 0)
                {
                    LowerMatchTree(variantNode.Branches, destination, otherwiseBasicBlockId, afterBasicBlockId);

                    if (variantNode.Expression is not null)
                    {
                        _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);

                        otherwiseBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
                        
                        foreach (var (variable, variableAccessor) in GetAncestorVariableAssignments(variantNode))
                        {
                            _basicBlockStatements.Add(
                                new Assign(GetLocalVariablePlace(variable), new Use(variableAccessor.ToOperand())));
                        }
                        
                        NewLowerExpression(variantNode.Expression, destination);
                    }
                }
            }
        }

        if (typeNodes.Count > 0)
        {
            // for now there can only be a single type node. when interfaces are introduced and there are multiple type nodes, this will need a runtime check
            Debug.Assert(typeNodes.Count == 1);
            
            var typeNode = typeNodes[0];

            if (typeNode.Expression is not null)
            {
                /*
                 if there is an expression and also branches, then the expression is treated as the otherwise case.
                 This is based on the assumption that redundant patterns are not processed here.
                 eg:
                 match (a) {
                    MyUnion::A => 1,
                    MyUnion => 2
                 }
                 This will end up with like TypeNode {type: MyUnion, expression: 2, branches: [VariantNode{variant: A, expression: 1}]}
                 */
                
                if (typeNode.Branches.Count > 0)
                {
                    otherwiseBasicBlockId = new BasicBlockId("otherwise");
                }
                else
                {
                    foreach (var (variable, variableAccessor) in GetAncestorVariableAssignments(typeNode))
                    {
                        _basicBlockStatements.Add(
                            new Assign(GetLocalVariablePlace(variable), new Use(variableAccessor.ToOperand())));
                    }
                    NewLowerExpression(typeNode.Expression, destination);
                }
            }
            
            if (typeNode.Branches.Count > 0)
            {
                LowerMatchTree(typeNode.Branches, destination, otherwiseBasicBlockId, afterBasicBlockId);

                if (typeNode.Expression is not null)
                {
                    _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);
                    otherwiseBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
                    
                    foreach (var (variable, variableAccessor) in GetAncestorVariableAssignments(typeNode))
                    {
                        _basicBlockStatements.Add(
                            new Assign(GetLocalVariablePlace(variable), new Use(variableAccessor.ToOperand())));
                    }
                    NewLowerExpression(typeNode.Expression, destination);
                }
            }
        }

        if (discardNodes.Count == 1)
        {
            var discardNode = discardNodes[0];
            _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);
            otherwiseBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
            
            foreach (var (variable, variableAccessor) in GetAncestorVariableAssignments(discardNode))
            {
                _basicBlockStatements.Add(
                    new Assign(GetLocalVariablePlace(variable), new Use(variableAccessor.ToOperand())));
            }
            NewLowerExpression(discardNode.Expression.NotNull(), destination);
        }
    }

    private static IEnumerable<(TypeChecker.LocalVariable localVariable, IExpressionResult accessor)>
        GetAncestorVariableAssignments(INode node)
    {
        return node switch
        {
            {Ancestor: null, Variable: null} => [],
            {Ancestor: null, Variable: {} variable, Accessor: var accessor} => [(variable, accessor)],
            {Ancestor: {} ancestor, Variable: null} => GetAncestorVariableAssignments(ancestor),
            {Ancestor: {} ancestor, Variable: {} variable, Accessor: var accessor} => [(variable, accessor), ..GetAncestorVariableAssignments(ancestor)]
        };
    }
    
    private interface INode
    {
        IExpressionResult Accessor { get; }
        INode? Ancestor { get; }
        List<INode> Branches { get; }
        IExpression? Expression { get; set; }
        TypeChecker.LocalVariable? Variable { get; }
    }

    private record TypeNode(
        INode? Ancestor,
        ILoweredTypeReference Type,
        IExpressionResult Accessor,
        List<INode> Branches,
        TypeChecker.LocalVariable? Variable) : INode
    {
        public IExpression? Expression { get; set; }
    }

    private record VariantNode(
        INode? Ancestor,
        LoweredConcreteTypeReference Type,
        TypeChecker.ITypeReference ExpressionType,
        string VariantName,
        IExpressionResult Accessor,
        List<INode> Branches,
        TypeChecker.LocalVariable? Variable) : INode
    {
        public IExpression? Expression { get; set; }
    }

    private record DiscardNode(
        INode? Ancestor,
        IExpressionResult Accessor,
        List<INode> Branches,
        TypeChecker.LocalVariable? Variable)
        : INode
    {
        public IExpression? Expression { get; set; }
    }

    private List<INode> BuildMatchTree(IEnumerable<(IPattern Pattern, IExpression Expression)> patterns, IExpressionResult accessExpression, ILoweredTypeReference valueType)
    {
        var rootNodes = new List<INode>();

        foreach (var (pattern, expression) in patterns.Where(x => !x.Pattern.IsRedundant))
        {
            var patternNode = CreateNodeFromPattern(ancestor: null, pattern, ref accessExpression, valueType);
            InsertNodeIntoMatchTree(rootNodes, patternNode, expression);
        }
        
        return rootNodes;
    }

    private IPlace ExpressionResultIntoPlace(IExpressionResult expressionResult, ILoweredTypeReference valueType)
    {
        if (expressionResult is PlaceResult(var placeValue))
        {
            return placeValue;
        }
        if (expressionResult is OperandResult(var operandValue))
        {
            var local = new MethodLocal(LocalName((uint)_locals.Count), null, valueType);
            _locals.Add(local);
            var place = new Local(local.CompilerGivenName);
            
            _basicBlockStatements.Add(new Assign(place, new Use(operandValue)));

            return place;
        }
        
        throw new UnreachableException();
    }

    private static void InsertNodeIntoMatchTree(List<INode> tree, INode patternNode, IExpression expression)
    {
        // incoming nodes will either have one or 0 branches 
        Debug.Assert(patternNode.Branches.Count <= 1);
        
        foreach (var node in tree.Where(node => AreNodesShallowEqual(node, patternNode)))
        {
            Debug.Assert(node.Accessor.Equals(patternNode.Accessor));
            if (patternNode.Branches.Count == 0)
            {
                Debug.Assert(node.Expression is null);
                node.Expression = expression;

                return;
            }
            
            InsertNodeIntoMatchTree(node.Branches, patternNode.Branches[0], expression);
            
            return;
        }
        tree.Add(patternNode);
        
        var leaf = GetNodeLeafs(patternNode).Single();
        Debug.Assert(leaf.Expression is null);
        leaf.Expression = expression;
    }

    private static bool AreNodesShallowEqual(INode left, INode right)
    {
        return (left, right) switch
        {
            (TypeNode typeNode, TypeNode patternTypeNode) => EqualTypeReferences(typeNode.Type, patternTypeNode.Type),
            (VariantNode variantNode, VariantNode patternVariantNode) => EqualTypeReferences(variantNode.Type,
                patternVariantNode.Type) && variantNode.VariantName == patternVariantNode.VariantName,
            (DiscardNode, DiscardNode) => true,
            _ => false
        };
    }

    private INode CreateNodeFromPattern(INode? ancestor, IPattern? pattern, ref IExpressionResult accessor, ILoweredTypeReference valueType)
    {
        switch (pattern)
        {
            case ClassPattern classPattern:
            {
                var typeReference = GetTypeReference(classPattern.TypeReference.NotNull());
                var concreteType = GetConcreteTypeReference(typeReference);
                
                var variant = GetDataType(concreteType.DefinitionId)
                    .Variants[0];
                
                var typeNode = new TypeNode(ancestor, concreteType, accessor, [], classPattern.Variable);
                
                INode previousNode = typeNode;
                
                IPlace accessorPlace;
                if (accessor is PlaceResult(var placeValue))
                {
                    accessorPlace = placeValue;
                }
                else if (accessor is OperandResult)
                {
                    accessorPlace = ExpressionResultIntoPlace(accessor, valueType);
                    accessor = new PlaceResult(accessorPlace);
                }
                else
                {
                    throw new UnreachableException();
                }
                
                if (valueType is LoweredPointer && accessorPlace is not Deref)
                {
                    accessorPlace = new Deref(accessorPlace);
                }
                
                // loop through the variant's fields rather that the field patterns so that we every branch has the same order and number of fields
                foreach (var field in variant.Fields)
                {
                    var fieldPattern =
                        classPattern.FieldPatterns.FirstOrDefault(x =>
                            x.FieldName.StringValue == field.Name)
                        ?? new FieldPattern(Token.Identifier(field.Name, SourceSpan.Default), new DiscardPattern(SourceRange.Default));

                    IExpressionResult fieldPlace = new PlaceResult(new Field(accessorPlace, fieldPattern.FieldName.StringValue, ClassVariantName));
                    
                    var newNode = CreateNodeFromPattern(previousNode, fieldPattern.Pattern ??
                                                        new VariableDeclarationPattern(fieldPattern.FieldName,
                                                            SourceRange.Default, IsMut: false){Variable = fieldPattern.Variable},
                        ref fieldPlace,
                        field.Type);

                    var leaf = GetNodeLeafs(newNode).Single();
                    
                    previousNode.Branches.Add(newNode);
                    previousNode = leaf;
                }

                return typeNode;
            }
            case DiscardPattern:
                return new DiscardNode(ancestor, accessor, [], null);
            case TypePattern typePattern:
                return new TypeNode(ancestor, GetConcreteTypeReference(GetTypeReference(typePattern.TypeReference.NotNull())), accessor, [], typePattern.Variable);
            case UnionClassVariantPattern unionClassVariantPattern:
            {
                var typeReference = GetConcreteTypeReference(GetTypeReference(unionClassVariantPattern.TypeReference.NotNull()));
                var typeNode = new TypeNode(ancestor, typeReference, accessor, [], null);
                
                var node = new VariantNode(
                    typeNode,
                    typeReference,
                    unionClassVariantPattern.TypeReference.NotNull(),
                    unionClassVariantPattern.VariantName.StringValue,
                    accessor,
                    [],
                    unionClassVariantPattern.Variable);

                typeNode.Branches.Add(node);

                INode previousNode = node;

                var variant = GetDataType(node.Type.NotNull().DefinitionId)
                    .Variants.First(x => x.Name == node.VariantName);
                
                IPlace accessorPlace;
                if (accessor is PlaceResult(var placeValue))
                {
                    accessorPlace = placeValue;
                }
                else if (accessor is OperandResult)
                {
                    accessorPlace = ExpressionResultIntoPlace(accessor, valueType);
                    accessor = new PlaceResult(accessorPlace);
                }
                else
                {
                    throw new UnreachableException();
                }

                if (valueType is LoweredPointer && accessorPlace is not Deref)
                {
                    accessorPlace = new Deref(accessorPlace);
                }

                // loop through the variant's fields rather that the field patterns so that we every branch has the same order and number of fields
                foreach (var field in variant.Fields.Where(x => x.Name != VariantIdentifierFieldName))
                {
                    var fieldPattern =
                        unionClassVariantPattern.FieldPatterns.FirstOrDefault(x =>
                            x.FieldName.StringValue == field.Name)
                        ?? new FieldPattern(Token.Identifier(field.Name, SourceSpan.Default), new DiscardPattern(SourceRange.Default));
                    
                    IExpressionResult fieldPlace = new PlaceResult(new Field(accessorPlace, fieldPattern.FieldName.StringValue, unionClassVariantPattern.VariantName.StringValue));
                    
                    var newNode = CreateNodeFromPattern(previousNode, fieldPattern.Pattern ??
                                                        new VariableDeclarationPattern(fieldPattern.FieldName,
                                                            SourceRange.Default, IsMut: false){Variable = fieldPattern.Variable},
                        ref fieldPlace,
                        field.Type);

                    var leafs = GetNodeLeafs(newNode);
                    var leaf = leafs.Single();
                    
                    previousNode.Branches.Add(newNode);
                    previousNode = leaf;
                }
                
                return typeNode;
            }
            case UnionTupleVariantPattern unionTupleVariantPattern:
            {
                var typeReference = GetConcreteTypeReference(GetTypeReference(unionTupleVariantPattern.TypeReference.NotNull()));
                var typeNode = new TypeNode(ancestor, typeReference, accessor, [], null);
                
                var node = new VariantNode(
                    typeNode,
                    typeReference,
                    unionTupleVariantPattern.TypeReference.NotNull(),
                    unionTupleVariantPattern.VariantName.StringValue,
                    accessor,
                    [],
                    unionTupleVariantPattern.Variable);

                var dataType = GetDataType(typeReference.DefinitionId);
                var variant = dataType.Variants.First(x => x.Name == unionTupleVariantPattern.VariantName.StringValue);
                
                typeNode.Branches.Add(node);

                INode previousNode = node;
                
                IPlace accessorPlace;
                if (accessor is PlaceResult(var placeValue))
                {
                    accessorPlace = placeValue;
                }
                else if (accessor is OperandResult)
                {
                    accessorPlace = ExpressionResultIntoPlace(accessor, valueType);
                    accessor = new PlaceResult(accessorPlace);
                }
                else
                {
                    throw new UnreachableException();
                }
                
                if (valueType is LoweredPointer && accessorPlace is not Deref)
                {
                    accessorPlace = new Deref(accessorPlace);
                }

                // tuple patterns have to have the same number of patterns and in the same order, so just loop through the param patterns
                foreach (var (i, memberPattern) in unionTupleVariantPattern.TupleParamPatterns.Index())
                {
                    IExpressionResult fieldPlace = new PlaceResult(new Field(accessorPlace, TupleElementName((uint)i), unionTupleVariantPattern.VariantName.StringValue));
                    
                    var newNode = CreateNodeFromPattern(
                        previousNode,
                        memberPattern,
                        ref fieldPlace,
                        variant.Fields.First(x => x.Name == TupleElementName((uint)i)).Type);

                    var leafs = GetNodeLeafs(newNode);
                    var leaf = leafs.Single();

                    previousNode.Branches.Add(newNode);
                    previousNode = leaf;
                }

                return typeNode;
            }
            case UnionVariantPattern unionVariantPattern:
            {
                var typeReference = GetConcreteTypeReference(GetTypeReference(unionVariantPattern.TypeReference.NotNull()));
                var typeNode = new TypeNode(ancestor, typeReference, accessor, [], null);

                var variantNode = new VariantNode(
                    typeNode,
                    typeReference,
                    unionVariantPattern.TypeReference.NotNull(),
                    unionVariantPattern.VariantName.NotNull().StringValue,
                    accessor,
                    [],
                    unionVariantPattern.Variable);
                
                typeNode.Branches.Add(variantNode);
                
                return typeNode;
            }
            case VariableDeclarationPattern variableDeclarationPattern:
            {
                return new DiscardNode(ancestor, accessor, [], variableDeclarationPattern.Variable);
            }
            default:
                throw new UnreachableException();
        }
    }

    private static IEnumerable<INode> GetNodeLeafs(INode node)
    {
        return node.Branches.Count == 0
            ? [node]
            : node.Branches.SelectMany(GetNodeLeafs);
    }

    private IExpressionResult LowerMatch(MatchExpression e, IPlace? destination)
    {
        var accessResult = NewLowerExpression(e.Value, destination: null);

        var afterBasicBlockId = new BasicBlockId("after");
        
        var result = LowerMatchPatterns(
            [..e.Arms.Select(x => (x.Pattern, x.Expression.NotNull()))],
            accessResult,
            GetTypeReference(e.Value.ResolvedType.NotNull()),
            destination,
            afterBasicBlockId);

        afterBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

        return result;
    }
    
    private IExpressionResult LowerMatchesPattern(
            IExpressionResult value,
            IPattern pattern,
            IPlace? destination)
    {
        var boolType = GetTypeReference(TypeChecker.InstantiatedClass.Boolean);
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
                if (loweredTypeReference is not LoweredConcreteTypeReference)
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
                
                var isBoxed = IsTypeReferenceBoxed(unionType.NotNull());
                
                var type = GetConcreteTypeReference(GetTypeReference(unionType.NotNull()));
                var dataType = GetDataType(type.DefinitionId);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new MethodLocal(localName, null, boolType));
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
                    _locals.Add(new MethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }

                if (isBoxed)
                {
                    valuePlace = new Deref(valuePlace);
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
                
                var isBoxed = IsTypeReferenceBoxed(unionType.NotNull());
                
                var type = GetConcreteTypeReference(GetTypeReference(unionType.NotNull()));
                var dataType = GetDataType(type.DefinitionId);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new MethodLocal(localName, null, boolType));
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
                    _locals.Add(new MethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }
                
                if (isBoxed)
                {
                    valuePlace = new Deref(valuePlace);
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

                GetNextEmptyBasicBlock();
                var nextBasicBlock = _basicBlocks[^1];

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
                        new PlaceResult(new Field(valuePlace, TupleElementName((uint)i), variant.Name)),
                        tupleParamPattern,
                        destination);

                    var nextBasicBlockId = GetNextEmptyBasicBlock(); 
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

                    nextBasicBlock = _basicBlocks[^1];
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
                
                var isBoxed = IsTypeReferenceBoxed(unionType.NotNull());
                var type = GetConcreteTypeReference(GetTypeReference(unionType.NotNull()));
                var dataType = GetDataType(type.DefinitionId);
                var (variantIndex, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName.NotNull().StringValue);

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new MethodLocal(localName, null, boolType));
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
                    _locals.Add(new MethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }
                
                if (isBoxed)
                {
                    valuePlace = new Deref(valuePlace);
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

                GetNextEmptyBasicBlock();
                var nextBasicBlock = _basicBlocks[^1];

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

                    var nextBasicBlockId = GetNextEmptyBasicBlock();
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

                    nextBasicBlock = _basicBlocks[^1];
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
                
                var isBoxed = IsTypeReferenceBoxed(unionType.NotNull());
                var type = GetConcreteTypeReference(GetTypeReference(unionType.NotNull()));

                if (destination is null)
                {
                    var localName = LocalName((uint)_locals.Count);
                    _locals.Add(new MethodLocal(localName, null, boolType));
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
                    _locals.Add(new MethodLocal(localName, null, type));
                    valuePlace = new Local(localName);
                    _basicBlockStatements.Add(new Assign(valuePlace, new Use(value.ToOperand())));
                    throw new NotImplementedException("I don't know if this is actually ever hit");
                }
                
                if (isBoxed)
                {
                    valuePlace = new Deref(valuePlace);
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

                    var basicBlock = _basicBlocks[^1];
                    var nextBasicBlockId = GetNextEmptyBasicBlock();
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

    private bool IsTypeReferenceBoxed(TypeChecker.ITypeReference typeReference)
    {
        return typeReference switch
        {
            TypeChecker.FunctionObject => true,
            TypeChecker.GenericPlaceholder => false,
            TypeChecker.GenericTypeReference => false,
            TypeChecker.InstantiatedClass instantiatedClass => instantiatedClass.Boxed,
            TypeChecker.InstantiatedUnion instantiatedUnion => instantiatedUnion.Boxed,
            TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType => unspecifiedSizedIntType.Boxed,
            TypeChecker.UnknownInferredType {ResolvedType: var resolvedType} => IsTypeReferenceBoxed(resolvedType.NotNull()),
            TypeChecker.UnknownType => throw new UnreachableException($"{typeReference.GetType()}"),
            _ => throw new ArgumentOutOfRangeException(nameof(typeReference))
        };
    }
    
    private IExpressionResult LowerUnionClassVariantInitializer(
        UnionClassVariantInitializerExpression e,
        IPlace? destination)
    {
        var typeReference = GetTypeReference(e.ResolvedType.NotNull());

        var definitionId = typeReference switch
        {
            LoweredPointer(LoweredConcreteTypeReference pointerTo) => pointerTo.DefinitionId,
            LoweredConcreteTypeReference unboxedTypeReference => unboxedTypeReference.DefinitionId,
            _ => throw new InvalidOperationException()
        };
        
        var dataType = _types[definitionId];
        
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
                var unionType = GetTypeReference(e.OwnerType.NotNull());
                
                var concreteType = unionType switch
                {
                    LoweredPointer(LoweredConcreteTypeReference pointerTo) => pointerTo,
                    LoweredConcreteTypeReference unboxedTypeReference => unboxedTypeReference,
                    _ => throw new InvalidOperationException()
                };

                var dataType = _types[concreteType.DefinitionId];
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

                if (e.ResolvedType is not TypeChecker.FunctionObject)
                {
                    throw new InvalidOperationException($"Expected a function object, got a {e.ResolvedType?.GetType()}");
                }
                
                var ownerTypeArguments = concreteType.TypeArguments;

                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();

                var functionObjectType =
                    GetTypeReference(e.ResolvedType.NotNull());

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
                var ownerTypeArguments = GetConcreteTypeReference(GetTypeReference(e.OwnerType.NotNull())).TypeArguments;
                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();

                return CreateObject(
                    GetTypeReference(e.ResolvedType.NotNull()),
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
                    GetConcreteTypeReference(GetTypeReference(e.OwnerType.NotNull())),
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

                var ownerType = GetTypeReference(e.MemberAccess.OwnerType.NotNull());

                switch (ownerResult)
                {
                    case OperandResult{Value: var operand}:
                    {
                        var localName = LocalName((uint)_locals.Count);
                        _locals.Add(new MethodLocal(localName, null, ownerType));
            
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

                if (IsTypeReferenceBoxed(e.MemberAccess.OwnerType.NotNull()))
                {
                    ownerPlace = new Deref(ownerPlace);
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
                var concreteType = GetTypeReference(e.MemberAccess.OwnerType.NotNull()) switch
                {
                    LoweredPointer(LoweredConcreteTypeReference pointerTo) => pointerTo,
                    LoweredConcreteTypeReference unboxedTypeReference => unboxedTypeReference,
                    _ => throw new InvalidOperationException()
                };
                
                var fn = e.MemberAccess.InstantiatedFunction.NotNull();

                var functionObjectType =
                    GetTypeReference(e.ResolvedType.NotNull());

                return CreateObject(
                    functionObjectType,
                    ClassVariantName,
                    [
                        new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                                GetFunctionReference(
                                    fn.FunctionId,
                                    [..fn.TypeArguments.Select(GetTypeReference)],
                                    concreteType.TypeArguments))),
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
        var typeReference = GetTypeReference(objectInitializerExpression.ResolvedType.NotNull());

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
        ILoweredTypeReference type,
        string variantName,
        IEnumerable<CreateObjectField> fields,
        IPlace? destination)
    {
        if (destination is null)
        {
            var localName = LocalName((uint)_locals.Count);
            _locals.Add(new MethodLocal(localName, null, type));
            destination = new Local(localName);
        }

        var concreteType = type switch
        {
            LoweredPointer(LoweredConcreteTypeReference pointerTo) => pointerTo,
            LoweredConcreteTypeReference unboxedTypeReference => unboxedTypeReference,
            _ => throw new InvalidOperationException()
        };

        if (type is LoweredPointer)
        {
            _basicBlocks[^1].Terminator = new MethodCall(
                new LoweredFunctionReference(DefId.Allocate, []),
                [new SizeOf(concreteType)],
                destination,
                new BasicBlockId($"bb{_basicBlocks.Count}"));
            
            _basicBlockStatements = [];
            _basicBlocks.Add(new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements));
            
            destination = new Deref(destination);
        }
        
        _basicBlockStatements.Add(new Assign(
            destination,
            new CreateObject(concreteType)));

        foreach (var createObjectField in fields)
        {
            var field = new Field(destination, createObjectField.FieldName, variantName);
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
        
        return new PlaceResult(destination);
    }

    private IExpressionResult LowerTuple(TupleExpression tupleExpression, IPlace? destination)
    {
        if (tupleExpression.Values.Count == 1)
        {
            return NewLowerExpression(tupleExpression.Values[0], destination);
        }

        var typeReference = GetTypeReference(tupleExpression.ResolvedType.NotNull());

        return CreateObject(
            typeReference,
            ClassVariantName,
            tupleExpression.Values.Select((x, i) => new CreateObjectField(TupleElementName((uint)i), x)),
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
            _basicBlocks[^1].Terminator = new GoTo(new BasicBlockId(TempReturnBasicBlockId));
            GetNextEmptyBasicBlock();
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
            var local = new MethodLocal(localName, null, returnType);
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
        LoweredFunctionReference functionReference;

        // calling function object instead of normal function
        if (instantiatedFunction is null)
        {
            var functionObjectResult = NewLowerExpression(e.MethodCall.Method, destination: null);
            
            var methodType = GetConcreteTypeReference(GetTypeReference(e.MethodCall.Method.ResolvedType.NotNull()));

            var fn = _importedModules.SelectMany(x =>
                x.Methods.Where(y => y.Name == $"Function`{e.MethodCall.ArgumentList.Count + 1}__Call"))
                .First();
            
            functionReference = GetFunctionReference(
                    fn.Id,
                    [],
                    methodType.TypeArguments);

            arguments.Add(functionObjectResult.ToOperand());
            
            arguments.AddRange(originalArguments);

            var nextBasicBlockId = new BasicBlockId("after");
            _basicBlocks[^1].Terminator = new MethodCall(
                functionReference, arguments, destination, nextBasicBlockId);

            nextBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

            return new PlaceResult(destination);
        }
        
        IReadOnlyList<ILoweredTypeReference> ownerTypeArguments = [];
        if (e.MethodCall.Method is MemberAccessExpression memberAccess)
        {
            var owner = NewLowerExpression(memberAccess.MemberAccess.Owner, null);
            arguments.Add(owner.ToOperand());
            ownerTypeArguments =
                GetConcreteTypeReference(GetTypeReference(memberAccess.MemberAccess.Owner.ResolvedType.NotNull()))
                    .TypeArguments;
        }
        else if (instantiatedFunction.ClosureTypeId is not null)
        {
            var type = GetDataType(instantiatedFunction.ClosureTypeId);
            
            var localName = LocalName((uint)_locals.Count);
            _locals.Add(new MethodLocal(
                localName,
                null,
                new LoweredPointer(new LoweredConcreteTypeReference(
                    type.Name,
                    type.Id,
                    []))));
            CreateClosureObject(instantiatedFunction, new Local(localName));
            arguments.Add(new Copy(new Local(localName)));
        }
        else if (instantiatedFunction is { IsStatic: false, OwnerType: not null }
                 && _currentType is not null
                 && EqualTypeReferences(GetConcreteTypeReference(GetTypeReference(instantiatedFunction.OwnerType)), _currentType)
                 && _currentFunction?.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo)
                 && EqualTypeReferences(pointerTo, _currentType))
        {
            arguments.Add(new Copy(new Local(ParameterLocalName(parameterIndex: 0))));
        }

        if (e.MethodCall.Method is StaticMemberAccessExpression staticMemberAccess)
        {
            ownerTypeArguments = GetConcreteTypeReference(GetTypeReference(staticMemberAccess.OwnerType.NotNull())).TypeArguments;
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
            [..Enumerable.Select<TypeChecker.GenericTypeReference, ILoweredTypeReference>(instantiatedFunction.TypeArguments, GetTypeReference)],
            ownerTypeArguments);

        arguments.AddRange(originalArguments);

        {
            var nextBasicBlockId = new BasicBlockId("after");
            _basicBlocks[^1].Terminator = new MethodCall(functionReference, arguments, destination, nextBasicBlockId);
            nextBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

            return new PlaceResult(destination);
        }
    }

    private void CreateClosureObject(TypeChecker.InstantiatedFunction instantiatedFunction, IPlace destination)
    {
        Debug.Assert(instantiatedFunction.ClosureTypeId is not null);

        var closureType = _types[instantiatedFunction.ClosureTypeId];
        var closureTypeReference = new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []);

        _basicBlockStatements = [];
        _basicBlocks[^1].Terminator = new MethodCall(
            new LoweredFunctionReference(DefId.Allocate, []),
            [new SizeOf(closureTypeReference)],
            destination,
            new BasicBlockId($"bb{_basicBlocks.Count}"));
        _basicBlocks.Add(new BasicBlock(
            new BasicBlockId($"bb{_basicBlocks.Count}"),
            _basicBlockStatements));

        destination = new Deref(destination);
        
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
                case TypeChecker.LocalVariable localVariable:
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
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo)
                                    && EqualTypeReferences(pointerTo,
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
                                                new Deref(new Local(ParameterLocalName(0))),
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
                case TypeChecker.ThisVariable:
                case TypeChecker.FieldVariable:
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
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo2)
                                && EqualTypeReferences(
                                    pointerTo2,
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
                                            new Deref(new Local(ParameterLocalName(0))),
                                            ThisParameterName,
                                            ClassVariantName)))));
                            }
                            break;
                        }

                        Debug.Assert(
                            _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo)
                            && EqualTypeReferences(
                                pointerTo,
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
                case TypeChecker.FunctionSignatureParameter parameter:
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
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo)
                                    && EqualTypeReferences(pointerTo,
                                        currentClosureTypeReference));

                            var otherLocalsType = GetDataType(parameter.ContainingFunction.LocalsTypeId);

                            if (assignedFields.Add(otherLocalsType.Name))
                            {
                                _basicBlockStatements.Add(new Assign(
                                    new Field(destination, otherLocalsType.Name, ClassVariantName),
                                    new Use(new Copy(new Field(
                                        new Deref(new Local(ParameterLocalName(0))),
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

    private IExpressionResult LowerFallOut(IExpression operand, IPlace? destination)
    {
        var value = NewLowerExpression(operand, destination: null);

        var type = GetTypeReference(operand.ResolvedType.NotNull());
        var resultValuePlace = ExpressionResultIntoPlace(value, type);

        var errBasicBlockId = new BasicBlockId("err");
        var okBasicBlockId = new BasicBlockId("ok");

        _controlFlowDepth++;

        _basicBlocks[^1].Terminator = new SwitchInt(
                new Copy(new Field(resultValuePlace, VariantIdentifierFieldName, "Ok")),
                new Dictionary<int, BasicBlockId>
                {
                    { 0, okBasicBlockId }
                },
                errBasicBlockId);
        
        var currentMethod = _currentFunction.NotNull().LoweredMethod;
        var returnType = GetConcreteTypeReference(currentMethod.ReturnValue.Type);

        Debug.Assert(returnType.DefinitionId == DefId.Result);

        

        errBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
        _basicBlocks[^1].Terminator = new MethodCall(
            GetFunctionReference(DefId.Result_Create_Error, [], returnType.TypeArguments),
            [new Copy(new Field(resultValuePlace, TupleElementName(0), "Error"))],
            new Local(ReturnValueLocalName),
            new BasicBlockId(TempReturnBasicBlockId));
        
        _controlFlowDepth--;

        okBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

        var okValueField = new Field(
            resultValuePlace,
            TupleElementName(0),
            "Ok");

        if (destination is not null)
        {
            _basicBlockStatements.Add(
                new Assign(destination, new Use(new Copy(okValueField))));
        }

        return new PlaceResult(destination ?? okValueField);
    }

    private IExpressionResult LowerUnaryOperator(UnaryOperatorExpression unaryOperatorExpression, IPlace? destination)
    {
        if (unaryOperatorExpression.UnaryOperator.OperatorType == UnaryOperatorType.FallOut)
        {
            return LowerFallOut(unaryOperatorExpression.UnaryOperator.Operand.NotNull(), destination);
        }
        
        var valueOperand = NewLowerExpression(unaryOperatorExpression.UnaryOperator.Operand.NotNull(), destination: null).NotNull();

        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            _locals.Add(new MethodLocal(localName, null, GetTypeReference(unaryOperatorExpression.ResolvedType.NotNull())));
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
            return FunctionAccess(fn, (e.ResolvedType as TypeChecker.FunctionObject).NotNull(),
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
                TypeChecker.InstantiatedFunction innerFn,
                TypeChecker.FunctionObject typeReference,
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
                _locals.Add(new MethodLocal(localName, null, new LoweredPointer(new LoweredConcreteTypeReference(
                    dataType.Name,
                    dataType.Id,
                    []))));

                CreateClosureObject(innerFn, new Local(localName));
                
                functionObjectParameters.Add(new CreateObjectField("FunctionParameter", new Copy(new Local(localName))));
            }
            else if (innerFn is { IsStatic: false, OwnerType: not null }
                     && _currentType is not null
                     && EqualTypeReferences(GetConcreteTypeReference(GetTypeReference(innerFn.OwnerType)), _currentType)
                     && _currentFunction?.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo)
                     && EqualTypeReferences(pointerTo, _currentType))
            {
                functionObjectParameters.Add(new CreateObjectField("FunctionParameter", new Copy(new Local(ParameterLocalName(0)))));
            }

            return CreateObject(
                GetTypeReference(typeReference),
                ClassVariantName,
                functionObjectParameters,
                innerDestination);
        }

        IExpressionResult VariableAccess(
            TypeChecker.IVariable variable)
        {
            switch (variable)
            {
                case TypeChecker.LocalVariable localVariable:
                {
                    return new PlaceResult(GetLocalVariablePlace(localVariable));
                }
                case TypeChecker.ThisVariable thisVariable:
                    {
                        Debug.Assert(_currentFunction is not null);
                        Debug.Assert(_currentType is not null);

                        if (thisVariable.ReferencedInClosure
                                && _currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                            var closureTypeReference = new  LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureType.Id,
                                        []);
                            Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                            Debug.Assert(
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo2)
                                && EqualTypeReferences(
                                    pointerTo2,
                                    closureTypeReference));

                            return new PlaceResult(
                                new Field(
                                    new Deref(new Local(ParameterLocalName(0))),
                                    ClosureThisFieldName,
                                    ClassVariantName));
                        }

                        Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                        Debug.Assert(
                            _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo)
                            && EqualTypeReferences(
                                    pointerTo,
                                    _currentType));

                        return new PlaceResult(new Local(ParameterLocalName(0)));
                    }
                case TypeChecker.FieldVariable fieldVariable
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
                        var closureTypeReference = new LoweredConcreteTypeReference(closureType.Name, closureType.Id, []);
                    
                        // we're a closure, so reference the value through the "this" field
                        // of the closure type
                        Debug.Assert(loweredMethod.ParameterLocals.Count > 0);
                        Debug.Assert(
                            loweredMethod.ParameterLocals[0].Type is LoweredPointer(var pointerTo2)
                            && 
                                EqualTypeReferences(
                                    pointerTo2,
                                    closureTypeReference));

                        return new PlaceResult(new Field(
                            new Deref(new Field(
                                new Deref(new Local(ParameterLocalName(0))),
                                ClosureThisFieldName,
                                ClassVariantName)),
                            fieldVariable.Name.StringValue,
                            ClassVariantName)
                        );
                    }
                    
                    if (_currentFunction.Value.LoweredMethod.ParameterLocals.Count == 0
                        || _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type is not LoweredPointer(var pointerTo)
                        || !EqualTypeReferences(
                            pointerTo,
                            _currentType))
                    {
                        throw new InvalidOperationException("Expected to be in instance function");
                    }
                    
                    // todo: assert we're in a class and have _classVariant

                    return new PlaceResult(
                        new Field(
                            new Deref(new Local(ParameterLocalName(0))),
                            fieldVariable.Name.StringValue,
                            ClassVariantName));
                }
                case TypeChecker.FunctionSignatureParameter argument:
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
                        return new PlaceResult(new Field(new Deref(new Local(LocalsObjectLocalName)), argument.Name.StringValue,
                            ClassVariantName));
                    }

                    return new PlaceResult(new Field(
                        new Deref(new Field(
                            new Deref(new Local(ParameterLocalName(0))),
                            containingFunctionLocals.Name,
                            ClassVariantName)),
                        argument.Name.StringValue,
                        ClassVariantName));
                }
            }

            throw new UnreachableException($"{variable.GetType()}");
        }
    }

    private IPlace GetLocalVariablePlace(TypeChecker.LocalVariable variable)
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
                new Deref(new Local(LocalsObjectLocalName)),
                variable.Name.StringValue,
                ClassVariantName);
        }
                        
        return new Field(
            new Deref(new Field(
                new Deref(new Local("_param0")),
                containingFunctionLocals.Name,
                ClassVariantName)),
            variable.Name.StringValue,
            ClassVariantName);  
    }

    private static byte GetIntSize(TypeChecker.ITypeReference type)
    {
        if (type is TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType)
        {
            type = unspecifiedSizedIntType.ResolvedIntType.NotNull();
        }
        if (type is not TypeChecker.InstantiatedClass klass)
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
    
    private static bool IsIntSigned(TypeChecker.ITypeReference type)
    {
        if (type is TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType)
        {
            type = unspecifiedSizedIntType.ResolvedIntType.NotNull();
        }
        
        if (type is not TypeChecker.InstantiatedClass klass)
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

        NewLowerExpression(e.VariableDeclaration.Value, destination: new Field(new Deref(new Local(LocalsObjectLocalName)), FieldName: variableName, ClassVariantName));

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
            _locals.Add(new MethodLocal(localName, null, GetTypeReference(binaryOperatorExpression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }
        
        switch (binaryOperatorExpression.BinaryOperator.OperatorType)
        {
            case BinaryOperatorType.BooleanAnd:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), null).NotNull();

                var trueBasicBlockId = new BasicBlockId("true");
                var falseBasicBlockId = new BasicBlockId("false");
                var afterBasicBlockId = new BasicBlockId("after");
                
                _basicBlocks[^1].Terminator = new SwitchInt(
                    leftOperand.ToOperand(),
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, falseBasicBlockId }
                    },
                    trueBasicBlockId);

                trueBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

                _controlFlowDepth++;
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null)
                    .NotNull();
                _controlFlowDepth--;
                
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand.ToOperand())));
                _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);

                falseBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
                _basicBlockStatements.Add(new Assign(destination, new Use(new BoolConstant(false))));
                _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);

                afterBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
                break;
            }
            case BinaryOperatorType.BooleanOr:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null).NotNull();

                var falseBasicBlockId = new BasicBlockId("false");
                var trueBasicBlockId = new BasicBlockId("true");
                var afterBasicBlockId = new BasicBlockId("after");
                
                _basicBlocks[^1].Terminator = new SwitchInt(
                    leftOperand.ToOperand(),
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, falseBasicBlockId }
                    },
                    trueBasicBlockId);

                falseBasicBlockId.Id = GetNextEmptyBasicBlock().Id;

                _controlFlowDepth++;
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null)
                    .NotNull();
                _controlFlowDepth--;
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand.ToOperand())));
                _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);

                trueBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
                _basicBlockStatements.Add(new Assign(destination, new Use(new BoolConstant(true))));
                _basicBlocks[^1].Terminator = new GoTo(afterBasicBlockId);

                afterBasicBlockId.Id = GetNextEmptyBasicBlock().Id;
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