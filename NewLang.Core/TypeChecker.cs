using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NewLang.Core;

public class TypeChecker
{
    private readonly LangProgram _program;
    private readonly List<TypeCheckerError> _errors = [];

    // todo: generic placeholders in scope?
    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new();

    private readonly Dictionary<string, ITypeSignature> _types = ClassSignature.BuiltInTypes
        .Concat(UnionSignature.BuiltInTypes)
        .ToDictionary(x => x.Name);

    private TypeChecker(LangProgram program)
    {
        _program = program;
    }

    private Dictionary<string, FunctionSignature> ScopedFunctions => _typeCheckingScopes.Peek().Functions;
    private ITypeSignature? CurrentTypeSignature => _typeCheckingScopes.Peek().CurrentTypeSignature;
    private FunctionSignature? CurrentFunctionSignature => _typeCheckingScopes.Peek().CurrentFunctionSignature;
    private ITypeReference ExpectedReturnType => _typeCheckingScopes.Peek().ExpectedReturnType;

    public static IReadOnlyList<TypeCheckerError> TypeCheck(LangProgram program)
    {
        var typeChecker = new TypeChecker(program);
        typeChecker.TypeCheckInner();

        return typeChecker._errors;
    }

    private Variable GetScopedVariable(string name)
    {
        return _typeCheckingScopes.Peek().GetVariable(name);
    }

    private bool TryGetScopedVariable(string name, [NotNullWhen(true)] out Variable? variable)
    {
        return _typeCheckingScopes.Peek().TryGetVariable(name, out variable);
    }

    private bool TryAddScopedVariable(string name, Variable variable)
    {
        return _typeCheckingScopes.Peek().TryAddVariable(name, variable);
    }

    private void AddScopedVariable(string name, Variable variable)
    {
        _typeCheckingScopes.Peek().AddVariable(name, variable);
    }

    private bool VariableIsDefined(string name)
    {
        return _typeCheckingScopes.Peek().ContainsVariable(name);
    }

    private IDisposable PushScope(
        ITypeSignature? currentTypeSignature = null,
        FunctionSignature? currentFunctionSignature = null,
        ITypeReference? expectedReturnType = null)
    {
        var currentScope = _typeCheckingScopes.Peek();

        _typeCheckingScopes.Push(new TypeCheckingScope(
            currentScope,
            new Dictionary<string, FunctionSignature>(currentScope.Functions),
            expectedReturnType ?? currentScope.ExpectedReturnType,
            currentTypeSignature ?? currentScope.CurrentTypeSignature,
            currentFunctionSignature ?? currentScope.CurrentFunctionSignature));

        return new ScopeDisposable(PopScope);
    }

    private void PopScope()
    {
        _typeCheckingScopes.Pop();
    }

    private void TypeCheckInner()
    {
        // initial scope
        _typeCheckingScopes.Push(new TypeCheckingScope(
            null,
            new Dictionary<string, FunctionSignature>(),
            InstantiatedClass.Unit,
            null,
            null));

        var (classes, unions) = SetupSignatures();

        foreach (var (union, unionSignature) in unions)
        {
            var unionGenericPlaceholders = unionSignature.GenericParameters.ToHashSet();

            using (PushScope(unionSignature))
            {
                foreach (var function in union.Functions)
                {
                    var fnSignature = unionSignature.Functions.First(x => x.Name == function.Name.StringValue);

                    TypeCheckFunctionBody(function, fnSignature, unionGenericPlaceholders);
                }
            }
        }

        foreach (var (@class, classSignature) in classes)
        {
            var classGenericPlaceholders = classSignature.GenericParameters
                .ToHashSet();

            var instanceFieldVariables = new List<Variable>();
            var staticFieldVariables = new List<Variable>();

            foreach (var field in @class.Fields)
            {
                var isStatic = field.StaticModifier is not null;

                var fieldTypeReference = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type, classGenericPlaceholders);

                if (isStatic)
                {
                    // todo: static constructor?
                    if (field.InitializerValue is null)
                    {
                        throw new InvalidOperationException("Expected field initializer for static field");
                    }

                    var valueType = TypeCheckExpression(field.InitializerValue, classGenericPlaceholders);

                    ExpectType(valueType, fieldTypeReference, classGenericPlaceholders, field.InitializerValue.SourceRange);

                    staticFieldVariables.Add(new Variable(
                        field.Name.StringValue,
                        fieldTypeReference,
                        // field will be instantiated by the time it is accessed
                        true,
                        field.MutabilityModifier is not null));
                }
                else
                {
                    if (field.InitializerValue is not null)
                    {
                        throw new InvalidOperationException("Instance fields cannot have initializers");
                    }

                    instanceFieldVariables.Add(new Variable(
                        field.Name.StringValue,
                        fieldTypeReference,
                        // field will be instantiated by the time it is accessed
                        true,
                        field.MutabilityModifier is not null));
                }
            }

            // static functions
            using (PushScope(classSignature))
            {
                // static functions only have access to static fields
                foreach (var variable in staticFieldVariables)
                {
                    AddScopedVariable(variable.Name, variable);
                }

                foreach (var function in @class.Functions.Where(x => x.StaticModifier is not null))
                {
                    var fnSignature = classSignature.Functions.First(x => x.Name == function.Name.StringValue);

                    TypeCheckFunctionBody(function, fnSignature, classGenericPlaceholders);
                }
            }

            // instance functions
            using (PushScope(classSignature))
            {
                // instance functions have access to both instance and static fields
                foreach (var variable in instanceFieldVariables.Concat(staticFieldVariables))
                {
                    AddScopedVariable(variable.Name, variable);
                }

                foreach (var function in @class.Functions.Where(x => x.StaticModifier is null))
                {
                    var fnSignature = classSignature.Functions.First(x => x.Name == function.Name.StringValue);

                    TypeCheckFunctionBody(function, fnSignature, classGenericPlaceholders);
                }
            }
        }

        foreach (var functionSignature in ScopedFunctions.Values)
        {
            var function = _program.Functions.First(x => x.Name.StringValue == functionSignature.Name);
            TypeCheckFunctionBody(function, functionSignature, []);
        }

        foreach (var expression in _program.Expressions)
        {
            TypeCheckExpression(expression, []);
        }

        PopScope();

        ResolvedTypeChecker.CheckAllExpressionsHaveResolvedTypes(_program);
    }

    private (List<(ProgramClass, ClassSignature)>, List<(ProgramUnion, UnionSignature)>) SetupSignatures()
    {
        var classes =
            new List<(ProgramClass, ClassSignature, List<FunctionSignature>, List<TypeField> fields, List<TypeField>
                staticFields)>();
        var unions = new List<(ProgramUnion, UnionSignature, List<FunctionSignature>, List<IUnionVariant>)>();

        // setup union and class signatures before setting up their functions/fields etc. so that functions and fields can reference other types
        foreach (var union in _program.Unions)
        {
            var variants = new List<IUnionVariant>();
            var functions = new List<FunctionSignature>();
            var genericParameters = new List<GenericTypeReference>(union.GenericArguments.Count);
            var unionSignature = new UnionSignature
            {
                Name = union.Name.StringValue,
                GenericParameters = genericParameters,
                Functions = functions,
                Variants = variants
            };

            union.Signature = unionSignature;

            if (union.GenericArguments.GroupBy(x => x.StringValue).Any(x => x.Count() > 1))
            {
                throw new InvalidOperationException("Duplicate type argument");
            }

            genericParameters.AddRange(union.GenericArguments.Select(argument => new GenericTypeReference
                { GenericName = argument.StringValue, OwnerType = unionSignature }));

            unions.Add((union, unionSignature, functions, variants));

            if (!_types.TryAdd(unionSignature.Name, unionSignature))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeName(union.Name));
            }
        }

        foreach (var @class in _program.Classes)
        {
            var name = @class.Name.StringValue;
            var functions = new List<FunctionSignature>(@class.Functions.Count);
            var fields = new List<TypeField>(@class.Fields.Count(x => x.StaticModifier is null));
            var staticFields = new List<TypeField>(@class.Fields.Count(x => x.StaticModifier is not null));
            var genericParameters = new List<GenericTypeReference>(@class.TypeArguments.Count);
            var signature = new ClassSignature
            {
                Name = name,
                GenericParameters = genericParameters,
                Functions = functions,
                Fields = fields,
                StaticFields = staticFields
            };
            genericParameters.AddRange(@class.TypeArguments.Select(argument => new GenericTypeReference
                { GenericName = argument.StringValue, OwnerType = signature }));

            @class.Signature = signature;
            
            var typeArgumentsLookup = @class.TypeArguments.ToLookup(x => x.StringValue);

            foreach (var grouping in typeArgumentsLookup)
            {
                foreach (var typeArgument in grouping.Skip(1))
                {
                    _errors.Add(TypeCheckerError.DuplicateGenericArgument(typeArgument));
                }
            }

            classes.Add((@class, signature, functions, fields, staticFields));

            if (!_types.TryAdd(name, signature))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeName(@class.Name));
            }
        }

        foreach (var (union, unionSignature, functions, variants) in unions)
        {
            using var _ = PushScope(unionSignature);
            var unionGenericPlaceholders = unionSignature.GenericParameters
                .ToHashSet();

            foreach (var function in union.Functions)
            {
                if (functions.Any(x => x.Name == function.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.ConflictingFunctionName(function.Name));
                }
                
                functions.Add(TypeCheckFunctionSignature(function, unionGenericPlaceholders));
            }

            foreach (var variant in union.Variants)
            {
                if (variants.Any(x => x.Name == variant.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.DuplicateVariantName(variant.Name));
                }

                variants.Add(variant switch
                {
                    UnitStructUnionVariant => new NoMembersUnionVariant { Name = variant.Name.StringValue },
                    Core.TupleUnionVariant tupleVariant => TypeCheckTupleVariant(tupleVariant),
                    StructUnionVariant structUnionVariant => TypeCheckClassVariant(structUnionVariant),
                    _ => throw new UnreachableException()
                });

                continue;

                TupleUnionVariant TypeCheckTupleVariant(Core.TupleUnionVariant tupleVariant)
                {
                    if (tupleVariant.TupleMembers.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "union Tuple variants must have at least one parameter. Use a unit variant instead");
                    }

                    return new TupleUnionVariant
                    {
                        Name = variant.Name.StringValue,
                        TupleMembers =
                            [..tupleVariant.TupleMembers.Select(x => GetTypeReference(x, unionGenericPlaceholders))]
                    };
                }

                ClassUnionVariant TypeCheckClassVariant(StructUnionVariant structVariant)
                {
                    var fields = new List<TypeField>();
                    foreach (var field in structVariant.Fields)
                    {
                        if (fields.Any(x => x.Name == field.Name.StringValue))
                        {
                            _errors.Add(TypeCheckerError.DuplicateFieldInUnionStructVariant(union.Name, structVariant.Name, field.Name));
                        }

                        if (field.AccessModifier is not null)
                        {
                            throw new InvalidOperationException(
                                "Access modifier not allowed on class union variants. All fields are public");
                        }

                        if (field.StaticModifier is not null)
                        {
                            throw new InvalidOperationException("StaticModifier not allowed on class union variants");
                        }

                        var typeField = new TypeField
                        {
                            Name = field.Name.StringValue,
                            Type = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type, unionGenericPlaceholders),
                            IsMutable = field.MutabilityModifier is { Modifier.Type: TokenType.Mut },
                            IsPublic = true
                        };
                        fields.Add(typeField);
                    }

                    return new ClassUnionVariant
                    {
                        Fields = fields,
                        Name = structVariant.Name.StringValue
                    };
                }
            }
        }

        foreach (var (@class, classSignature, functions, fields, staticFields) in classes)
        {
            using var _ = PushScope(classSignature);
            var classGenericPlaceholders = classSignature.GenericParameters
                .ToHashSet();

            foreach (var fn in @class.Functions)
            {
                if (functions.Any(x => x.Name == fn.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.ConflictingFunctionName(fn.Name));
                }
                
                // todo: function overloading
                functions.Add(TypeCheckFunctionSignature(fn, classGenericPlaceholders));
            }

            foreach (var field in @class.Fields)
            {
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type, classGenericPlaceholders),
                    IsMutable = field.MutabilityModifier is { Modifier.Type: TokenType.Mut },
                    IsPublic = field.AccessModifier is { Token.Type: TokenType.Pub }
                };

                if (field.StaticModifier is not null)
                {
                    if (staticFields.Any(y => y.Name == typeField.Name))
                    {
                        throw new InvalidOperationException($"Static field with name {field.Name} already defined");
                    }

                    staticFields.Add(typeField);
                }
                else
                {
                    if (fields.Any(y => y.Name == typeField.Name))
                    {
                        throw new InvalidOperationException($"Field with name {field.Name} already defined");
                    }

                    fields.Add(typeField);
                }
            }
        }

        foreach (var fn in _program.Functions)
        {
            var name = fn.Name.StringValue;

            // todo: function overloading
            if (!ScopedFunctions.TryAdd(name, TypeCheckFunctionSignature(fn, [])))
            {
                _errors.Add(TypeCheckerError.ConflictingFunctionName(fn.Name));
            }
        }

        foreach (var genericParameter in _program.Classes.SelectMany(x => x.TypeArguments)
                     .Concat(_program.Unions.SelectMany(x => x.GenericArguments))
                     .Where(x => _types.ContainsKey(x.StringValue)))
        {
            _errors.Add(TypeCheckerError.TypeArgumentConflictsWithType(genericParameter));
        }

        return (
            classes.Select(x => (x.Item1, x.Item2)).ToList(),
            unions.Select(x => (x.Item1, x.Item2)).ToList()
        );
    }


    private void TypeCheckFunctionBody(LangFunction function,
        FunctionSignature fnSignature,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var functionType = InstantiateFunction(fnSignature, [..genericPlaceholders]);

        var innerGenericPlaceholders =
            new HashSet<GenericTypeReference>(genericPlaceholders);

        foreach (var typeArgument in functionType.TypeArguments)
        {
            innerGenericPlaceholders.Add(typeArgument);
        }

        using var _ = PushScope(null, fnSignature, fnSignature.ReturnType);
        foreach (var parameter in fnSignature.Arguments.Values)
        {
            AddScopedVariable(parameter.Name, new Variable(
                parameter.Name,
                parameter.Type,
                true,
                parameter.Mutable));
        }

        TypeCheckBlock(function.Block,
            innerGenericPlaceholders);
    }

    private FunctionSignature TypeCheckFunctionSignature(LangFunction fn,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var parameters = new OrderedDictionary<string, FunctionArgument>();

        var name = fn.Name.StringValue;
        var genericParameters = new List<GenericTypeReference>(fn.TypeArguments.Count);
        var fnSignature = new FunctionSignature(
            name,
            genericParameters,
            parameters,
            fn.StaticModifier is not null)
        {
            ReturnType = null!
        };

        fn.Signature = fnSignature;

        var foundTypeArguments = new HashSet<string>();
        var genericPlaceholdersDictionary = genericPlaceholders.ToDictionary(x => x.GenericName);
        foreach (var typeArgument in fn.TypeArguments)
        {
            if (!foundTypeArguments.Add(typeArgument.StringValue))
            {
                _errors.Add(TypeCheckerError.DuplicateGenericArgument(typeArgument));
            }

            if (genericPlaceholdersDictionary.ContainsKey(typeArgument.StringValue))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeArgument(typeArgument));
            }
            
            if (_types.ContainsKey(typeArgument.StringValue))
            {
                _errors.Add(TypeCheckerError.TypeArgumentConflictsWithType(typeArgument));
            }
            genericParameters.Add(new GenericTypeReference
            {
                GenericName = typeArgument.StringValue,
                OwnerType = fnSignature
            });
        }

        var functionType = InstantiateFunction(fnSignature, [..genericPlaceholders]);

        HashSet<GenericTypeReference> innerGenericPlaceholders = [..functionType.TypeArguments, ..genericPlaceholders];

        fnSignature.ReturnType = fn.ReturnType is null
            ? InstantiatedClass.Unit
            : GetTypeReference(fn.ReturnType, innerGenericPlaceholders);

        for (var i = 0; i < fn.Parameters.Count; i++)
        {
            var parameter = fn.Parameters[i];
            var paramName = parameter.Identifier.StringValue;
            var type = parameter.Type is null ? UnknownType.Instance : GetTypeReference(parameter.Type, innerGenericPlaceholders);
            
            if (!parameters.TryAdd(paramName, new FunctionArgument(paramName, type, parameter.MutabilityModifier is not null)))
            {
                _errors.Add(TypeCheckerError.DuplicateFunctionArgument(parameter.Identifier, fn.Name));
            }
        }

        // todo: function overloading
        return fnSignature;
    }

    private ITypeReference TypeCheckBlock(
        Block block,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        using var _ = PushScope();

        foreach (var fn in block.Functions)
        {
            ScopedFunctions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, genericPlaceholders);
        }

        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, ScopedFunctions[fn.Name.StringValue], genericPlaceholders);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, genericPlaceholders);
        }

        // todo: tail expressions
        return InstantiatedClass.Unit;
    }

    private ITypeReference TypeCheckExpression(
        IExpression expression,
        HashSet<GenericTypeReference> genericPlaceholders,
        bool allowUninstantiatedVariable = false)
    {
        var expressionType = expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, genericPlaceholders),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression,
                allowUninstantiatedVariable, genericPlaceholders),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression,
                genericPlaceholders),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression.MethodCall,
                genericPlaceholders),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block, genericPlaceholders),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression,
                genericPlaceholders),
            BinaryOperatorExpression binaryOperatorExpression => TypeCheckBinaryOperatorExpression(
                binaryOperatorExpression.BinaryOperator, genericPlaceholders),
            ObjectInitializerExpression objectInitializerExpression => TypeCheckObjectInitializer(
                objectInitializerExpression, genericPlaceholders),
            MemberAccessExpression memberAccessExpression => TypeCheckMemberAccess(memberAccessExpression.MemberAccess,
                genericPlaceholders),
            StaticMemberAccessExpression staticMemberAccessExpression => TypeCheckStaticMemberAccess(
                staticMemberAccessExpression.StaticMemberAccess, genericPlaceholders),
            GenericInstantiationExpression genericInstantiationExpression => TypeCheckGenericInstantiation(
                genericInstantiationExpression.GenericInstantiation, genericPlaceholders),
            UnaryOperatorExpression unaryOperatorExpression => TypeCheckUnaryOperator(
                unaryOperatorExpression.UnaryOperator,
                genericPlaceholders),
            UnionStructVariantInitializerExpression unionStructVariantInitializerExpression =>
                TypeCheckUnionStructInitializer(
                    unionStructVariantInitializerExpression.UnionInitializer, genericPlaceholders),
            MatchesExpression matchesExpression => TypeCheckMatchesExpression(
                matchesExpression, genericPlaceholders),
            TupleExpression tupleExpression => TypeCheckTupleExpression(tupleExpression, genericPlaceholders),
            MatchExpression matchExpression => TypeCheckMatchExpression(matchExpression, genericPlaceholders),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };

        expression.ResolvedType = expressionType;

        return expressionType;
    }

    private ITypeReference TypeCheckTupleExpression(TupleExpression tuple,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        if (tuple.Values.Count == 1)
        {
            return TypeCheckExpression(tuple.Values[0], genericPlaceholders);
        }

        var types = tuple.Values.Select(value => (TypeCheckExpression(value, genericPlaceholders), value.SourceRange)).ToArray();

        return InstantiateTuple(types);
    }

    private ITypeReference TypeCheckMatchExpression(MatchExpression matchExpression,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var valueType = TypeCheckExpression(matchExpression.Value, genericPlaceholders);

        ITypeReference? foundType = null;

        foreach (var arm in matchExpression.Arms)
        {
            var patternVariables = TypeCheckPattern(valueType, arm.Pattern, genericPlaceholders);

            using var _ = PushScope();
            foreach (var variable in patternVariables)
            {
                GetScopedVariable(variable).Instantiated = true;
            }

            var armType = arm.Expression is null
                ? UnknownType.Instance
                : TypeCheckExpression(arm.Expression, genericPlaceholders);
            
            foundType ??= armType;

            ExpectExpressionType(foundType, arm.Expression, genericPlaceholders);

            foreach (var variable in patternVariables)
            {
                GetScopedVariable(variable).Instantiated = false;
            }
        }

        if (!IsMatchExhaustive(matchExpression.Arms, valueType))
        {
            throw new InvalidOperationException("match expression is not exhaustive");
        }

        return foundType ?? throw new UnreachableException("Parser checked match expression has at least one arm");
    }

    private static bool IsMatchExhaustive(IReadOnlyList<MatchArm> matchArms, ITypeReference type)
    {
        var foundDiscardPattern = false;
        var matchedVariants = new List<string>();

        foreach (var armPattern in matchArms.Select(x => x.Pattern))
        {
            foundDiscardPattern |= armPattern is DiscardPattern;
            switch (armPattern)
            {
                case UnionVariantPattern { VariantName.StringValue: var variantName }:
                    matchedVariants.Add(variantName);
                    break;
                case UnionStructVariantPattern { VariantName.StringValue: var structVariantName }:
                    matchedVariants.Add(structVariantName);
                    break;
                case UnionTupleVariantPattern { VariantName.StringValue: var tupleVariantName }:
                    matchedVariants.Add(tupleVariantName);
                    break;
            }
        }

        // todo: other type patterns and exhaustive checks. string, int, etc

        return foundDiscardPattern
               || (type is InstantiatedUnion { Variants: var unionVariants }
                   && !unionVariants.Select(x => x.Name).Except(matchedVariants).Any());
    }

    private ITypeReference TypeCheckMatchesExpression(MatchesExpression matchesExpression,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var valueType = TypeCheckExpression(matchesExpression.ValueExpression, genericPlaceholders);

        if (matchesExpression.Pattern is not null)
        {
            matchesExpression.DeclaredVariables =
                TypeCheckPattern(valueType, matchesExpression.Pattern, genericPlaceholders);
        }

        return InstantiatedClass.Boolean;
    }

    private IReadOnlyList<string> TypeCheckPattern(ITypeReference typeReference, IPattern pattern,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var patternVariables = new List<string>();
        switch (pattern)
        {
            case DiscardPattern:
                // discard pattern always type checks
                break;
            case UnionVariantPattern variantPattern:
            {
                var patternUnionType = GetTypeReference(variantPattern.Type, genericPlaceholders);

                if (patternUnionType is not InstantiatedUnion union)
                {
                    throw new InvalidOperationException($"{patternUnionType} is not a union");
                }

                ExpectType(typeReference, union, genericPlaceholders, variantPattern.SourceRange);

                if (variantPattern.VariantName is not null)
                {
                    _ = union.Variants.FirstOrDefault(x => x.Name == variantPattern.VariantName.StringValue)
                        ?? throw new InvalidOperationException(
                            $"Variant {variantPattern.VariantName.StringValue} not found on type {union}");
                }

                if (variantPattern.VariableName is { StringValue: var variableName })
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternUnionType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case ClassPattern classPattern:
            {
                var patternType = GetTypeReference(classPattern.Type, genericPlaceholders);

                ExpectType(patternType, typeReference, genericPlaceholders, classPattern.SourceRange);

                if (classPattern.FieldPatterns.Count > 0)
                {
                    if (patternType is not InstantiatedClass classType)
                    {
                        throw new InvalidOperationException($"Expected {typeReference} to be a class");
                    }

                    if (classPattern.FieldPatterns.GroupBy(x => x.FieldName.StringValue).Any(x => x.Count() > 1))
                    {
                        throw new InvalidOperationException("Duplicate fields found");
                    }

                    var remainingFields = classType.Fields.Where(x => x.IsPublic)
                        .Select(x => x.Name)
                        .ToHashSet();

                    foreach (var (fieldName, fieldPattern) in classPattern.FieldPatterns)
                    {
                        remainingFields.Remove(fieldName.StringValue);
                        var fieldType = GetClassField(classType, fieldName.StringValue);
                        
                        if (fieldPattern is null)
                        {
                            patternVariables.Add(fieldName.StringValue);
                            var variable = new Variable(
                                fieldName.StringValue,
                                fieldType,
                                false,
                                false);
                            if (!TryAddScopedVariable(fieldName.StringValue, variable))
                            {
                                throw new InvalidOperationException($"Duplicate variable {fieldName.StringValue}");
                            }
                        }
                        else
                        {
                            patternVariables.AddRange(TypeCheckPattern(fieldType, fieldPattern, genericPlaceholders));
                        }
                    }

                    if (classPattern.RemainingFieldsDiscarded)
                    {
                        remainingFields.Clear();
                    }

                    if (remainingFields.Count > 0)
                    {
                        _errors.Add(TypeCheckerError.MissingFieldsInClassPattern(remainingFields, classPattern.Type));
                    }
                }

                if (classPattern.VariableName is { StringValue: var variableName })
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case UnionStructVariantPattern structVariantPattern:
            {
                var patternType = GetTypeReference(structVariantPattern.Type, genericPlaceholders);

                ExpectType(patternType, typeReference, genericPlaceholders, pattern.SourceRange);

                if (patternType is not InstantiatedUnion union)
                {
                    throw new InvalidOperationException($"{patternType} is not a union");
                }

                var variant = union.Variants.FirstOrDefault(x => x.Name == structVariantPattern.VariantName.StringValue)
                              ?? throw new InvalidOperationException(
                                  $"No variant found named {structVariantPattern.VariantName.StringValue}");

                if (variant is not ClassUnionVariant structVariant)
                {
                    throw new InvalidOperationException($"Variant {variant.Name} is not a struct variant");
                }

                if (structVariantPattern.FieldPatterns.GroupBy(x => x.FieldName.StringValue).Any(x => x.Count() > 1))
                {
                    throw new InvalidOperationException("Duplicate fields found");
                }

                if (!structVariantPattern.RemainingFieldsDiscarded &&
                    structVariantPattern.FieldPatterns.Count != structVariant.Fields.Count)
                {
                    throw new InvalidOperationException("Not all fields are listed");
                }

                foreach (var (fieldName, fieldPattern) in structVariantPattern.FieldPatterns)
                {
                    var fieldType = GetUnionStructVariantField(structVariant, fieldName.StringValue);

                    if (fieldPattern is null)
                    {
                        patternVariables.Add(fieldName.StringValue);

                        var variable = new Variable(
                            fieldName.StringValue,
                            fieldType,
                            false,
                            false);
                        if (!TryAddScopedVariable(fieldName.StringValue, variable))
                        {
                            throw new InvalidOperationException($"Duplicate variable {fieldName.StringValue}");
                        }
                    }
                    else
                    {
                        patternVariables.AddRange(TypeCheckPattern(fieldType, fieldPattern, genericPlaceholders));
                    }
                }

                if (structVariantPattern.VariableName is { StringValue: var variableName })
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case UnionTupleVariantPattern unionTupleVariantPattern:
            {
                var patternType = GetTypeReference(unionTupleVariantPattern.Type, genericPlaceholders);

                ExpectType(patternType, typeReference, genericPlaceholders, pattern.SourceRange);

                if (patternType is not InstantiatedUnion unionType)
                {
                    throw new InvalidOperationException($"{typeReference} is not a union");
                }

                var variant = unionType.Variants.FirstOrDefault(x =>
                                  x.Name == unionTupleVariantPattern.VariantName.StringValue)
                              ?? throw new InvalidOperationException(
                                  $"No union variant found with name {unionTupleVariantPattern.VariantName.StringValue}");

                if (variant is not TupleUnionVariant tupleUnionVariant)
                {
                    throw new InvalidOperationException("Expected union to be a tuple variant");
                }

                if (tupleUnionVariant.TupleMembers.Count != unionTupleVariantPattern.TupleParamPatterns.Count)
                {
                    throw new InvalidOperationException(
                        $"Expected {tupleUnionVariant.TupleMembers.Count} tuple members, found {unionTupleVariantPattern.TupleParamPatterns.Count}");
                }

                foreach (var (tupleMemberType, tupleMemberPattern) in tupleUnionVariant.TupleMembers.Zip(
                             unionTupleVariantPattern.TupleParamPatterns))
                {
                    patternVariables.AddRange(
                        TypeCheckPattern(tupleMemberType, tupleMemberPattern, genericPlaceholders));
                }

                if (unionTupleVariantPattern.VariableName is { StringValue: var variableName })
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case VariableDeclarationPattern { VariableName.StringValue: var variableName }:
            {
                patternVariables.Add(variableName);
                var variable = new Variable(
                    variableName,
                    typeReference,
                    false,
                    false);
                if (!TryAddScopedVariable(variableName, variable))
                {
                    throw new InvalidOperationException($"Duplicate variable {variableName}");
                }

                break;
            }
            case TypePattern { Type: var typeIdentifier, VariableName.StringValue: var variableName }:
            {
                var type = GetTypeReference(typeIdentifier, genericPlaceholders);
                patternVariables.Add(variableName);
                var variable = new Variable(variableName, type, Instantiated: false, Mutable: false);
                if (!TryAddScopedVariable(variableName, variable))
                {
                    throw new InvalidOperationException($"Duplicate variable {variableName}");
                }

                break;
            }
            default:
                throw new NotImplementedException(pattern.GetType().Name);
        }

        return patternVariables;
    }

    private ITypeReference TypeCheckUnionStructInitializer(UnionStructVariantInitializer initializer,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var type = GetTypeReference(initializer.UnionType, genericPlaceholders);

        if (type is not InstantiatedUnion instantiatedUnion)
        {
            throw new InvalidOperationException($"{type} is not a union");
        }

        var variant =
            instantiatedUnion.Variants.FirstOrDefault(x => x.Name == initializer.VariantIdentifier.StringValue)
            ?? throw new InvalidOperationException($"No union variant found name {initializer.VariantIdentifier}");

        if (variant is not ClassUnionVariant classVariant)
        {
            throw new InvalidOperationException($"{variant.Name} is not a union struct variant");
        }

        if (initializer.FieldInitializers.GroupBy(x => x.FieldName.StringValue)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Field can only be initialized once");
        }

        if (initializer.FieldInitializers.Count != classVariant.Fields.Count)
        {
            throw new InvalidOperationException("Not all fields were initialized");
        }

        var fields = classVariant.Fields.ToDictionary(x => x.Name);

        foreach (var fieldInitializer in initializer.FieldInitializers)
        {
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                throw new InvalidOperationException($"No field named {fieldInitializer.FieldName.StringValue}");
            }

            if (fieldInitializer.Value is not null)
                TypeCheckExpression(fieldInitializer.Value, genericPlaceholders);

            ExpectExpressionType(field.Type, fieldInitializer.Value, genericPlaceholders);
        }

        return type;
    }

    private ITypeReference TypeCheckUnaryOperator(UnaryOperator unaryOperator,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        return unaryOperator.OperatorType switch
        {
            UnaryOperatorType.FallOut => TypeCheckFallout(unaryOperator.Operand, genericPlaceholders),
            UnaryOperatorType.Not => TypeCheckNot(unaryOperator.Operand, genericPlaceholders),
            _ => throw new NotImplementedException($"{unaryOperator.OperatorType}")
        };
    }

    private InstantiatedClass TypeCheckNot(IExpression? expression, HashSet<GenericTypeReference> genericPlaceholders)
    {
        if (expression is not null)
            TypeCheckExpression(expression, genericPlaceholders);

        ExpectExpressionType(InstantiatedClass.Boolean, expression, genericPlaceholders);

        return InstantiatedClass.Boolean;
    }

    private ITypeReference TypeCheckFallout(IExpression? expression, HashSet<GenericTypeReference> genericPlaceholders)
    {
        if (expression is not null)
            TypeCheckExpression(expression, genericPlaceholders);

        // todo: could implement with an interface? union Result : IFallout?
        if (ExpectedReturnType is not InstantiatedUnion { Name: "result" or "option" } union)
        {
            throw new InvalidOperationException("Fallout operator is only valid for Result and Option return types");
        }

        ExpectExpressionType(ExpectedReturnType, expression, genericPlaceholders);

        if (union.Name == UnionSignature.Result.Name)
        {
            return union.TypeArguments.First(x => x.GenericName == "TValue");
        }

        if (union.Name == "Option")
        {
            throw new NotImplementedException("");
        }

        throw new UnreachableException();
    }

    private ITypeReference TypeCheckGenericInstantiation(GenericInstantiation genericInstantiation,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var valueType = TypeCheckExpression(genericInstantiation.Value, genericPlaceholders);

        if (valueType is not InstantiatedFunction instantiatedFunction)
        {
            throw new InvalidOperationException("Expected function");
        }

        if (genericInstantiation.GenericArguments.Count != instantiatedFunction.TypeArguments.Count)
        {
            throw new InvalidOperationException(
                $"Expected {instantiatedFunction.TypeArguments.Count} type arguments but found {genericInstantiation.GenericArguments.Count}");
        }

        for (var i = 0; i < instantiatedFunction.TypeArguments.Count; i++)
        {
            var typeReference = GetTypeReference(genericInstantiation.GenericArguments[i], genericPlaceholders);
            var expectedTypeArgument = instantiatedFunction.TypeArguments[i];
            ExpectType(typeReference, expectedTypeArgument, genericPlaceholders, genericInstantiation.GenericArguments[i].SourceRange);
        }

        return instantiatedFunction;
    }

    private ITypeReference TypeCheckMemberAccess(
        MemberAccess memberAccess,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var ownerExpression = memberAccess.Owner;
        var ownerType = TypeCheckExpression(ownerExpression, genericPlaceholders);

        if (ownerType is not InstantiatedClass classType)
        {
            // todo: generic argument constraints with interfaces?
            throw new InvalidOperationException("Can only access members on instantiated types");
        }

        if (memberAccess.MemberName is null)
        {
            return UnknownType.Instance;
        }

        if (TryInstantiateClassFunction(classType, memberAccess.MemberName.StringValue, out var function))
        {
            return function;
        }

        return GetClassField(classType, memberAccess.MemberName.StringValue);
    }

    private static ITypeReference GetUnionStructVariantField(ClassUnionVariant variant, string fieldName)
    {
        var fieldType = variant.Fields.FirstOrDefault(x => x.Name == fieldName)?.Type
                        ?? throw new InvalidOperationException($"No field named {fieldName}");

        return fieldType;
    }

    private ITypeReference GetClassField(InstantiatedClass classType, string fieldName)
    {
        var field = classType.Fields.FirstOrDefault(x => x.Name == fieldName)
                    ?? throw new InvalidOperationException($"No field named {fieldName}");

        if ((CurrentTypeSignature is not ClassSignature currentClassSignature
             || !classType.MatchesSignature(currentClassSignature))
            && !field.IsPublic)
        {
            throw new InvalidOperationException($"Cannot access private field {fieldName}");
        }

        var fieldType = field.Type;
        return fieldType;
    }

    private ITypeReference TypeCheckStaticMemberAccess(
        StaticMemberAccess staticMemberAccess,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var type = GetTypeReference(staticMemberAccess.Type, genericPlaceholders);

        var memberName = staticMemberAccess.MemberName?.StringValue;
        if (memberName is null)
        {
            return UnknownType.Instance;
        }
        
        if (type is InstantiatedClass { StaticFields: var staticFields } instantiatedClass)
        {
            var field = staticFields.FirstOrDefault(x => x.Name == memberName);
            if (field is not null)
            {
                return field.Type;
            }

            if (TryInstantiateClassFunction(instantiatedClass, memberName, out var function))
            {
                if (!function.IsStatic)
                {
                    throw new InvalidOperationException($"{memberName} is not static");
                }

                return function;
            }

            throw new InvalidOperationException($"No member found with name {memberName}");
        }

        if (type is InstantiatedUnion instantiatedUnion)
        {
            var variant = instantiatedUnion.Variants.FirstOrDefault(x => x.Name == memberName)
                          ?? throw new InvalidOperationException($"No union variant with name {memberName}");

            return variant switch
            {
                TupleUnionVariant tupleVariant => GetTupleUnionFunction(tupleVariant, instantiatedUnion),
                NoMembersUnionVariant => type,
                ClassUnionVariant => throw new InvalidOperationException(
                    "Cannot create struct union variant without initializer"),
                _ => throw new UnreachableException()
            };
        }

        throw new InvalidOperationException("Cannot access static members");
    }

    private ITypeReference TypeCheckObjectInitializer(
        ObjectInitializerExpression objectInitializerExpression,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var objectInitializer = objectInitializerExpression.ObjectInitializer;
        var foundType = GetTypeReference(objectInitializer.Type, genericPlaceholders);
        if (foundType is not InstantiatedClass instantiatedClass)
        {
            // todo: more checks
            throw new InvalidOperationException($"Type {foundType} cannot be initialized");
        }

        var initializedFields = new HashSet<string>();
        var fields = instantiatedClass.Fields.ToDictionary(x => x.Name);
        var insideClass = CurrentTypeSignature is ClassSignature currentClassSignature
                          && instantiatedClass.MatchesSignature(currentClassSignature);

        var publicFields = instantiatedClass.Fields
            .Where(x => x.IsPublic || insideClass)
            .Select(x => x.Name)
            .ToHashSet();

        foreach (var fieldInitializer in objectInitializer.FieldInitializers)
        {
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                _errors.Add(TypeCheckerError.UnknownClassField(fieldInitializer.FieldName));
                continue;
            }
            
            if (!publicFields.Contains(fieldInitializer.FieldName.StringValue))
            {
                _errors.Add(TypeCheckerError.PrivateFieldReferenced(fieldInitializer.FieldName));
            }
            // only set field as initialized if it is public
            else if (!initializedFields.Add(fieldInitializer.FieldName.StringValue))
            {
                _errors.Add(TypeCheckerError.ClassFieldSetMultipleTypesInInitializer(fieldInitializer.FieldName));
            }

            if (fieldInitializer.Value is not null)
            {
                TypeCheckExpression(fieldInitializer.Value, genericPlaceholders);
            }

            ExpectExpressionType(field.Type, fieldInitializer.Value, genericPlaceholders);
        }
        
        if (initializedFields.Count != publicFields.Count)
        {
            _errors.Add(TypeCheckerError.FieldsLeftUnassignedInClassInitializer(
                objectInitializerExpression,
                publicFields.Where(x => !initializedFields.Contains(x))));
        }

        return foundType;
    }

    private ITypeReference TypeCheckBinaryOperatorExpression(
        BinaryOperator @operator,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
            {
                if (@operator.Left is not null)
                    TypeCheckExpression(@operator.Left, genericPlaceholders);
                if (@operator.Right is not null)
                    TypeCheckExpression(@operator.Right, genericPlaceholders);
                ExpectExpressionType(InstantiatedClass.Int, @operator.Left, genericPlaceholders);
                ExpectExpressionType(InstantiatedClass.Int, @operator.Right, genericPlaceholders);

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
            {
                if (@operator.Left is not null)
                    TypeCheckExpression(@operator.Left, genericPlaceholders);
                if (@operator.Right is not null)
                    TypeCheckExpression(@operator.Right, genericPlaceholders);
                
                ExpectExpressionType(InstantiatedClass.Int, @operator.Left, genericPlaceholders);
                ExpectExpressionType(InstantiatedClass.Int, @operator.Right, genericPlaceholders);

                return InstantiatedClass.Int;
            }
            case BinaryOperatorType.EqualityCheck:
            {
                var leftType = @operator.Left is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Left, genericPlaceholders);
                var rightType = @operator.Right is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Right, genericPlaceholders);
                
                // todo: use interface. left and right implements IEquals<T>
                
                ExpectType(rightType, leftType, genericPlaceholders, new SourceRange(@operator.OperatorToken.SourceSpan, @operator.OperatorToken.SourceSpan));

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.ValueAssignment:
            {
                var leftType = @operator.Left is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Left, genericPlaceholders, true);
                if (@operator.Right is not null)
                    TypeCheckExpression(@operator.Right, genericPlaceholders);
                if (@operator.Left is not null)
                {
                    ExpectAssignableExpression(@operator.Left, genericPlaceholders);
                }

                if (@operator.Left is ValueAccessorExpression
                    {
                        ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken variableName }
                    } && GetScopedVariable(variableName.StringValue) is { Instantiated: false } variable)
                {
                    variable.Instantiated = true;
                }

                ExpectExpressionType(leftType, @operator.Right, genericPlaceholders);

                return leftType;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private bool ExpectAssignableExpression(IExpression expression, HashSet<GenericTypeReference> genericPlaceholders, bool report = true)
    {
        if (expression is ValueAccessorExpression
            {
                ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken valueToken }
            })
        {
            var variable = GetScopedVariable(valueToken.StringValue);
            if (variable is { Instantiated: true, Mutable: false })
            {
                if (report)
                {
                    _errors.Add(TypeCheckerError.NonMutableAssignment(variable.Name,
                        new SourceRange(valueToken.SourceSpan, valueToken.SourceSpan)));
                }
                return false;
            }
            
            return true;
        }

        if (expression is MemberAccessExpression memberAccess)
        {
            var owner = memberAccess.MemberAccess.Owner;

            if (memberAccess.MemberAccess.MemberName is null)
            {
                return false;
            }
            
            var isOwnerAssignable = ExpectAssignableExpression(owner, genericPlaceholders, report: false);

            if (owner.ResolvedType is not InstantiatedClass { Fields: var fields })
            {
                if (report)
                    _errors.Add(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                return false;
            }

            var field = fields.FirstOrDefault(x => x.Name == memberAccess.MemberAccess.MemberName.StringValue);
            if (field is null)
            {
                if (report)
                    _errors.Add(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                return false;
            }

            if (!field.IsMutable)
            {
                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberAssignment(memberAccess));
                return false;
            }

            if (!isOwnerAssignable)
            {
                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;
            }

            return true;
        }

        if (expression is StaticMemberAccessExpression staticMemberAccess)
        {
            var ownerType = GetTypeReference(staticMemberAccess.StaticMemberAccess.Type, genericPlaceholders);

            if (staticMemberAccess.StaticMemberAccess.MemberName is null)
            {
                return false;
            }

            if (ownerType is not InstantiatedClass { StaticFields: var staticFields })
            {
                if (report)
                    _errors.Add(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                return false;
            }

            var staticField = staticFields.FirstOrDefault(x =>
                x.Name == staticMemberAccess.StaticMemberAccess.MemberName.StringValue);
            if (staticField is null)
            {
                if (report)
                    _errors.Add(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                return false;
            }

            if (!staticField.IsMutable)
            {
                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberAssignment(staticMemberAccess));
                return false;
            }

            return true;
        }

        if (report)
            _errors.Add(TypeCheckerError.ExpressionNotAssignable(expression));
        
        return false;
    }

    private ITypeReference TypeCheckIfExpression(IfExpression ifExpression,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        // scope around the entire if expression. Variables declared in the check expression (e.g. with matches) will be
        // conditionally available in the body
        using var _ = PushScope();
        
        TypeCheckExpression(ifExpression.CheckExpression, genericPlaceholders);

        ExpectExpressionType(InstantiatedClass.Boolean, ifExpression.CheckExpression, genericPlaceholders);

        IReadOnlyList<string> conditionallyInstantiatedVariables = [];

        if (ifExpression.CheckExpression is MatchesExpression { DeclaredVariables: var declaredVariables })
        {
            conditionallyInstantiatedVariables = declaredVariables;
        }

        foreach (var variable in conditionallyInstantiatedVariables)
        {
            GetScopedVariable(variable).Instantiated = true;
        }

        if (ifExpression.Body is not null)
        {
            TypeCheckExpression(ifExpression.Body, genericPlaceholders);
        }

        foreach (var variable in conditionallyInstantiatedVariables)
        {
            GetScopedVariable(variable).Instantiated = false;
        }

        foreach (var elseIf in ifExpression.ElseIfs)
        {
            using var __ = PushScope();
            TypeCheckExpression(elseIf.CheckExpression, genericPlaceholders);
            
            ExpectExpressionType(InstantiatedClass.Boolean, elseIf.CheckExpression, genericPlaceholders);

            conditionallyInstantiatedVariables = elseIf.CheckExpression is MatchesExpression
            {
                DeclaredVariables: var elseIfDeclaredVariables
            }
                ? elseIfDeclaredVariables
                : [];

            foreach (var variable in conditionallyInstantiatedVariables)
            {
                GetScopedVariable(variable).Instantiated = true;
            }

            if (elseIf.Body is not null)
            {
                TypeCheckExpression(elseIf.Body, genericPlaceholders);
            }

            foreach (var variable in conditionallyInstantiatedVariables)
            {
                GetScopedVariable(variable).Instantiated = false;
            }
        }

        if (ifExpression.ElseBody is not null)
        {
            using var __ = PushScope();
            TypeCheckExpression(ifExpression.ElseBody, genericPlaceholders);
        }

        // todo: tail expression
        return InstantiatedClass.Unit;
    }

    private ITypeReference TypeCheckMethodCall(
        MethodCall methodCall,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var methodType = TypeCheckExpression(methodCall.Method, genericPlaceholders);

        if (methodType is UnknownType)
        {
            return UnknownType.Instance;
        }

        if (methodType is not InstantiatedFunction functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (methodCall.ParameterList.Count != functionType.Arguments.Count)
        {
            throw new InvalidOperationException(
                $"Expected {functionType.Arguments.Count} parameters, got {methodCall.ParameterList.Count}");
        }

        for (var i = 0; i < functionType.Arguments.Count; i++)
        {
            var (_, expectedParameterType, isParameterMutable) = functionType.Arguments.GetAt(i).Value;

            var parameterExpression = methodCall.ParameterList[i];
            TypeCheckExpression(parameterExpression, genericPlaceholders);

            ExpectExpressionType(expectedParameterType, parameterExpression, genericPlaceholders);

            if (isParameterMutable)
            {
                ExpectAssignableExpression(parameterExpression, genericPlaceholders);
            }
        }

        return functionType.ReturnType;
    }

    private ITypeReference TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        if (methodReturnExpression.MethodReturn.Expression is null)
        {
            // no inner expression to check the type of, but we know the type is unit
            ExpectType(InstantiatedClass.Unit, ExpectedReturnType, genericPlaceholders,
                methodReturnExpression.SourceRange);
        }
        else
        {
            TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, genericPlaceholders);
            ExpectExpressionType(ExpectedReturnType, methodReturnExpression.MethodReturn.Expression, genericPlaceholders);
        }

        return InstantiatedClass.Never;
    }

    private ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression,
        bool allowUninstantiatedVariables,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            { AccessType: ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral } } => InstantiatedClass
                .Int,
            { AccessType: ValueAccessType.Literal, Token: StringToken { Type: TokenType.StringLiteral } } =>
                InstantiatedClass.String,
            { AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedClass
                .Boolean,
            // todo: bring union variants into scope
            { AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: "ok"}} => TypeCheckResultVariantKeyword("Ok"),
            { AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: "error"}} =>
                TypeCheckResultVariantKeyword("Error"),
            { AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: "this" }} => TypeCheckThis(),
            { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo } => InstantiatedClass.Never,
            {
                    AccessType: ValueAccessType.Variable,
                    Token: StringToken { Type: TokenType.Identifier } variableNameToken
                } =>
                TypeCheckVariableAccess(variableNameToken, allowUninstantiatedVariables, genericPlaceholders),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };

        ITypeReference TypeCheckThis()
        {
            if (CurrentTypeSignature is null)
            {
                throw new InvalidOperationException("this is only available in instance functions within a type");
            }

            if (CurrentFunctionSignature is null)
            {
                throw new InvalidOperationException("this is not available in static field initializer");
            }

            if (CurrentFunctionSignature.IsStatic)
            {
                throw new InvalidOperationException("this is not available in static functions");
            }

            return CurrentTypeSignature switch
            {
                UnionSignature unionSignature => InstantiateUnion(unionSignature, null),
                ClassSignature classSignature => InstantiateClass(classSignature, null),
                _ => throw new UnreachableException($"Unknown signature type {CurrentTypeSignature.GetType()}")
            };
        }

        ITypeReference TypeCheckResultVariantKeyword(string variantName)
        {
            var unionSignature = UnionSignature.Result;

            var okVariant = unionSignature.Variants.FirstOrDefault(x => x.Name == variantName)
                            ?? throw new UnreachableException($"{variantName} is a built in variant of Result");

            if (okVariant is not TupleUnionVariant tupleVariant)
            {
                throw new UnreachableException($"{variantName} is a tuple variant");
            }

            var instantiatedUnion = InstantiateResult();

            return GetTupleUnionFunction(tupleVariant, instantiatedUnion);
        }
    }

    private InstantiatedFunction GetTupleUnionFunction(TupleUnionVariant tupleVariant,
        InstantiatedUnion instantiatedUnion)
    {
        var arguments = new OrderedDictionary<string, FunctionArgument>();
        for (var i = 0; i < tupleVariant.TupleMembers.Count; i++)
        {
            var name = i.ToString();
            var member = tupleVariant.TupleMembers[i];
            arguments.Add(name, new FunctionArgument(name, member, Mutable: false));
        }

        var signature = new FunctionSignature(
            tupleVariant.Name,
            [],
            arguments,
            true)
        {
            ReturnType = instantiatedUnion
        };

        return InstantiateFunction(signature, instantiatedUnion.TypeArguments);
    }

    private ITypeReference TypeCheckVariableAccess(
        StringToken variableName, bool allowUninstantiated, HashSet<GenericTypeReference> genericPlaceholders)
    {
        if (ScopedFunctions.TryGetValue(variableName.StringValue, out var function))
        {
            return InstantiateFunction(function, [..genericPlaceholders]);
        }

        if (!TryGetScopedVariable(variableName.StringValue, out var value))
        {
            _errors.Add(TypeCheckerError.SymbolNotFound(variableName));
            return UnknownType.Instance;
        }

        if (!allowUninstantiated && !value.Instantiated)
        {
            throw new InvalidOperationException($"{value.Name} is not instantiated");
        }

        return value.Type;
    }

    private ITypeReference TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var varName = expression.VariableDeclaration.VariableNameToken.StringValue;
        if (VariableIsDefined(varName))
        {
            throw new InvalidOperationException(
                $"Variable with name {varName} already exists");
        }

        switch (expression.VariableDeclaration)
        {
            case { Value: null, Type: null }:
                throw new InvalidOperationException("Variable declaration must have a type specifier or a value");
            case { Value: { } value, Type: var type, MutabilityModifier: var mutModifier }:
            {
                var valueType = TypeCheckExpression(value, genericPlaceholders);
                if (type is not null)
                {
                    var expectedType = GetTypeReference(type, genericPlaceholders);

                    ExpectExpressionType(expectedType, value, genericPlaceholders);
                }

                AddScopedVariable(varName, new Variable(varName, valueType, true, mutModifier is not null));

                break;
            }
            case { Value: null, Type: { } type, MutabilityModifier: var mutModifier }:
            {
                var langType = GetTypeReference(type, genericPlaceholders);
                AddScopedVariable(varName, new Variable(varName, langType, false, mutModifier is not null));

                break;
            }
        }

        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedClass.Unit;
    }

    private ITypeReference GetTypeReference(
        TypeIdentifier typeIdentifier,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        var identifierName = typeIdentifier.Identifier.StringValue;
        
        if (_types.TryGetValue(identifierName, out var nameMatchingType))
        {
            if (nameMatchingType is ClassSignature classSignature)
            {
                return InstantiateClass(classSignature, [
                    ..typeIdentifier.TypeArguments
                        .Select(x => (GetTypeReference(x, genericPlaceholders), x.SourceRange))
                ]);
            }

            if (nameMatchingType is UnionSignature unionSignature)
            {
                return InstantiateUnion(unionSignature, [
                    ..typeIdentifier.TypeArguments
                        .Select(x => (GetTypeReference(x, genericPlaceholders), x.SourceRange))
                ]);
            }
        }

        var genericTypeReference =
            genericPlaceholders.FirstOrDefault(x => x.GenericName == identifierName);

        if (genericTypeReference is not null)
        {
            return genericTypeReference;
        }
        throw new InvalidOperationException($"No type found {typeIdentifier}");
    }

    private void ExpectExpressionType(ITypeReference expected, IExpression? actual,
        HashSet<GenericTypeReference> genericPlaceholders)
    {
        if (actual is null)
        {
            return;
        }
        
        if (actual.ResolvedType is null)
        {
            throw new InvalidOperationException("Expected should have been type checked first before expecting it's value type");
        }

        _ = actual switch
        {
            MatchExpression matchExpression => ExpectMatchExpressionType(matchExpression),
            BlockExpression blockExpression => ExpectBlockExpressionType(blockExpression),
            GenericInstantiationExpression => throw new UnreachableException(),
            IfExpressionExpression ifExpressionExpression => ExpectIfExpressionType(ifExpressionExpression),
            // these expression types are considered to provide their own types, rather than deferring to inner expressions
            BinaryOperatorExpression or MatchesExpression or MemberAccessExpression or MethodCallExpression
                or MethodReturnExpression or ObjectInitializerExpression or StaticMemberAccessExpression
                or TupleExpression or UnaryOperatorExpression or UnionStructVariantInitializerExpression
                or ValueAccessorExpression or VariableDeclarationExpression => ExpectType(actual.ResolvedType!,
                    expected, genericPlaceholders, actual.SourceRange),
            _ => throw new UnreachableException(actual.GetType().ToString())
        };

        return;
        
        bool ExpectIfExpressionType(IfExpressionExpression ifExpression)
        {
            return ExpectType(ifExpression.ResolvedType!, expected, genericPlaceholders, SourceRange.Default);
            // todo: tail expression
        }

        bool ExpectBlockExpressionType(BlockExpression blockExpression)
        {
            return ExpectType(blockExpression.ResolvedType!, expected, genericPlaceholders, SourceRange.Default);
            // todo: tail expression
        }
    
        bool ExpectMatchExpressionType(MatchExpression matchExpression)
        {
            return ExpectType(matchExpression.ResolvedType!, expected, genericPlaceholders, SourceRange.Default);
            // todo: tail expression
        }
    }

    private bool ExpectType(ITypeReference actual, ITypeReference expected,
        HashSet<GenericTypeReference> genericPlaceholders, SourceRange actualSourceRange, bool reportError = true)
    {
        if ((actual is InstantiatedClass x && x.IsSameSignature(InstantiatedClass.Never))
            || (expected is InstantiatedClass y && y.IsSameSignature(InstantiatedClass.Never)))
        {
            return true;
        }
        
        var result = true;

        switch (actual, expected)
        {
            case (InstantiatedClass actualClass, InstantiatedClass expectedClass):
            {
                if (!actualClass.IsSameSignature(expectedClass))
                {
                    if (reportError)
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    result = false;
                }

                var argumentsPassed = true;

                for (var i = 0; i < actualClass.TypeArguments.Count; i++)
                {
                    argumentsPassed &= ExpectType(actualClass.TypeArguments[i], expectedClass.TypeArguments[i], genericPlaceholders, actualSourceRange, reportError: false);
                }

                if (!argumentsPassed && reportError)
                {
                    _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                }

                result &= argumentsPassed;

                break;
            }
            case (InstantiatedUnion actualUnion, InstantiatedUnion expectedUnion):
            {
                if (!actualUnion.IsSameSignature(expectedUnion))
                {
                    if (reportError)
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    result = false;
                }

                var argumentsPassed = true;

                for (var i = 0; i < actualUnion.TypeArguments.Count; i++)
                {
                    argumentsPassed &= ExpectType(actualUnion.TypeArguments[i], expectedUnion.TypeArguments[i],
                        genericPlaceholders, actualSourceRange, reportError: false);
                }
                
                if (!argumentsPassed && reportError)
                {
                    _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                }
                result &= argumentsPassed;

                break;
            }
            case (InstantiatedUnion union, GenericTypeReference generic):
            {
                if (generic.ResolvedType is not null)
                {
                    result &= ExpectType(union, generic.ResolvedType, genericPlaceholders, actualSourceRange, reportError);
                }
                else if (!genericPlaceholders.Contains(generic))
                {
                    generic.ResolvedType = union;
                }
                else
                {
                    if (reportError)
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    result = false;
                }

                break;
            }
            case (GenericTypeReference generic, InstantiatedUnion union):
            {
                if (generic.ResolvedType is not null)
                {
                    result &= ExpectType(union, generic.ResolvedType, genericPlaceholders, actualSourceRange, reportError);
                }
                else if (!genericPlaceholders.Contains(generic))
                {
                    generic.ResolvedType = union;
                }
                else
                {
                    if (reportError)
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    result = false;
                }

                break;
            }
            case (InstantiatedClass @class, GenericTypeReference generic):
            {
                if (generic.ResolvedType is not null)
                {
                    result &= ExpectType(@class, generic.ResolvedType, genericPlaceholders, actualSourceRange, reportError);
                }
                else if (!genericPlaceholders.Contains(generic))
                {
                    generic.ResolvedType = @class;
                }
                else
                {
                    if (reportError)
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    result = false;
                }

                break;
            }
            case (GenericTypeReference generic, InstantiatedClass @class):
            {
                if (generic.ResolvedType is not null)
                {
                    result &= ExpectType(@class, generic.ResolvedType, genericPlaceholders, actualSourceRange, reportError);
                }
                else if (!genericPlaceholders.Contains(generic))
                {
                    generic.ResolvedType = @class;
                }
                else
                {
                    if (reportError)
                        _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    result = false;
                }

                break;
            }
            case (GenericTypeReference genericTypeReference, GenericTypeReference expectedGeneric):
            {
                if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is not null)
                {
                    result &= ExpectType(genericTypeReference.ResolvedType, expectedGeneric.ResolvedType, genericPlaceholders, actualSourceRange, reportError);
                }
                else if (genericTypeReference.ResolvedType is null && expectedGeneric.ResolvedType is not null)
                {
                    if (!genericPlaceholders.Contains(genericTypeReference))
                    {
                        genericTypeReference.ResolvedType = expectedGeneric.ResolvedType;
                    }
                    else if (genericTypeReference.GenericName != expectedGeneric.GenericName)
                    {
                        if (reportError)
                            _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                    }
                }
                else if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is null)
                {
                    if (!genericPlaceholders.Contains(genericTypeReference))
                    {
                        expectedGeneric.ResolvedType = genericTypeReference.ResolvedType;
                    }
                    else if (genericTypeReference.GenericName != expectedGeneric.GenericName)
                    {
                        if (reportError)
                            _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                    }
                }
                else
                {
                    genericTypeReference.Link(expectedGeneric);
                    if (expectedGeneric != genericTypeReference && !genericPlaceholders.Contains(expectedGeneric))
                    {
                        expectedGeneric.ResolvedType = genericTypeReference;
                    }
                    else if (genericTypeReference.GenericName != expectedGeneric.GenericName)
                    {
                        if (reportError)
                            _errors.Add(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                    }
                }

                break;
            }
        }

        return result;
    }

    private record TypeCheckingScope(
        TypeCheckingScope? ParentScope,
        Dictionary<string, FunctionSignature> Functions,
        ITypeReference ExpectedReturnType,
        ITypeSignature? CurrentTypeSignature,
        FunctionSignature? CurrentFunctionSignature)
    {
        private Dictionary<string, Variable> CurrentScopeVariables { get; } = new();

        public Variable GetVariable(string name)
        {
            if (ParentScope?.TryGetVariable(name, out var parentScopeVariable) ?? false)
            {
                return parentScopeVariable;
            }

            return CurrentScopeVariables[name];
        }

        public bool TryGetVariable(string name, [NotNullWhen(true)] out Variable? variable)
        {
            if (ParentScope?.TryGetVariable(name, out variable) ?? false)
            {
                return true;
            }

            return CurrentScopeVariables.TryGetValue(name, out variable);
        }

        public bool TryAddVariable(string name, Variable variable)
        {
            return CurrentScopeVariables.TryAdd(name, variable);
        }

        public void AddVariable(string name, Variable variable)
        {
            CurrentScopeVariables.Add(name, variable);
        }

        public bool ContainsVariable(string name)
        {
            return CurrentScopeVariables.ContainsKey(name) || (ParentScope?.ContainsVariable(name) ?? false);
        }
    }

    private class ScopeDisposable(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Scope already disposed");
            }

            _disposed = true;
            onDispose();
        }
    }

    private record Variable(string Name, ITypeReference Type, bool Instantiated, bool Mutable)
    {
        public bool Instantiated { get; set; } = Instantiated;
    }

    public interface ITypeReference;

    public class UnknownType : ITypeReference
    {
        public static UnknownType Instance { get; } = new();
        
        private UnknownType()
        {
            
        }
    }

    public class GenericTypeReference : ITypeReference, IEquatable<GenericTypeReference>
    {
        public required string GenericName { get; init; }

        public required ITypeSignature OwnerType { get; init; }

        private ITypeReference? _resolvedType;
        public ITypeReference? ResolvedType
        {
            get => _resolvedType;
            set
            {
                if (ReferenceEquals(value, this))
                {
                    return;
                }
                
                _resolvedType = value;
                foreach (var link in _links)
                {
                    link.ResolvedType ??= _resolvedType;
                }
            }
        }

        private readonly HashSet<GenericTypeReference> _links = [];

        public void Link(GenericTypeReference other)
        {
            _links.Add(other);
            other._links.Add(this);
        }

        public bool Equals(GenericTypeReference? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return GenericName == other.GenericName && OwnerType.Equals(other.OwnerType);
        }

        private ITypeReference? GetConcreteTypeReference()
        {
            return ResolvedType switch
            {
                null => null,
                GenericTypeReference genericTypeReference => genericTypeReference.GetConcreteTypeReference(),
                _ => ResolvedType
            };
        }
        
        public override string ToString()
        {
            var sb = new StringBuilder($"{GenericName}=[");
            sb.Append(GetConcreteTypeReference()?.ToString() ?? "??");
            sb.Append(']');

            return sb.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((GenericTypeReference)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GenericName, OwnerType);
        }

        public static bool operator ==(GenericTypeReference? left, GenericTypeReference? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GenericTypeReference? left, GenericTypeReference? right)
        {
            return !Equals(left, right);
        }
    }

    public class InstantiatedFunction : ITypeReference
    {
        public InstantiatedFunction(
            FunctionSignature signature,
            IReadOnlyList<GenericTypeReference> typeArguments,
            IReadOnlyList<GenericTypeReference> ownerTypeArguments)
        {
            Signature = signature;
            // todo: don't think this model is going to work. Need to be able to access multiple layers of generic arguments
            TypeArguments = typeArguments;
            Arguments = new OrderedDictionary<string, FunctionArgument>();
            for (var i = 0; i < signature.Arguments.Count; i++)
            {
                var argument = signature.Arguments.GetAt(i);
                Arguments.Add(argument.Key, argument.Value with
                {
                    Type = argument.Value.Type switch
                    {
                        GenericTypeReference genericTypeReference => typeArguments.FirstOrDefault(y =>
                                                                         y.GenericName == genericTypeReference
                                                                             .GenericName)
                                                                     ?? ownerTypeArguments.First(y =>
                                                                         y.GenericName == genericTypeReference
                                                                             .GenericName),
                        _ => argument.Value.Type
                    }
                });
            }
            ReturnType = signature.ReturnType switch
            {
                GenericTypeReference genericTypeReference => typeArguments.FirstOrDefault(y =>
                                                                 y.GenericName == genericTypeReference.GenericName)
                                                             ?? ownerTypeArguments.First(y =>
                                                                 y.GenericName == genericTypeReference.GenericName),
                _ => signature.ReturnType
            };
        }

        private FunctionSignature Signature { get; }

        public bool IsStatic => Signature.IsStatic;

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ITypeReference ReturnType { get; }

        public OrderedDictionary<string, FunctionArgument> Arguments { get; }
    }

    public class InstantiatedClass : ITypeReference
    {
        public InstantiatedClass(ClassSignature signature, IReadOnlyList<GenericTypeReference> typeArguments)
        {
            Signature = signature;
            TypeArguments = typeArguments;

            Fields =
            [
                ..signature.Fields.Select(x => new TypeField
                {
                    Name = x.Name,
                    IsMutable = x.IsMutable,
                    IsPublic = x.IsPublic,
                    Type = x.Type switch
                    {
                        GenericTypeReference genericTypeReference => typeArguments.First(y =>
                            y.GenericName == genericTypeReference.GenericName),
                        var type => type
                    }
                })
            ];
            StaticFields =
            [
                ..signature.StaticFields.Select(x => new TypeField
                {
                    Name = x.Name,
                    IsMutable = x.IsMutable,
                    IsPublic = x.IsPublic,
                    Type = x.Type switch
                    {
                        GenericTypeReference genericTypeReference => typeArguments.First(y =>
                            y.GenericName == genericTypeReference.GenericName),
                        var type => type
                    }
                })
            ];
        }

        public static InstantiatedClass String { get; } = new (ClassSignature.String, []);
        public static InstantiatedClass Boolean { get; } = new(ClassSignature.Boolean, []);

        public static InstantiatedClass Int { get; } = new(ClassSignature.Int, []);

        public static InstantiatedClass Unit { get; } = new(ClassSignature.Unit, []);

        public static InstantiatedClass Never { get; } = new(ClassSignature.Never, []);

        // todo: be consistent with argument/parameter
        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ClassSignature Signature { get; }

        public IReadOnlyList<TypeField> Fields { get; }
        public IReadOnlyList<TypeField> StaticFields { get; }

        

        

        public bool IsSameSignature(InstantiatedClass other)
        {
            return Signature == other.Signature;
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Signature.Name}");
            if (TypeArguments.Count > 0)
            {
                sb.Append('<');
                sb.AppendJoin(",", TypeArguments.Select(x => x));
                sb.Append('>');
            }

            return sb.ToString();
        }

        public bool MatchesSignature(ClassSignature currentTypeSignature)
        {
            return Signature == currentTypeSignature;
        }
    }

    public class InstantiatedUnion : ITypeReference
    {
        public InstantiatedUnion(UnionSignature signature, IReadOnlyList<GenericTypeReference> typeArguments)
        {
            Signature = signature;
            TypeArguments = typeArguments;

            Variants =
            [
                ..signature.Variants.Select(x => x switch
                {
                    TupleUnionVariant tuple => new TupleUnionVariant
                    {
                        Name = tuple.Name,
                        TupleMembers =
                        [
                            ..tuple.TupleMembers.Select(y => y switch
                            {
                                GenericTypeReference genericTypeReference => typeArguments.First(z =>
                                    z.GenericName == genericTypeReference.GenericName),
                                _ => y
                            })
                        ]
                    },
                    ClassUnionVariant classVariant => new ClassUnionVariant
                    {
                        Name = classVariant.Name,
                        Fields =
                        [
                            ..classVariant.Fields.Select(y => new TypeField
                            {
                                Name = y.Name,
                                IsMutable = y.IsMutable,
                                IsPublic = y.IsPublic,
                                Type = y.Type switch
                                {
                                    GenericTypeReference genericTypeReference => typeArguments.First(z =>
                                        z.GenericName == genericTypeReference.GenericName),
                                    _ => y.Type
                                }
                            })
                        ]
                    },
                    _ => x
                })
            ];
        }

        private UnionSignature Signature { get; }

        public IReadOnlyList<IUnionVariant> Variants { get; }

        public string Name => Signature.Name;

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }

        

        public bool IsSameSignature(InstantiatedUnion other)
        {
            return Signature == other.Signature;
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Signature.Name}");
            if (TypeArguments.Count > 0)
            {
                sb.Append('<');
                sb.AppendJoin(",", TypeArguments.Select(x => x));
                sb.Append('>');
            }

            return sb.ToString();
        }
    }

    // todo: name
    public interface ITypeSignature
    {
        string Name { get; }
    }

    private InstantiatedFunction InstantiateFunction(FunctionSignature signature,
        IReadOnlyList<GenericTypeReference>? ownerTypeArguments)
    {
        ownerTypeArguments ??= [];
        GenericTypeReference[] typeArguments =
        [
            ..signature.GenericParameters.Select(x => new GenericTypeReference
            {
                GenericName = x.GenericName,
                OwnerType = signature
            })
        ];
        
        return new InstantiatedFunction(signature, typeArguments, ownerTypeArguments);
    }

    private InstantiatedUnion InstantiateUnion(UnionSignature signature, IReadOnlyList<(ITypeReference, SourceRange)>? typeReferences)
    {
        // when instantiating, create new generic type references so they can be resolved
        GenericTypeReference[] typeArguments =
        [
            ..signature.GenericParameters.Select(x => new GenericTypeReference
            {
                GenericName = x.GenericName,
                OwnerType = signature
            })
        ];

        if (typeReferences is not null)
        {
            if (typeReferences.Count != signature.GenericParameters.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {signature.GenericParameters.Count} type parameters, but found {typeReferences.Count}");
            }

            for (var i = 0; i < typeReferences.Count; i++)
            {
                var (typeReference, sourceRange) = typeReferences[i];

                ExpectType(typeReference, typeArguments[i], [], sourceRange);
            }
        }

        return new InstantiatedUnion(signature, typeArguments);
    }
    
    private InstantiatedUnion InstantiateResult()
    {
        return InstantiateUnion(UnionSignature.Result, null);
    }
    
    private InstantiatedClass InstantiateTuple(IReadOnlyList<(ITypeReference, SourceRange)> types)
    {
        return types.Count switch
        {
            0 => throw new InvalidOperationException("Tuple must not be empty"),
            > 10 => throw new InvalidOperationException("Tuple can contain at most 10 items"),
            _ => InstantiateClass(ClassSignature.Tuple([..types.Select(x => x.Item1)]), types)
        };
    }
    
    private bool TryInstantiateClassFunction(InstantiatedClass @class, string functionName,
        [NotNullWhen(true)] out InstantiatedFunction? function)
    {
        var signature = @class.Signature.Functions.FirstOrDefault(x => x.Name == functionName);

        if (signature is null)
        {
            function = null;
            return false;
        }

        function = InstantiateFunction(signature, @class.TypeArguments);
        return true;
    }

    private InstantiatedClass InstantiateClass(ClassSignature signature, IReadOnlyList<(ITypeReference, SourceRange)>? typeReferences)
    {
        GenericTypeReference[] typeArguments =
        [
            ..signature.GenericParameters.Select(x => new GenericTypeReference
            {
                GenericName = x.GenericName,
                OwnerType = signature
            })
        ];

        if (typeReferences is not null)
        {
            if (typeReferences.Count != signature.GenericParameters.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {signature.GenericParameters.Count} type parameters, but found {typeReferences.Count}");
            }

            for (var i = 0; i < typeReferences.Count; i++)
            {
                var (typeReference, sourceRange) = typeReferences[i];
                ExpectType(typeReference, typeArguments[i], [], sourceRange);
            }
        }

        return new InstantiatedClass(signature, typeArguments);
    }
    
    public class FunctionSignature(
        string name,
        IReadOnlyList<GenericTypeReference> genericParameters,
        OrderedDictionary<string, FunctionArgument> arguments,
        bool isStatic) : ITypeSignature
    {
        public bool IsStatic { get; } = isStatic;
        public IReadOnlyList<GenericTypeReference> GenericParameters { get; } = genericParameters;
        public OrderedDictionary<string, FunctionArgument> Arguments { get; } = arguments;

        // mutable due to setting up signatures and generic stuff
        public required ITypeReference ReturnType { get; set; }
        public string Name { get; } = name;
    }

    public record FunctionArgument(string Name, ITypeReference Type, bool Mutable);

    public class UnionSignature : ITypeSignature
    {
        public static readonly IReadOnlyList<ITypeSignature> BuiltInTypes;

        static UnionSignature()
        {
            var variants = new TupleUnionVariant[2];
            var genericParameters = new GenericTypeReference[2];
            var resultSignature = new UnionSignature
            {
                GenericParameters = genericParameters,
                Name = "result",
                Variants = variants,
                Functions = []
            };

            genericParameters[0] = new GenericTypeReference
            {
                GenericName = "TValue",
                OwnerType = resultSignature
            };
            genericParameters[1] = new GenericTypeReference
            {
                GenericName = "TError",
                OwnerType = resultSignature
            };

            variants[0] = new TupleUnionVariant
            {
                Name = "Ok",
                TupleMembers = [genericParameters[0]]
            };
            variants[1] = new TupleUnionVariant
            {
                Name = "Error",
                TupleMembers = [genericParameters[1]]
            };

            Result = resultSignature;
            BuiltInTypes = [Result];
        }

        public static UnionSignature Result { get; }
        public required IReadOnlyList<GenericTypeReference> GenericParameters { get; init; }
        public required IReadOnlyList<IUnionVariant> Variants { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }

        public required string Name { get; init; }
    }

    public interface IUnionVariant
    {
        string Name { get; }
    }

    // todo: better names
    private class TupleUnionVariant : IUnionVariant
    {
        public required IReadOnlyList<ITypeReference> TupleMembers { get; init; }
        public required string Name { get; init; }
    }

    private class ClassUnionVariant : IUnionVariant
    {
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required string Name { get; init; }
    }

    private class NoMembersUnionVariant : IUnionVariant
    {
        public required string Name { get; init; }
    }

    public class ClassSignature : ITypeSignature
    {
        private static readonly Dictionary<int, string> TupleFieldNames = new()
        {
            { 0, "First" },
            { 1, "Second" },
            { 2, "Third" },
            { 3, "Fourth" },
            { 4, "Fifth" },
            { 5, "Sixth" },
            { 6, "Seventh" },
            { 7, "Eighth" },
            { 8, "Ninth" },
            { 9, "Tenth" }
        };

        public static ClassSignature Unit { get; } = new()
            { GenericParameters = [], Name = "Unit", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature String { get; } = new()
            { GenericParameters = [], Name = "string", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature Int { get; } = new()
            { GenericParameters = [], Name = "int", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature Boolean { get; } = new()
            { GenericParameters = [], Name = "bool", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature Never { get; } = new()
            { GenericParameters = [], Name = "!", Fields = [], StaticFields = [], Functions = [] };

        public static IEnumerable<ITypeSignature> BuiltInTypes { get; } = [Unit, String, Int, Never, Boolean];

        public required IReadOnlyList<GenericTypeReference> GenericParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<TypeField> StaticFields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required string Name { get; init; }

        public static ClassSignature Tuple(IReadOnlyList<ITypeReference> elements)
        {
            var genericParameters = new List<GenericTypeReference>(elements.Count);
            var signature = new ClassSignature
            {
                GenericParameters = genericParameters,
                Name = $"Tuple`{elements.Count}",
                Fields =
                [
                    ..elements.Select((type, i) => new TypeField
                    {
                        // todo: verify this
                        IsMutable = false,
                        Name = TupleFieldNames.TryGetValue(i, out var name)
                            ? name
                            : throw new InvalidOperationException("Tuple can only contain at most 10 elements"),
                        Type = type,
                        IsPublic = true
                    })
                ],
                Functions = [],
                StaticFields = []
            };
            genericParameters.AddRange(Enumerable.Range(0, elements.Count).Select(x => new GenericTypeReference
            {
                GenericName = $"T{x}",
                OwnerType = signature
            }));

            return signature;
        }
        
        // todo: namespaces
    }

    public class TypeField
    {
        public required ITypeReference Type { get; init; }
        public required string Name { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
    }
}