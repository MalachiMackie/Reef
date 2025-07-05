using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NewLang.Core;

public class TypeChecker
{
    private readonly LangProgram _program;
    private readonly List<TypeCheckerError> _errors = [];

    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new();

    private readonly Dictionary<string, ITypeSignature> _types = ClassSignature.BuiltInTypes
        .Concat(UnionSignature.BuiltInTypes)
        .ToDictionary(x => x.Name);

    private TypeChecker(LangProgram program)
    {
        _program = program;
    }

    private Dictionary<string, FunctionSignature> ScopedFunctions => _typeCheckingScopes.Peek().Functions;
    private HashSet<GenericTypeReference> GenericPlaceholders => _typeCheckingScopes.Peek().GenericPlaceholders;
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

    private IEnumerable<Variable> GetScopedVariables()
    {
        return _typeCheckingScopes.Peek().GetVariables();
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

    private ScopeDisposable PushScope(
        ITypeSignature? currentTypeSignature = null,
        FunctionSignature? currentFunctionSignature = null,
        ITypeReference? expectedReturnType = null,
        IEnumerable<GenericTypeReference>? genericPlaceholders = null)
    {
        var currentScope = _typeCheckingScopes.Peek();

        _typeCheckingScopes.Push(new TypeCheckingScope(
            currentScope,
            new Dictionary<string, FunctionSignature>(currentScope.Functions),
            expectedReturnType ?? currentScope.ExpectedReturnType,
            currentTypeSignature ?? currentScope.CurrentTypeSignature,
            currentFunctionSignature ?? currentScope.CurrentFunctionSignature,
            [..currentScope.GenericPlaceholders, ..genericPlaceholders ?? []]));

        return new ScopeDisposable(PopScope);
    }

    private void PopScope() => _typeCheckingScopes.Pop();

    private void TypeCheckInner()
    {
        // initial scope
        _typeCheckingScopes.Push(new TypeCheckingScope(
            null,
            new Dictionary<string, FunctionSignature>(),
            InstantiatedClass.Unit,
            null,
            null,
            []));

        var (classes, unions) = SetupSignatures();

        foreach (var (union, unionSignature) in unions)
        {
            using var _ = PushScope(unionSignature, genericPlaceholders: unionSignature.TypeParameters);
            
            foreach (var function in union.Functions)
            {
                var fnSignature = unionSignature.Functions.First(x => x.Name == function.Name.StringValue);

                TypeCheckFunctionBody(function, fnSignature);
            }
        }

        foreach (var (@class, classSignature) in classes)
        {
            using var _ = PushScope(genericPlaceholders: classSignature.TypeParameters);

            var instanceFieldVariables = new List<Variable>();
            var staticFieldVariables = new List<Variable>();

            foreach (var field in @class.Fields)
            {
                var isStatic = field.StaticModifier is not null;

                var fieldTypeReference = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type);

                if (isStatic)
                {
                    // todo: static constructor?
                    if (field.InitializerValue is null)
                    {
                        throw new InvalidOperationException("Expected field initializer for static field");
                    }

                    var valueType = TypeCheckExpression(field.InitializerValue);

                    ExpectType(valueType, fieldTypeReference, field.InitializerValue.SourceRange);

                    staticFieldVariables.Add(new Variable(
                        field.Name,
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
                        field.Name,
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
                    AddScopedVariable(variable.Name.StringValue, variable);
                }

                foreach (var function in @class.Functions.Where(x => x.StaticModifier is not null))
                {
                    var fnSignature = classSignature.Functions.First(x => x.Name == function.Name.StringValue);

                    TypeCheckFunctionBody(function, fnSignature);
                }
            }

            // instance functions
            using (PushScope(classSignature))
            {
                // instance functions have access to both instance and static fields
                foreach (var variable in instanceFieldVariables.Concat(staticFieldVariables))
                {
                    AddScopedVariable(variable.Name.StringValue, variable);
                }

                foreach (var function in @class.Functions.Where(x => x.StaticModifier is null))
                {
                    var fnSignature = classSignature.Functions.First(x => x.Name == function.Name.StringValue);

                    TypeCheckFunctionBody(function, fnSignature);
                }
            }
        }

        foreach (var functionSignature in ScopedFunctions.Values)
        {
            var function = _program.Functions.First(x => x.Name.StringValue == functionSignature.Name);
            TypeCheckFunctionBody(function, functionSignature);
        }

        foreach (var expression in _program.Expressions)
        {
            TypeCheckExpression(expression);
        }

        PopScope();

        if (_errors.Count == 0)
        {
            _errors.AddRange(ResolvedTypeChecker.CheckAllExpressionsHaveResolvedTypes(_program));
        }
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
            var typeParameters = new List<GenericTypeReference>(union.TypeParameters.Count);
            var unionSignature = new UnionSignature
            {
                Name = union.Name.StringValue,
                TypeParameters = typeParameters,
                Functions = functions,
                Variants = variants
            };

            union.Signature = unionSignature;

            if (union.TypeParameters.GroupBy(x => x.StringValue).Any(x => x.Count() > 1))
            {
                throw new InvalidOperationException("Duplicate type parameter");
            }

            typeParameters.AddRange(union.TypeParameters.Select(typeParameter => new GenericTypeReference
                { GenericName = typeParameter.StringValue, OwnerType = unionSignature }));

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
            var typeParameters = new List<GenericTypeReference>(@class.TypeParameters.Count);
            var signature = new ClassSignature
            {
                Name = name,
                TypeParameters = typeParameters,
                Functions = functions,
                Fields = fields,
                StaticFields = staticFields
            };
            typeParameters.AddRange(@class.TypeParameters.Select(typeParameter => new GenericTypeReference
                { GenericName = typeParameter.StringValue, OwnerType = signature }));

            @class.Signature = signature;
            
            var typeParametersLookup = @class.TypeParameters.ToLookup(x => x.StringValue);

            foreach (var grouping in typeParametersLookup)
            {
                foreach (var typeParameter in grouping.Skip(1))
                {
                    _errors.Add(TypeCheckerError.DuplicateTypeParameter(typeParameter));
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
            using var _ = PushScope(
                unionSignature,
                genericPlaceholders: unionSignature.TypeParameters);

            foreach (var function in union.Functions)
            {
                if (functions.Any(x => x.Name == function.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.ConflictingFunctionName(function.Name));
                }
                
                functions.Add(TypeCheckFunctionSignature(function));
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
                    return new TupleUnionVariant
                    {
                        Name = variant.Name.StringValue,
                        TupleMembers =
                            [..tupleVariant.TupleMembers.Select(GetTypeReference)]
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
                            Type = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type),
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
            using var _ = PushScope(classSignature, genericPlaceholders: classSignature.TypeParameters);

            foreach (var fn in @class.Functions)
            {
                if (functions.Any(x => x.Name == fn.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.ConflictingFunctionName(fn.Name));
                }
                
                // todo: function overloading
                functions.Add(TypeCheckFunctionSignature(fn));
            }

            foreach (var field in @class.Fields)
            {
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type),
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
            if (!ScopedFunctions.TryAdd(name, TypeCheckFunctionSignature(fn)))
            {
                _errors.Add(TypeCheckerError.ConflictingFunctionName(fn.Name));
            }
        }

        foreach (var typeParameter in _program.Classes.SelectMany(x => x.TypeParameters)
                     .Concat(_program.Unions.SelectMany(x => x.TypeParameters))
                     .Where(x => _types.ContainsKey(x.StringValue)))
        {
            _errors.Add(TypeCheckerError.TypeParameterConflictsWithType(typeParameter));
        }

        return (
            classes.Select(x => (x.Item1, x.Item2)).ToList(),
            unions.Select(x => (x.Item1, x.Item2)).ToList()
        );
    }


    private void TypeCheckFunctionBody(LangFunction function,
        FunctionSignature fnSignature)
    {
        var functionType = InstantiateFunction(fnSignature, [..GenericPlaceholders]);

        using var _ = PushScope(null, fnSignature, fnSignature.ReturnType, genericPlaceholders: functionType.TypeArguments);
        foreach (var parameter in fnSignature.Parameters.Values)
        {
            AddScopedVariable(parameter.Name.StringValue, new Variable(
                parameter.Name,
                parameter.Type,
                true,
                parameter.Mutable));
        }

        TypeCheckBlock(function.Block);
    }

    private FunctionSignature TypeCheckFunctionSignature(LangFunction fn)
    {
        var parameters = new OrderedDictionary<string, FunctionParameter>();

        var name = fn.Name.StringValue;
        var typeParameters = new List<GenericTypeReference>(fn.TypeParameters.Count);
        var fnSignature = new FunctionSignature(
            name,
            typeParameters,
            parameters,
            fn.StaticModifier is not null)
        {
            ReturnType = null!
        };

        fn.Signature = fnSignature;

        var foundTypeParameters = new HashSet<string>();
        var genericPlaceholdersDictionary = GenericPlaceholders.ToDictionary(x => x.GenericName);
        foreach (var typeParameter in fn.TypeParameters)
        {
            if (!foundTypeParameters.Add(typeParameter.StringValue))
            {
                _errors.Add(TypeCheckerError.DuplicateTypeParameter(typeParameter));
            }

            if (genericPlaceholdersDictionary.ContainsKey(typeParameter.StringValue))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeParameter(typeParameter));
            }
            
            if (_types.ContainsKey(typeParameter.StringValue))
            {
                _errors.Add(TypeCheckerError.TypeParameterConflictsWithType(typeParameter));
            }
            typeParameters.Add(new GenericTypeReference
            {
                GenericName = typeParameter.StringValue,
                OwnerType = fnSignature
            });
        }

        var functionType = InstantiateFunction(fnSignature, [..GenericPlaceholders]);
        using var _ = PushScope(genericPlaceholders: functionType.TypeArguments);

        fnSignature.ReturnType = fn.ReturnType is null
            ? InstantiatedClass.Unit
            : GetTypeReference(fn.ReturnType);

        foreach (var parameter in fn.Parameters)
        {
            var paramName = parameter.Identifier;
            var type = parameter.Type is null ? UnknownType.Instance : GetTypeReference(parameter.Type);
            
            if (!parameters.TryAdd(paramName.StringValue, new FunctionParameter(paramName, type, parameter.MutabilityModifier is not null)))
            {
                _errors.Add(TypeCheckerError.DuplicateFunctionParameter(parameter.Identifier, fn.Name));
            }
        }

        // todo: function overloading
        return fnSignature;
    }

    private InstantiatedClass TypeCheckBlock(
        Block block)
    {
        using var _ = PushScope();

        foreach (var fn in block.Functions)
        {
            ScopedFunctions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn);
        }

        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, ScopedFunctions[fn.Name.StringValue]);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression);
        }

        // todo: tail expressions
        return InstantiatedClass.Unit;
    }

    private ITypeReference TypeCheckExpression(
        IExpression expression,
        bool allowUninstantiatedVariable = false)
    {
        var expressionType = expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression,
                allowUninstantiatedVariable),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression),
            BinaryOperatorExpression binaryOperatorExpression => TypeCheckBinaryOperatorExpression(
                binaryOperatorExpression.BinaryOperator),
            ObjectInitializerExpression objectInitializerExpression => TypeCheckObjectInitializer(
                objectInitializerExpression),
            MemberAccessExpression memberAccessExpression => TypeCheckMemberAccess(memberAccessExpression),
            StaticMemberAccessExpression staticMemberAccessExpression => TypeCheckStaticMemberAccess(
                staticMemberAccessExpression),
            GenericInstantiationExpression genericInstantiationExpression => TypeCheckGenericInstantiation(
                genericInstantiationExpression),
            UnaryOperatorExpression unaryOperatorExpression => TypeCheckUnaryOperator(
                unaryOperatorExpression.UnaryOperator),
            UnionStructVariantInitializerExpression unionStructVariantInitializerExpression =>
                TypeCheckUnionStructInitializer(
                    unionStructVariantInitializerExpression.UnionInitializer),
            MatchesExpression matchesExpression => TypeCheckMatchesExpression(
                matchesExpression),
            TupleExpression tupleExpression => TypeCheckTupleExpression(tupleExpression),
            MatchExpression matchExpression => TypeCheckMatchExpression(matchExpression),
            _ => throw new UnreachableException($"{expression.ExpressionType}")
        };

        expression.ResolvedType = expressionType;

        return expressionType;
    }

    private ITypeReference TypeCheckTupleExpression(TupleExpression tuple)
    {
        if (tuple.Values.Count == 1)
        {
            return TypeCheckExpression(tuple.Values[0]);
        }

        var types = tuple.Values.Select(value => (TypeCheckExpression(value), value.SourceRange)).ToArray();

        return InstantiateTuple(types, tuple.SourceRange);
    }

    private ITypeReference TypeCheckMatchExpression(MatchExpression matchExpression)
    {
        var valueType = TypeCheckExpression(matchExpression.Value);

        ITypeReference? foundType = null;

        foreach (var arm in matchExpression.Arms)
        {
            var patternVariables = TypeCheckPattern(valueType, arm.Pattern);

            using var _ = PushScope();
            foreach (var variable in patternVariables)
            {
                GetScopedVariable(variable).Instantiated = true;
            }

            var armType = arm.Expression is null
                ? UnknownType.Instance
                : TypeCheckExpression(arm.Expression);
            
            foundType ??= armType;

            ExpectExpressionType(foundType, arm.Expression);

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

        return foundDiscardPattern
               || (type is InstantiatedUnion { Variants: var unionVariants }
                   && !unionVariants.Select(x => x.Name).Except(matchedVariants).Any());
    }

    private InstantiatedClass TypeCheckMatchesExpression(MatchesExpression matchesExpression)
    {
        var valueType = TypeCheckExpression(matchesExpression.ValueExpression);

        if (matchesExpression.Pattern is not null)
        {
            matchesExpression.DeclaredVariables =
                TypeCheckPattern(valueType, matchesExpression.Pattern);
        }

        return InstantiatedClass.Boolean;
    }

    private List<string> TypeCheckPattern(ITypeReference typeReference, IPattern pattern)
    {
        var patternVariables = new List<string>();
        switch (pattern)
        {
            case DiscardPattern:
                // discard pattern always type checks
                break;
            case UnionVariantPattern variantPattern:
            {
                var patternUnionType = GetTypeReference(variantPattern.Type);

                if (patternUnionType is not InstantiatedUnion union)
                {
                    throw new InvalidOperationException($"{patternUnionType} is not a union");
                }

                ExpectType(typeReference, union, variantPattern.SourceRange);

                if (variantPattern.VariantName is not null)
                {
                    if (union.Variants.All(x => x.Name != variantPattern.VariantName.StringValue))
                    {
                        _errors.Add(TypeCheckerError.UnknownVariant(variantPattern.VariantName, union.Name));
                        break;
                    }
                }

                if (variantPattern.VariableName is {} variableName)
                {
                    patternVariables.Add(variableName.StringValue);
                    var variable = new Variable(
                        variableName,
                        patternUnionType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName.StringValue, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case ClassPattern classPattern:
            {
                var patternType = GetTypeReference(classPattern.Type);

                ExpectType(patternType, typeReference, classPattern.SourceRange);

                if (classPattern.FieldPatterns.Count > 0)
                {
                    if (patternType is not InstantiatedClass classType)
                    {
                        _errors.Add(TypeCheckerError.NonClassUsedInClassPattern(classPattern.Type));
                        break;
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
                        var fieldType = GetClassField(classType, fieldName);
                        
                        if (fieldPattern is null)
                        {
                            patternVariables.Add(fieldName.StringValue);
                            var variable = new Variable(
                                fieldName,
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
                            patternVariables.AddRange(TypeCheckPattern(fieldType, fieldPattern));
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

                if (classPattern.VariableName is {} variableName)
                {
                    patternVariables.Add(variableName.StringValue);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName.StringValue, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case UnionStructVariantPattern structVariantPattern:
            {
                var patternType = GetTypeReference(structVariantPattern.Type);

                ExpectType(patternType, typeReference, pattern.SourceRange);

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
                    _errors.Add(TypeCheckerError.MissingFieldsInStructVariantUnionPattern(
                        structVariantPattern,
                        structVariant.Fields.Select(x => x.Name).Except(structVariantPattern.FieldPatterns.Select(x => x.FieldName.StringValue))));
                }

                foreach (var (fieldName, fieldPattern) in structVariantPattern.FieldPatterns)
                {
                    var fieldType = GetUnionStructVariantField(structVariant, fieldName.StringValue);

                    if (fieldPattern is null)
                    {
                        patternVariables.Add(fieldName.StringValue);

                        var variable = new Variable(
                            fieldName,
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
                        patternVariables.AddRange(TypeCheckPattern(fieldType, fieldPattern));
                    }
                }

                if (structVariantPattern.VariableName is {} variableName)
                {
                    patternVariables.Add(variableName.StringValue);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName.StringValue, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case UnionTupleVariantPattern unionTupleVariantPattern:
            {
                var patternType = GetTypeReference(unionTupleVariantPattern.Type);

                ExpectType(patternType, typeReference, pattern.SourceRange);

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
                    _errors.Add(TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(
                        unionTupleVariantPattern, tupleUnionVariant.TupleMembers.Count));
                }
                
                foreach (var (tupleMemberType, tupleMemberPattern) in tupleUnionVariant.TupleMembers.Zip(
                             unionTupleVariantPattern.TupleParamPatterns))
                {
                    patternVariables.AddRange(
                        TypeCheckPattern(tupleMemberType, tupleMemberPattern));
                }

                if (unionTupleVariantPattern.VariableName is {} variableName)
                {
                    patternVariables.Add(variableName.StringValue);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        false,
                        false);
                    if (!TryAddScopedVariable(variableName.StringValue, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }

                break;
            }
            case VariableDeclarationPattern { VariableName: var variableName }:
            {
                patternVariables.Add(variableName.StringValue);
                var variable = new Variable(
                    variableName,
                    typeReference,
                    false,
                    false);
                if (!TryAddScopedVariable(variableName.StringValue, variable))
                {
                    throw new InvalidOperationException($"Duplicate variable {variableName}");
                }

                break;
            }
            case TypePattern { Type: var typeIdentifier, VariableName: {} variableName }:
            {
                var type = GetTypeReference(typeIdentifier);
                patternVariables.Add(variableName.StringValue);
                var variable = new Variable(variableName, type, Instantiated: false, Mutable: false);
                if (!TryAddScopedVariable(variableName.StringValue, variable))
                {
                    throw new InvalidOperationException($"Duplicate variable {variableName}");
                }

                break;
            }
            default:
                throw new UnreachableException(pattern.GetType().Name);
        }

        return patternVariables;
    }

    private ITypeReference TypeCheckUnionStructInitializer(UnionStructVariantInitializer initializer)
    {
        var type = GetTypeReference(initializer.UnionType);

        if (type is not InstantiatedUnion instantiatedUnion)
        {
            throw new InvalidOperationException($"{type} is not a union");
        }

        var variant =
            instantiatedUnion.Variants.FirstOrDefault(x => x.Name == initializer.VariantIdentifier.StringValue);

        if (variant is null)
        {
            _errors.Add(TypeCheckerError.UnknownVariant(initializer.VariantIdentifier, initializer.UnionType.Identifier.StringValue));
            return instantiatedUnion;
        }

        if (variant is not ClassUnionVariant classVariant)
        {
            _errors.Add(TypeCheckerError.UnionStructVariantInitializerNotStructVariant(initializer.VariantIdentifier));
            return instantiatedUnion;
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
            if (fieldInitializer.Value is not null)
                TypeCheckExpression(fieldInitializer.Value);
            
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                _errors.Add(TypeCheckerError.UnknownField(fieldInitializer.FieldName, $"union variant {initializer.UnionType.Identifier.StringValue}::{initializer.VariantIdentifier.StringValue}"));
                continue;
            }

            ExpectExpressionType(field.Type, fieldInitializer.Value);
        }

        return type;
    }

    private ITypeReference TypeCheckUnaryOperator(UnaryOperator unaryOperator)
    {
        return unaryOperator.OperatorType switch
        {
            UnaryOperatorType.FallOut => TypeCheckFallout(unaryOperator.Operand),
            UnaryOperatorType.Not => TypeCheckNot(unaryOperator.Operand),
            _ => throw new UnreachableException($"{unaryOperator.OperatorType}")
        };
    }

    private InstantiatedClass TypeCheckNot(IExpression? expression)
    {
        if (expression is not null)
            TypeCheckExpression(expression);

        ExpectExpressionType(InstantiatedClass.Boolean, expression);

        return InstantiatedClass.Boolean;
    }

    private GenericTypeReference TypeCheckFallout(IExpression? expression)
    {
        if (expression is not null)
            TypeCheckExpression(expression);

        // todo: could implement with an interface? union Result : IFallout?
        if (ExpectedReturnType is not InstantiatedUnion { Name: "result" or "option" } union)
        {
            throw new InvalidOperationException("Fallout operator is only valid for Result and Option return types");
        }

        ExpectExpressionType(ExpectedReturnType, expression);

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

    private InstantiatedFunction TypeCheckGenericInstantiation(
        GenericInstantiationExpression genericInstantiationExpression)
    {
        var genericInstantiation = genericInstantiationExpression.GenericInstantiation;
        var valueType = TypeCheckExpression(genericInstantiation.Value);

        if (valueType is not InstantiatedFunction instantiatedFunction)
        {
            throw new InvalidOperationException("Expected function");
        }

        if (genericInstantiation.TypeArguments.Count <= 0)
        {
            return instantiatedFunction;
        }

        if (genericInstantiation.TypeArguments.Count != instantiatedFunction.TypeArguments.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(
                genericInstantiationExpression.SourceRange,
                genericInstantiation.TypeArguments.Count,
                instantiatedFunction.TypeArguments.Count));
        }

        for (var i = 0; i < Math.Min(instantiatedFunction.TypeArguments.Count, genericInstantiation.TypeArguments.Count); i++)
        {
            var typeArgument = GetTypeReference(genericInstantiation.TypeArguments[i]);
            var typeParameter = instantiatedFunction.TypeArguments[i];
            ExpectType(typeArgument, typeParameter, genericInstantiation.TypeArguments[i].SourceRange);
        }

        return instantiatedFunction;
    }

    private ITypeReference TypeCheckMemberAccess(
        MemberAccessExpression memberAccessExpression)
    {
        var (ownerExpression, stringToken) = memberAccessExpression.MemberAccess;
        var ownerType = TypeCheckExpression(ownerExpression);

        if (ownerType is not InstantiatedClass classType)
        {
            // todo: generic parameter constraints with interfaces?
            _errors.Add(TypeCheckerError.MemberAccessOnGenericExpression(memberAccessExpression));
            return UnknownType.Instance;
        }

        if (stringToken is null)
        {
            return UnknownType.Instance;
        }

        return TryInstantiateClassFunction(classType, stringToken.StringValue, out var function)
            ? function
            : GetClassField(classType, stringToken);
    }

    private static ITypeReference GetUnionStructVariantField(ClassUnionVariant variant, string fieldName)
    {
        var fieldType = variant.Fields.FirstOrDefault(x => x.Name == fieldName)?.Type
                        ?? throw new InvalidOperationException($"No field named {fieldName}");

        return fieldType;
    }

    private ITypeReference GetClassField(InstantiatedClass classType, StringToken fieldName)
    {
        var field = classType.Fields.FirstOrDefault(x => x.Name == fieldName.StringValue)
                    ?? throw new InvalidOperationException($"No field named {fieldName}");

        if ((CurrentTypeSignature is not ClassSignature currentClassSignature
             || !classType.MatchesSignature(currentClassSignature))
            && !field.IsPublic)
        {
            _errors.Add(TypeCheckerError.PrivateFieldReferenced(fieldName));
        }

        var fieldType = field.Type;
        return fieldType;
    }

    private ITypeReference TypeCheckStaticMemberAccess(
        StaticMemberAccessExpression staticMemberAccessExpression)
    {
        var staticMemberAccess = staticMemberAccessExpression.StaticMemberAccess;
        var type = GetTypeReference(staticMemberAccess.Type);

        var memberName = staticMemberAccess.MemberName?.StringValue;
        if (memberName is null)
        {
            return UnknownType.Instance;
        }
        
        switch (type)
        {
            case InstantiatedClass { StaticFields: var staticFields } instantiatedClass:
            {
                var field = staticFields.FirstOrDefault(x => x.Name == memberName);
                if (field is not null)
                {
                    return field.Type;
                }

                if (!TryInstantiateClassFunction(instantiatedClass, memberName, out var function))
                {
                    throw new InvalidOperationException($"No member found with name {memberName}");
                }

                if (!function.IsStatic)
                {
                    throw new InvalidOperationException($"{memberName} is not static");
                }

                return function;

            }
            case InstantiatedUnion instantiatedUnion:
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
            case GenericTypeReference:
                _errors.Add(TypeCheckerError.StaticMemberAccessOnGenericReference(staticMemberAccessExpression));
                return UnknownType.Instance;
            default:
                throw new UnreachableException(type.GetType().ToString());
        }
    }

    private ITypeReference TypeCheckObjectInitializer(
        ObjectInitializerExpression objectInitializerExpression)
    {
        var objectInitializer = objectInitializerExpression.ObjectInitializer;
        var foundType = GetTypeReference(objectInitializer.Type);

        if (foundType is UnknownType)
        {
            // if we don't know what type this is, type check the field initializers anyway 
            foreach (var fieldInitializer in objectInitializer.FieldInitializers.Where(x => x.Value is not null))
            {
                TypeCheckExpression(fieldInitializer.Value!);
            }
            
            return UnknownType.Instance;
        }
        
        if (foundType is not InstantiatedClass instantiatedClass)
        {
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
            if (fieldInitializer.Value is not null)
            {
                TypeCheckExpression(fieldInitializer.Value);
            }
            
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                _errors.Add(TypeCheckerError.UnknownField(fieldInitializer.FieldName, $"class {objectInitializer.Type.Identifier.StringValue}"));
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

            ExpectExpressionType(field.Type, fieldInitializer.Value);
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
        BinaryOperator @operator)
    {
        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
            {
                if (@operator.Left is not null)
                    TypeCheckExpression(@operator.Left);
                if (@operator.Right is not null)
                    TypeCheckExpression(@operator.Right);
                ExpectExpressionType(InstantiatedClass.Int, @operator.Left);
                ExpectExpressionType(InstantiatedClass.Int, @operator.Right);

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
            {
                if (@operator.Left is not null)
                    TypeCheckExpression(@operator.Left);
                if (@operator.Right is not null)
                    TypeCheckExpression(@operator.Right);
                
                ExpectExpressionType(InstantiatedClass.Int, @operator.Left);
                ExpectExpressionType(InstantiatedClass.Int, @operator.Right);

                return InstantiatedClass.Int;
            }
            case BinaryOperatorType.EqualityCheck:
            {
                var leftType = @operator.Left is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Left);
                var rightType = @operator.Right is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Right);
                
                // todo: use interface. left and right implements IEquals<T>
                
                ExpectType(rightType, leftType, new SourceRange(@operator.OperatorToken.SourceSpan, @operator.OperatorToken.SourceSpan));

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.ValueAssignment:
            {
                var leftType = @operator.Left is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Left, true);
                var rightType = @operator.Right is null
                    ? UnknownType.Instance
                    : TypeCheckExpression(@operator.Right);
                
                if (@operator.Left is not null)
                {
                    ExpectAssignableExpression(@operator.Left);
                }

                if (@operator.Left is ValueAccessorExpression
                    {
                        ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken variableName }
                    } && GetScopedVariable(variableName.StringValue) is { Instantiated: false } variable)
                {
                    variable.Instantiated = true;
                    variable.Type ??= rightType;
                }

                ExpectExpressionType(leftType, @operator.Right);

                return leftType;
            }
            default:
                throw new UnreachableException(@operator.OperatorType.ToString());
        }
    }

    private bool ExpectAssignableExpression(IExpression expression, bool report = true)
    {
        switch (expression)
        {
            case ValueAccessorExpression
            {
                ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken valueToken }
            }:
            {
                var variable = GetScopedVariable(valueToken.StringValue);
                if (variable is not { Instantiated: true, Mutable: false })
                {
                    return true;
                }

                if (report)
                {
                    _errors.Add(TypeCheckerError.NonMutableAssignment(variable.Name.StringValue,
                        new SourceRange(valueToken.SourceSpan, valueToken.SourceSpan)));
                }
                return false;

            }
            case MemberAccessExpression memberAccess:
            {
                var owner = memberAccess.MemberAccess.Owner;

                if (memberAccess.MemberAccess.MemberName is null)
                {
                    return false;
                }
            
                var isOwnerAssignable = ExpectAssignableExpression(owner, report: false);

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

                if (isOwnerAssignable)
                {
                    return true;
                }

                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;

            }
            case StaticMemberAccessExpression staticMemberAccess:
            {
                var ownerType = GetTypeReference(staticMemberAccess.StaticMemberAccess.Type);

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

                if (staticField.IsMutable)
                {
                    return true;
                }

                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberAssignment(staticMemberAccess));
                return false;

            }
        }

        if (report)
            _errors.Add(TypeCheckerError.ExpressionNotAssignable(expression));
        
        return false;
    }

    private sealed class VariableIfInstantiation
    {
        public bool InstantiatedInBody { get; set; }
        public bool InstantiatedInElse { get; set; }
        public bool InstantiatedInEachElseIf { get; set; } = true;
    }

    private ITypeReference TypeCheckIfExpression(IfExpression ifExpression)
    {
        // scope around the entire if expression. Variables declared in the check expression (e.g. with matches) will be
        // conditionally available in the body
        using var _ = PushScope();
        
        TypeCheckExpression(ifExpression.CheckExpression);

        ExpectExpressionType(InstantiatedClass.Boolean, ifExpression.CheckExpression);

        IReadOnlyList<string> matchVariableDeclarations = [];

        if (ifExpression.CheckExpression is MatchesExpression { DeclaredVariables: var declaredVariables })
        {
            matchVariableDeclarations = declaredVariables;
        }

        var uninstantiatedVariables = GetScopedVariables().Where(x => !x.Instantiated)
            .ToDictionary(x => x, _ => new VariableIfInstantiation());

        foreach (var variable in matchVariableDeclarations)
        {
            GetScopedVariable(variable).Instantiated = true;
        }

        if (ifExpression.Body is not null)
        {
            TypeCheckExpression(ifExpression.Body);
        }
        
        foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
        {
            variableInstantiation.InstantiatedInBody = variable.Instantiated;
            variable.Instantiated = false;
        }

        foreach (var variable in matchVariableDeclarations)
        {
            GetScopedVariable(variable).Instantiated = false;
        }

        foreach (var elseIf in ifExpression.ElseIfs)
        {
            
            using var __ = PushScope();
            TypeCheckExpression(elseIf.CheckExpression);
            
            ExpectExpressionType(InstantiatedClass.Boolean, elseIf.CheckExpression);

            matchVariableDeclarations = elseIf.CheckExpression is MatchesExpression
            {
                DeclaredVariables: var elseIfDeclaredVariables
            }
                ? elseIfDeclaredVariables
                : [];

            foreach (var variable in matchVariableDeclarations)
            {
                GetScopedVariable(variable).Instantiated = true;
            }

            if (elseIf.Body is not null)
            {
                TypeCheckExpression(elseIf.Body);
            }
            
            foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
            {
                variableInstantiation.InstantiatedInEachElseIf &= variable.Instantiated;
                variable.Instantiated = false;
            }

            foreach (var variable in matchVariableDeclarations)
            {
                GetScopedVariable(variable).Instantiated = false;
            }
        }

        if (ifExpression.ElseBody is not null)
        {
            using var __ = PushScope();
            TypeCheckExpression(ifExpression.ElseBody);
            
            foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
            {
                variableInstantiation.InstantiatedInElse = variable.Instantiated;
                variable.Instantiated = false;
            }
        }
        
        foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
        {
            // if variable was instantiated in each branch, then it is instantiated
            variable.Instantiated = ifExpression.Body is not null && variableInstantiation.InstantiatedInBody
                                                                  && ifExpression.ElseBody is not null &&
                                                                      variableInstantiation.InstantiatedInElse
                                                                  && (ifExpression.ElseIfs.Count == 0 ||
                                                                      variableInstantiation.InstantiatedInEachElseIf);
        }

        // todo: tail expression
        return InstantiatedClass.Unit;
    }

    private ITypeReference TypeCheckMethodCall(
        MethodCallExpression methodCallExpression)
    {
        var methodCall = methodCallExpression.MethodCall;
        var methodType = TypeCheckExpression(methodCall.Method);

        if (methodType is UnknownType)
        {
            // type check arguments even if we don't know what the type is
            foreach (var argument in methodCall.ArgumentList)
            {
                TypeCheckExpression(argument);
            }
            
            return UnknownType.Instance;
        }

        if (methodType is not InstantiatedFunction functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (methodCall.ArgumentList.Count != functionType.Parameters.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfMethodArguments(
                methodCallExpression, functionType.Parameters.Count));

            foreach (var argument in methodCall.ArgumentList)
            {
                TypeCheckExpression(argument);
            }

            return functionType.ReturnType;
        }

        for (var i = 0; i < functionType.Parameters.Count; i++)
        {
            var (_, expectedParameterType, isParameterMutable) = functionType.Parameters.GetAt(i).Value;

            var argumentExpression = methodCall.ArgumentList[i];
            TypeCheckExpression(argumentExpression);

            ExpectExpressionType(expectedParameterType, argumentExpression);

            if (isParameterMutable)
            {
                ExpectAssignableExpression(argumentExpression);
            }
        }

        return functionType.ReturnType;
    }

    private InstantiatedClass TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression)
    {
        if (methodReturnExpression.MethodReturn.Expression is null)
        {
            // no inner expression to check the type of, but we know the type is unit
            ExpectType(InstantiatedClass.Unit, ExpectedReturnType,
                methodReturnExpression.SourceRange);
        }
        else
        {
            TypeCheckExpression(methodReturnExpression.MethodReturn.Expression);
            ExpectExpressionType(ExpectedReturnType, methodReturnExpression.MethodReturn.Expression);
        }

        return InstantiatedClass.Never;
    }

    private ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression,
        bool allowUninstantiatedVariables)
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
            { AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: "this" } thisToken} => TypeCheckThis(thisToken),
            { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo } => InstantiatedClass.Never,
            {
                    AccessType: ValueAccessType.Variable,
                    Token: StringToken { Type: TokenType.Identifier } variableNameToken
                } =>
                TypeCheckVariableAccess(variableNameToken, allowUninstantiatedVariables),
            _ => throw new UnreachableException($"{valueAccessorExpression}")
        };

        ITypeReference TypeCheckThis(StringToken thisToken)
        {
            if (CurrentTypeSignature is null || CurrentFunctionSignature is null)
            {
                _errors.Add(TypeCheckerError.ThisAccessedOutsideOfInstanceMethod(thisToken));
                return UnknownType.Instance;
            }

            if (CurrentFunctionSignature.IsStatic)
            {
                _errors.Add(TypeCheckerError.ThisAccessedOutsideOfInstanceMethod(thisToken));
            }

            return CurrentTypeSignature switch
            {
                UnionSignature unionSignature => InstantiateUnion(unionSignature, [], new SourceRange(thisToken.SourceSpan, thisToken.SourceSpan)),
                ClassSignature classSignature => InstantiateClass(classSignature, [], new SourceRange(thisToken.SourceSpan, thisToken.SourceSpan)),
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

            var instantiatedUnion = InstantiateResult(valueAccessorExpression.SourceRange);

            return GetTupleUnionFunction(tupleVariant, instantiatedUnion);
        }
    }

    private InstantiatedFunction GetTupleUnionFunction(TupleUnionVariant tupleVariant,
        InstantiatedUnion instantiatedUnion)
    {
        var parameters = new OrderedDictionary<string, FunctionParameter>();
        for (var i = 0; i < tupleVariant.TupleMembers.Count; i++)
        {
            var name = i.ToString();
            var member = tupleVariant.TupleMembers[i];
            // use default source span here because we don't actually have a source span
            var nameToken = Token.Identifier(name, SourceSpan.Default);
            parameters.Add(name, new FunctionParameter(nameToken, member, Mutable: false));
        }

        var signature = new FunctionSignature(
            tupleVariant.Name,
            [],
            parameters,
            true)
        {
            ReturnType = instantiatedUnion
        };

        return InstantiateFunction(signature, instantiatedUnion.TypeArguments);
    }

    private ITypeReference TypeCheckVariableAccess(
        StringToken variableName, bool allowUninstantiated)
    {
        if (ScopedFunctions.TryGetValue(variableName.StringValue, out var function))
        {
            return InstantiateFunction(function, [..GenericPlaceholders]);
        }

        if (!TryGetScopedVariable(variableName.StringValue, out var value))
        {
            _errors.Add(TypeCheckerError.SymbolNotFound(variableName));
            return UnknownType.Instance;
        }

        if (!allowUninstantiated && !value.Instantiated)
        {
            _errors.Add(TypeCheckerError.AccessUninitializedVariable(variableName));
        }

        return value.Type ?? UnknownType.Instance;
    }

    private InstantiatedClass TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression)
    {
        var varName = expression.VariableDeclaration.VariableNameToken;
        if (VariableIsDefined(varName.StringValue))
        {
            throw new InvalidOperationException(
                $"Variable with name {varName} already exists");
        }

        Variable? variable = null;
        switch (expression.VariableDeclaration)
        {
            case { Value: null, Type: null, MutabilityModifier: var mutModifier }:
            {
                variable = new Variable(
                    varName, null, Instantiated: false, Mutable: mutModifier is not null);
                break;
            }
            case { Value: { } value, Type: var type, MutabilityModifier: var mutModifier }:
            {
                var valueType = TypeCheckExpression(value);
                if (type is not null)
                {
                    var expectedType = GetTypeReference(type);

                    ExpectExpressionType(expectedType, value);
                }

                variable = new Variable(varName, valueType, true, mutModifier is not null);
                break;
            }
            case { Value: null, Type: { } type, MutabilityModifier: var mutModifier }:
            {
                var langType = GetTypeReference(type);
                variable = new Variable(varName, langType, false, mutModifier is not null);
                break;
            }
        }

        if (variable is not null)
        {
            AddScopedVariable(varName.StringValue, variable);
        }
        expression.VariableDeclaration.Variable = variable;

        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedClass.Unit;
    }

    private ITypeReference GetTypeReference(
        TypeIdentifier typeIdentifier)
    {
        var identifierName = typeIdentifier.Identifier.StringValue;
        
        if (_types.TryGetValue(identifierName, out var nameMatchingType))
        {
            switch (nameMatchingType)
            {
                case ClassSignature classSignature:
                    return InstantiateClass(classSignature, [
                        ..typeIdentifier.TypeArguments
                            .Select(x => (GetTypeReference(x), x.SourceRange))
                    ], typeIdentifier.SourceRange);
                case UnionSignature unionSignature:
                    return InstantiateUnion(unionSignature, [
                        ..typeIdentifier.TypeArguments
                            .Select(x => (GetTypeReference(x), x.SourceRange))
                    ], typeIdentifier.SourceRange);
            }
        }

        if (GenericPlaceholders.FirstOrDefault(x => x.GenericName == identifierName) is {} genericTypeReference)
        {
            return genericTypeReference;
        }
        
        _errors.Add(TypeCheckerError.SymbolNotFound(typeIdentifier.Identifier));
        return UnknownType.Instance;
    }

    private void ExpectExpressionType(ITypeReference expected, IExpression? actual)
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
                    expected, actual.SourceRange),
            _ => throw new UnreachableException(actual.GetType().ToString())
        };

        return;
        
        bool ExpectIfExpressionType(IfExpressionExpression ifExpression)
        {
            return ExpectType(ifExpression.ResolvedType!, expected, SourceRange.Default);
            // todo: tail expression
        }

        bool ExpectBlockExpressionType(BlockExpression blockExpression)
        {
            return ExpectType(blockExpression.ResolvedType!, expected, SourceRange.Default);
            // todo: tail expression
        }
    
        bool ExpectMatchExpressionType(MatchExpression matchExpression)
        {
            return ExpectType(matchExpression.ResolvedType!, expected, SourceRange.Default);
            // todo: tail expression
        }
    }

    private bool ExpectType(ITypeReference actual, ITypeReference expected,
        SourceRange actualSourceRange, bool reportError = true)
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
                    argumentsPassed &= ExpectType(actualClass.TypeArguments[i], expectedClass.TypeArguments[i], actualSourceRange, reportError: false);
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
                        actualSourceRange, reportError: false);
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
                    result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError);
                }
                else if (!GenericPlaceholders.Contains(generic))
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
                    result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError);
                }
                else if (!GenericPlaceholders.Contains(generic))
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
                    result &= ExpectType(@class, generic.ResolvedType, actualSourceRange, reportError);
                }
                else if (!GenericPlaceholders.Contains(generic))
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
                    result &= ExpectType(@class, generic.ResolvedType, actualSourceRange, reportError);
                }
                else if (!GenericPlaceholders.Contains(generic))
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
                    result &= ExpectType(genericTypeReference.ResolvedType, expectedGeneric.ResolvedType, actualSourceRange, reportError);
                }
                else if (genericTypeReference.ResolvedType is null && expectedGeneric.ResolvedType is not null)
                {
                    if (!GenericPlaceholders.Contains(genericTypeReference))
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
                    if (!GenericPlaceholders.Contains(genericTypeReference))
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
                    if (expectedGeneric != genericTypeReference && !GenericPlaceholders.Contains(expectedGeneric))
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
        FunctionSignature? CurrentFunctionSignature,
        HashSet<GenericTypeReference> GenericPlaceholders)
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

        public IEnumerable<Variable> GetVariables()
        {
            return [..CurrentScopeVariables.Values, ..ParentScope?.GetVariables() ?? []];
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

    public record Variable(StringToken Name, ITypeReference? Type, bool Instantiated, bool Mutable)
    {
        public bool Instantiated { get; set; } = Instantiated;
        public ITypeReference? Type { get; set; } = Type;
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

            return obj.GetType() == GetType() && Equals((GenericTypeReference)obj);
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
            IReadOnlyList<GenericTypeReference> ownerTypeParameters)
        {
            Signature = signature;
            TypeArguments = typeArguments;
            Parameters = new OrderedDictionary<string, FunctionParameter>();
            for (var i = 0; i < signature.Parameters.Count; i++)
            {
                var parameter = signature.Parameters.GetAt(i);
                Parameters.Add(parameter.Key, parameter.Value with
                {
                    Type = parameter.Value.Type switch
                    {
                        GenericTypeReference genericTypeReference => typeArguments.FirstOrDefault(y =>
                                                                         y.GenericName == genericTypeReference
                                                                             .GenericName)
                                                                     ?? ownerTypeParameters.First(y =>
                                                                         y.GenericName == genericTypeReference
                                                                             .GenericName),
                        _ => parameter.Value.Type
                    }
                });
            }
            ReturnType = signature.ReturnType switch
            {
                GenericTypeReference genericTypeReference => typeArguments.FirstOrDefault(y =>
                                                                 y.GenericName == genericTypeReference.GenericName)
                                                             ?? ownerTypeParameters.First(y =>
                                                                 y.GenericName == genericTypeReference.GenericName),
                _ => signature.ReturnType
            };
        }

        private FunctionSignature Signature { get; }

        public bool IsStatic => Signature.IsStatic;

        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ITypeReference ReturnType { get; }

        public OrderedDictionary<string, FunctionParameter> Parameters { get; }
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
            if (TypeArguments.Count <= 0)
            {
                return sb.ToString();
            }

            sb.Append('<');
            sb.AppendJoin(",", TypeArguments.Select(x => x));
            sb.Append('>');

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
            if (TypeArguments.Count <= 0)
            {
                return sb.ToString();
            }

            sb.Append('<');
            sb.AppendJoin(",", TypeArguments.Select(x => x));
            sb.Append('>');

            return sb.ToString();
        }
    }

    public interface ITypeSignature
    {
        string Name { get; }
    }

    private static InstantiatedFunction InstantiateFunction(FunctionSignature signature,
        IReadOnlyList<GenericTypeReference>? ownerTypeArguments)
    {
        ownerTypeArguments ??= [];
        GenericTypeReference[] typeArguments =
        [
            ..signature.TypeParameters.Select(x => new GenericTypeReference
            {
                GenericName = x.GenericName,
                OwnerType = signature
            })
        ];
        
        return new InstantiatedFunction(signature, typeArguments, ownerTypeArguments);
    }

    private InstantiatedUnion InstantiateUnion(UnionSignature signature, IReadOnlyList<(ITypeReference, SourceRange)> typeArguments, SourceRange sourceRange)
    {
        // when instantiating, create new generic type references so they can be resolved
        GenericTypeReference[] typeArgumentReferences =
        [
            ..signature.TypeParameters.Select(x => new GenericTypeReference
            {
                GenericName = x.GenericName,
                OwnerType = signature
            })
        ];

        if (typeArguments.Count <= 0)
        {
            return new InstantiatedUnion(signature, typeArgumentReferences);
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        for (var i = 0; i < Math.Min(typeArguments.Count, typeArgumentReferences.Length); i++)
        {
            var (typeArgument, referenceSourceRange) = typeArguments[i];

            ExpectType(typeArgument, typeArgumentReferences[i], referenceSourceRange);
        }

        return new InstantiatedUnion(signature, typeArgumentReferences);
    }
    
    private InstantiatedUnion InstantiateResult(SourceRange sourceRange)
    {
        return InstantiateUnion(UnionSignature.Result, [], sourceRange);
    }
    
    private InstantiatedClass InstantiateTuple(IReadOnlyList<(ITypeReference, SourceRange)> types, SourceRange sourceRange)
    {
        return types.Count switch
        {
            0 => throw new InvalidOperationException("Tuple must not be empty"),
            > 10 => throw new InvalidOperationException("Tuple can contain at most 10 items"),
            _ => InstantiateClass(ClassSignature.Tuple([..types.Select(x => x.Item1)]), types, sourceRange)
        };
    }
    
    private static bool TryInstantiateClassFunction(InstantiatedClass @class, string functionName,
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

    private InstantiatedClass InstantiateClass(ClassSignature signature, IReadOnlyList<(ITypeReference, SourceRange)> typeArguments, SourceRange sourceRange)
    {
        GenericTypeReference[] typeArgumentReferences =
        [
            ..signature.TypeParameters.Select(x => new GenericTypeReference
            {
                GenericName = x.GenericName,
                OwnerType = signature
            })
        ];

        if (typeArguments.Count <= 0)
        {
            return new InstantiatedClass(signature, typeArgumentReferences);
        }

        if (typeArguments.Count != signature.TypeParameters.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(sourceRange, typeArguments.Count, signature.TypeParameters.Count));
        }

        for (var i = 0; i < Math.Min(typeArguments.Count, typeArgumentReferences.Length); i++)
        {
            var (typeReference, referenceSourceRange) = typeArguments[i];
            ExpectType(typeReference, typeArgumentReferences[i], referenceSourceRange);
        }

        return new InstantiatedClass(signature, typeArgumentReferences);
    }
    
    public class FunctionSignature(
        string name,
        IReadOnlyList<GenericTypeReference> typeParameters,
        OrderedDictionary<string, FunctionParameter> parameters,
        bool isStatic) : ITypeSignature
    {
        public bool IsStatic { get; } = isStatic;
        public IReadOnlyList<GenericTypeReference> TypeParameters { get; } = typeParameters;
        public OrderedDictionary<string, FunctionParameter> Parameters { get; } = parameters;

        // mutable due to setting up signatures and generic stuff
        public required ITypeReference ReturnType { get; set; }
        public string Name { get; } = name;
    }

    public record FunctionParameter(StringToken Name, ITypeReference Type, bool Mutable);

    public class UnionSignature : ITypeSignature
    {
        public static readonly IReadOnlyList<ITypeSignature> BuiltInTypes;

        static UnionSignature()
        {
            var variants = new TupleUnionVariant[2];
            var typeParameters = new GenericTypeReference[2];
            var resultSignature = new UnionSignature
            {
                TypeParameters = typeParameters,
                Name = "result",
                Variants = variants,
                Functions = []
            };

            typeParameters[0] = new GenericTypeReference
            {
                GenericName = "TValue",
                OwnerType = resultSignature
            };
            typeParameters[1] = new GenericTypeReference
            {
                GenericName = "TError",
                OwnerType = resultSignature
            };

            variants[0] = new TupleUnionVariant
            {
                Name = "Ok",
                TupleMembers = [typeParameters[0]]
            };
            variants[1] = new TupleUnionVariant
            {
                Name = "Error",
                TupleMembers = [typeParameters[1]]
            };

            Result = resultSignature;
            BuiltInTypes = [Result];
        }

        public static UnionSignature Result { get; }
        public required IReadOnlyList<GenericTypeReference> TypeParameters { get; init; }
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
            { TypeParameters = [], Name = "Unit", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature String { get; } = new()
            { TypeParameters = [], Name = "string", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature Int { get; } = new()
            { TypeParameters = [], Name = "int", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature Boolean { get; } = new()
            { TypeParameters = [], Name = "bool", Fields = [], StaticFields = [], Functions = [] };

        public static ClassSignature Never { get; } = new()
            { TypeParameters = [], Name = "!", Fields = [], StaticFields = [], Functions = [] };

        public static IEnumerable<ITypeSignature> BuiltInTypes { get; } = [Unit, String, Int, Never, Boolean];

        public required IReadOnlyList<GenericTypeReference> TypeParameters { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<TypeField> StaticFields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        public required string Name { get; init; }

        public static ClassSignature Tuple(IReadOnlyList<ITypeReference> elements)
        {
            var typeParameters = new List<GenericTypeReference>(elements.Count);
            var signature = new ClassSignature
            {
                TypeParameters = typeParameters,
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
            typeParameters.AddRange(Enumerable.Range(0, elements.Count).Select(x => new GenericTypeReference
            {
                GenericName = $"T{x}",
                OwnerType = signature
            }));

            return signature;
        }
    }

    public class TypeField
    {
        public required ITypeReference Type { get; init; }
        public required string Name { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
    }
}