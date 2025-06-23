using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NewLang.Core;

public class TypeChecker
{
    public static void TypeCheck(LangProgram program)
    {
        new TypeChecker(program).TypeCheckInner();
    }

    private TypeChecker(LangProgram program)
    {
        _program = program;
    }

    private readonly LangProgram _program;

    private readonly Dictionary<string, ITypeSignature> _types = ClassSignature.BuiltInTypes
        .Concat(UnionSignature.BuiltInTypes)
        .ToDictionary(x => x.Name);

    // todo: generic placeholders in scope?
    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new();
    private Dictionary<string, FunctionSignature> ScopedFunctions => _typeCheckingScopes.Peek().Functions;
    private ITypeSignature? CurrentTypeSignature => _typeCheckingScopes.Peek().CurrentTypeSignature;
    private FunctionSignature? CurrentFunctionSignature => _typeCheckingScopes.Peek().CurrentFunctionSignature;
    private ITypeReference ExpectedReturnType => _typeCheckingScopes.Peek().ExpectedReturnType;

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

    private void TypeCheckInner()
    {
        // initial scope
        _typeCheckingScopes.Push(new TypeCheckingScope(
            ParentScope: null,
            new Dictionary<string, FunctionSignature>(),
            ExpectedReturnType: InstantiatedClass.Unit,
            null,
            null));

        SetupSignatures();

        foreach (var union in _program.Unions)
        {
            if (_types[union.Name.StringValue] is not UnionSignature unionSignature)
            {
                throw new InvalidOperationException($"Expected {union.Name.StringValue} to be a union");
            }

            var unionGenericPlaceholders = union.GenericArguments.Select(x => x.StringValue).ToHashSet();
            
            using (PushScope(unionSignature))
            {
                foreach (var function in union.Functions)
                {
                    var fnSignature = unionSignature.Functions.First(x => x.Name == function.Name.StringValue);

                    TypeCheckFunctionBody(function, fnSignature, unionGenericPlaceholders);
                }
            }
        }

        foreach (var @class in _program.Classes)
        {
            if (_types[@class.Name.StringValue] is not ClassSignature classSignature)
            {
                throw new InvalidOperationException($"Expected {@class.Name.StringValue} to be a class");
            }

            var classGenericPlaceholders =
                @class.TypeArguments.Select(x => x.StringValue).ToHashSet();

            var instanceFieldVariables = new List<Variable>();
            var staticFieldVariables = new List<Variable>();
            
            foreach (var field in @class.Fields)
            {
                var isStatic = field.StaticModifier is not null;
                
                var fieldTypeReference = GetTypeReference(field.Type, classGenericPlaceholders);

                if (isStatic)
                {
                    // todo: static constructor?
                    if (field.InitializerValue is null)
                    {
                        throw new InvalidOperationException("Expected field initializer for static field");
                    }

                    var valueType = TypeCheckExpression(field.InitializerValue, classGenericPlaceholders);

                    ExpectType(valueType, fieldTypeReference);

                    staticFieldVariables.Add(new Variable(
                        field.Name.StringValue,
                        fieldTypeReference,
                        // field will be instantiated by the time it is accessed
                        Instantiated: true,
                        Mutable: field.MutabilityModifier is not null));
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
                        Instantiated: true,
                        Mutable: field.MutabilityModifier is not null));
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

        foreach (var function in _program.Functions)
        {
            TypeCheckFunctionBody(function, ScopedFunctions[function.Name.StringValue], []);
        }

        foreach (var expression in _program.Expressions)
        {
            TypeCheckExpression(expression, []);
        }

        PopScope();

        ResolvedTypeChecker.CheckAllExpressionsHaveResolvedTypes(_program);
    }

    private void SetupSignatures()
    {
        var classes =
            new List<(ProgramClass, ClassSignature, List<FunctionSignature>, List<TypeField> fields, List<TypeField>
                staticFields)>();
        var unions = new List<(ProgramUnion, UnionSignature, List<FunctionSignature>, List<IUnionVariant>)>();

        // setup union and class signatures before setting up their functions/fields etc so that functions and fields can reference other types
        foreach (var union in _program.Unions)
        {
            var variants = new List<IUnionVariant>();
            var functions = new List<FunctionSignature>();
            var unionSignature = new UnionSignature
            {
                Name = union.Name.StringValue,
                GenericParameters = [..union.GenericArguments.Select(x => x.StringValue)],
                Functions = functions,
                Variants = variants
            };
            
            unions.Add((union, unionSignature, functions, variants));

            if (!_types.TryAdd(unionSignature.Name, unionSignature))
            {
                throw new InvalidOperationException($"Duplicate type {unionSignature.Name}");
            }
        }

        foreach (var @class in _program.Classes)
        {
            var name = @class.Name.StringValue;
            var functions = new List<FunctionSignature>();
            var fields = new List<TypeField>();
            var staticFields = new List<TypeField>();
            var signature = new ClassSignature()
            {
                Name = name,
                GenericParameters = [..@class.TypeArguments.Select(x => x.StringValue)],
                Functions = functions,
                Fields = fields,
                StaticFields = staticFields
            };

            classes.Add((@class, signature, functions, fields, staticFields));

            if (!_types.TryAdd(name, signature))
            {
                throw new InvalidOperationException($"Class with name {name} already defined");
            }
        }

        foreach (var (union, unionSignature, functions, variants) in unions)
        {
            using var _ = PushScope(unionSignature);
            var unionGenericPlaceholders = new HashSet<string>();
            foreach (var genericParameter in unionSignature.GenericParameters)
            {
                if (!unionGenericPlaceholders.Add(genericParameter))
                {
                    throw new InvalidOperationException($"Generic name already defined {genericParameter}");
                }
            }
            
            foreach (var function in union.Functions)
            {
                functions.Add(TypeCheckFunctionSignature(function, unionGenericPlaceholders));
            }

            foreach (var variant in union.Variants)
            {
                if (variants.Any(x => x.Name == variant.Name.StringValue))
                {
                    throw new InvalidOperationException("Cannot add multiple variants with the same name");
                }
                
                variants.Add(variant switch
                    {
                        UnitStructUnionVariant => new NoMembersUnionVariant{Name = variant.Name.StringValue},
                        Core.TupleUnionVariant tupleVariant => TypeCheckTupleVariant(tupleVariant),
                        StructUnionVariant structUnionVariant => TypeCheckClassVariant(structUnionVariant),
                        _ => throw new UnreachableException()
                    });
                
                continue;

                TupleUnionVariant TypeCheckTupleVariant(Core.TupleUnionVariant tupleVariant)
                {
                    if (tupleVariant.TupleMembers.Count == 0)
                    {
                        throw new InvalidOperationException("union Tuple variants must have at least one parameter. Use a unit variant instead");
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
                            throw new InvalidOperationException($"Duplicate field {field.Name.StringValue}");
                        }

                        if (field.AccessModifier is not null)
                        {
                            throw new InvalidOperationException("Access modifier not allowed on class union variants. All fields are public");
                        }

                        if (field.StaticModifier is not null)
                        {
                            throw new InvalidOperationException("StaticModifier not allowed on class union variants");
                        }
                        
                        var typeField = new TypeField
                        {
                            Name = field.Name.StringValue,
                            Type = GetTypeReference(field.Type, unionGenericPlaceholders),
                            IsMutable = field.MutabilityModifier is { Modifier.Type: TokenType.Mut },
                            IsPublic = true,
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
            var classGenericPlaceholders = new HashSet<string>();
            foreach (var genericParameter in classSignature.GenericParameters)
            {
                if (!classGenericPlaceholders.Add(genericParameter))
                {
                    throw new InvalidOperationException($"Generic name already defined {genericParameter}");
                }
            }

            foreach (var fn in @class.Functions)
            {
                // todo: check function name collisions. also function overloading
                functions.Add(TypeCheckFunctionSignature(fn, classGenericPlaceholders));
            }

            foreach (var field in @class.Fields)
            {
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = GetTypeReference(field.Type, classGenericPlaceholders),
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
                throw new InvalidOperationException($"Function with name {name} already defined");
            }
        }

        foreach (var typeSignature in _types.Values)
        {
            if (typeSignature is ClassSignature classSignature)
            {
                if (classSignature.GenericParameters.Any(x =>
                        _types.ContainsKey(x)
                        || classSignature.Functions.Any(y => y.Name == x)
                        || _program.Functions.Any(y => y.Name.StringValue == x)))
                {
                    throw new InvalidOperationException("Generic name collision");
                }

                foreach (var fn in classSignature.Functions)
                {
                    if (fn.GenericParameters.Any(x =>
                            _types.ContainsKey(x)
                            || classSignature.Functions.Any(y => y.Name == x)
                            || _program.Functions.Any(y => y.Name.StringValue == x)))
                    {
                        throw new InvalidOperationException("Generic name collision");
                    }
                }
            }
            else if (typeSignature is UnionSignature unionSignature)
            {
                if (unionSignature.GenericParameters.Any(x =>
                        _types.ContainsKey(x)
                        || unionSignature.Functions.Any(y => y.Name == x)
                        || _program.Functions.Any(y => y.Name.StringValue == x)))
                {
                    throw new InvalidOperationException("Generic name collision");
                }

                foreach (var fn in unionSignature.Functions)
                {
                    if (fn.GenericParameters.Any(x =>
                            _types.ContainsKey(x)
                            || unionSignature.Functions.Any(y => y.Name == x)
                            || _program.Functions.Any(y => y.Name.StringValue == x)))
                    {
                        throw new InvalidOperationException("Generic name collision");
                    }
                }
            }
        }
    }


    private void TypeCheckFunctionBody(LangFunction function,
        FunctionSignature fnSignature,
        HashSet<string> genericPlaceholders)
    {
        var innerGenericPlaceholders = new HashSet<string>(genericPlaceholders);
        foreach (var genericParameter in fnSignature.GenericParameters)
        {
            innerGenericPlaceholders.Add(genericParameter);
        }

        using var _ = PushScope(null, fnSignature, fnSignature.ReturnType);
        foreach (var parameter in fnSignature.Arguments)
        {
            AddScopedVariable(parameter.Name, new Variable(
                parameter.Name,
                parameter.Type,
                Instantiated: true,
                Mutable: parameter.Mutable));
        }

        TypeCheckBlock(function.Block,
            innerGenericPlaceholders);
    }

    private FunctionSignature TypeCheckFunctionSignature(LangFunction fn,
        HashSet<string> genericPlaceholders)
    {
        var parameters = new List<FunctionArgument>();
        
        var name = fn.Name.StringValue;
        var fnSignature = new FunctionSignature(
            name,
            [..fn.TypeArguments.Select(x => x.StringValue)],
            parameters,
            IsStatic: fn.StaticModifier is not null)
        {
            ReturnType = null!
        };

        var innerGenericPlaceholders = new HashSet<string>(genericPlaceholders);
        foreach (var genericParameter in fnSignature.GenericParameters)
        {
            if (!innerGenericPlaceholders.Add(genericParameter))
            {
                throw new InvalidOperationException($"Generic parameter {genericParameter} already defined");
            }
        }

        fnSignature.ReturnType = fn.ReturnType is null
            ? InstantiatedClass.Unit
            : GetTypeReference(fn.ReturnType, innerGenericPlaceholders);

        foreach (var parameter in fn.Parameters)
        {
            var paramName = parameter.Identifier.StringValue;
            if (parameters.Any(x => x.Name == paramName))
            {
                throw new InvalidOperationException($"Parameter with {paramName} already defined");
            }

            var type = GetTypeReference(parameter.Type, innerGenericPlaceholders);

            parameters.Add(new FunctionArgument(paramName, type, Mutable: parameter.MutabilityModifier is not null));
        }

        // todo: check function name collisions. also function overloading
        return fnSignature;
    }

    private ITypeReference TypeCheckBlock(
        Block block,
        HashSet<string> genericPlaceholders)
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
        HashSet<string> genericPlaceholders,
        bool allowUninstantiatedVariable = false)
    {
        var expressionType = expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, genericPlaceholders),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression, allowUninstantiatedVariable),
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
                objectInitializerExpression.ObjectInitializer, genericPlaceholders),
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

    private ITypeReference TypeCheckTupleExpression(TupleExpression tuple, HashSet<string> genericPlaceholders)
    {
        if (tuple.Values.Count == 1)
        {
            return TypeCheckExpression(tuple.Values[0], genericPlaceholders);
        }

        var types = tuple.Values.Select(value => TypeCheckExpression(value, genericPlaceholders)).ToArray();
        
        return InstantiatedClass.Tuple(types);
    }

    private ITypeReference TypeCheckMatchExpression(MatchExpression matchExpression,
        HashSet<string> genericPlaceholders)
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

            var armType = TypeCheckExpression(arm.Expression, genericPlaceholders);
            foundType ??= armType;

            ExpectType(foundType, armType);
            
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
               || type is InstantiatedUnion { Variants: var unionVariants }
               && !unionVariants.Select(x => x.Name).Except(matchedVariants).Any();
    }

    private ITypeReference TypeCheckMatchesExpression(MatchesExpression matchesExpression, HashSet<string> genericPlaceholders)
    {
        var valueType = TypeCheckExpression(matchesExpression.ValueExpression, genericPlaceholders);

        matchesExpression.DeclaredVariables = TypeCheckPattern(valueType, matchesExpression.Pattern, genericPlaceholders);

        return InstantiatedClass.Boolean;
    }

    private IReadOnlyList<string> TypeCheckPattern(ITypeReference typeReference, IPattern pattern, HashSet<string> genericPlaceholders)
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
                
                if (typeReference is not InstantiatedUnion union)
                {
                    throw new InvalidOperationException($"{typeReference} is not a union");
                }

                ExpectType(patternUnionType, union);
                
                _ = union.Variants.FirstOrDefault(x => x.Name == variantPattern.VariantName.StringValue)
                    ?? throw new InvalidOperationException($"Variant {variantPattern.VariantName.StringValue} not found on type {union}");

                if (variantPattern.VariableName is {StringValue: var variableName})
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternUnionType,
                        Instantiated: false,
                        Mutable: false);
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

                ExpectType(patternType, typeReference);
                
                if (classPattern.FieldPatterns.Count > 0)
                {
                    if (patternType is not InstantiatedClass classType)
                    {
                        throw new InvalidOperationException($"Expected {typeReference} to be a class");
                    }

                    if (classPattern.FieldPatterns.GroupBy(x => x.Key.StringValue).Any(x => x.Count() > 1))
                    {
                        throw new InvalidOperationException("Duplicate fields found");
                    }

                    if (!classPattern.RemainingFieldsDiscarded &&
                        classPattern.FieldPatterns.Count != classType.Fields.Count)
                    {
                        throw new InvalidOperationException("Not all fields are listed");
                    }

                    foreach (var (fieldName, fieldPattern) in classPattern.FieldPatterns)
                    {
                        var fieldType = GetClassField(classType, fieldName.StringValue);
                        
                        if (fieldPattern is null)
                        {
                            patternVariables.Add(fieldName.StringValue);
                            var variable = new Variable(
                                fieldName.StringValue,
                                fieldType,
                                Instantiated: false,
                                Mutable: false);
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
                }

                if (classPattern.VariableName is {StringValue: var variableName})
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        Instantiated: false,
                        Mutable: false);
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
 
                 ExpectType(patternType, typeReference);

                 if (patternType is not InstantiatedUnion union)
                 {
                     throw new InvalidOperationException($"{patternType} is not a union");
                 }
                 
                 var variant = union.Variants.FirstOrDefault(x => x.Name == structVariantPattern.VariantName.StringValue)
                     ?? throw new InvalidOperationException($"No variant found named {structVariantPattern.VariantName.StringValue}");

                 if (variant is not ClassUnionVariant structVariant)
                 {
                     throw new InvalidOperationException($"Variant {variant.Name} is not a struct variant");
                 }
                 
                 if (structVariantPattern.FieldPatterns.GroupBy(x => x.Key.StringValue).Any(x => x.Count() > 1))
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
                            Instantiated: false,
                            Mutable: false);
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
 
                 if (structVariantPattern.VariableName is {StringValue: var variableName})
                 {
                     patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        Instantiated: false,
                        Mutable: false);
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

                ExpectType(patternType, typeReference);

                if (patternType is not InstantiatedUnion unionType)
                {
                    throw new InvalidOperationException($"{typeReference} is not a union");
                }

                var variant = unionType.Variants.FirstOrDefault(x =>
                        x.Name == unionTupleVariantPattern.VariantName.StringValue)
                    ?? throw new InvalidOperationException($"No union variant found with name {unionTupleVariantPattern.VariantName.StringValue}");

                if (variant is not TupleUnionVariant tupleUnionVariant)
                {
                    throw new InvalidOperationException("Expected union to be a tuple variant");
                }

                if (tupleUnionVariant.TupleMembers.Count != unionTupleVariantPattern.TupleParamPatterns.Count)
                {
                    throw new InvalidOperationException($"Expected {tupleUnionVariant.TupleMembers.Count} tuple members, found {unionTupleVariantPattern.TupleParamPatterns.Count}");
                }

                foreach (var (tupleMemberType, tupleMemberPattern) in tupleUnionVariant.TupleMembers.Zip(unionTupleVariantPattern.TupleParamPatterns))
                {
                    patternVariables.AddRange(TypeCheckPattern(tupleMemberType, tupleMemberPattern, genericPlaceholders));
                }

                if (unionTupleVariantPattern.VariableName is {StringValue: var variableName})
                {
                    patternVariables.Add(variableName);
                    var variable = new Variable(
                        variableName,
                        patternType,
                        Instantiated: false,
                        Mutable: false);
                    if (!TryAddScopedVariable(variableName, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }
                }
                
                break;
            }
            case VariableDeclarationPattern {VariableName.StringValue: var variableName}:
            {
                patternVariables.Add(variableName);
                var variable = new Variable(
                    variableName,
                    typeReference,
                    Instantiated: false,
                    Mutable: false);
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

    private ITypeReference TypeCheckUnionStructInitializer(UnionStructVariantInitializer initializer, HashSet<string> genericPlaceholders)
    {
        var type = GetTypeReference(initializer.UnionType, genericPlaceholders);

        if (type is not InstantiatedUnion instantiatedUnion)
        {
            throw new InvalidOperationException($"{type} is not a union");
        }

        var variant = instantiatedUnion.Variants.FirstOrDefault(x => x.Name == initializer.VariantIdentifier.StringValue)
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

            var valueType = TypeCheckExpression(fieldInitializer.Value, genericPlaceholders);

            ExpectType(field.Type, valueType);
        }

        return type;
    }

    private ITypeReference TypeCheckUnaryOperator(UnaryOperator unaryOperator, HashSet<string> genericPlaceholders)
    {
        return unaryOperator.OperatorType switch
        {
            UnaryOperatorType.FallOut => TypeCheckFallout(unaryOperator.Operand, genericPlaceholders),
            UnaryOperatorType.Not => TypeCheckNot(unaryOperator.Operand, genericPlaceholders),
            _ => throw new NotImplementedException($"{unaryOperator.OperatorType}")
        };
    }

    private InstantiatedClass TypeCheckNot(IExpression expression, HashSet<string> genericPlaceholders)
    {
        var expressionType = TypeCheckExpression(expression, genericPlaceholders);

        ExpectType(expressionType, InstantiatedClass.Boolean);
        
        return InstantiatedClass.Boolean;
    }

    private ITypeReference TypeCheckFallout(IExpression expression, HashSet<string> genericPlaceholders)
    {
        var expressionType = TypeCheckExpression(expression, genericPlaceholders);

        // todo: could implement with an interface? union Result : IFallout?
        if (ExpectedReturnType is not InstantiatedUnion { Name: "Result" or "Option" } union)
        {
            throw new InvalidOperationException("Fallout operator is only valid for Result and Option return types");
        }

        ExpectType(expressionType, ExpectedReturnType);

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

    private ITypeReference TypeCheckGenericInstantiation(GenericInstantiation genericInstantiation, HashSet<string> genericPlaceholders)
    {
        var valueType = TypeCheckExpression(genericInstantiation.Value, genericPlaceholders);

        if (valueType is not InstantiatedFunction instantiatedFunction)
        {
            throw new InvalidOperationException("Expected function");
        }

        if (genericInstantiation.GenericArguments.Count != instantiatedFunction.TypeArguments.Count)
        {
            throw new InvalidOperationException($"Expected {instantiatedFunction.TypeArguments.Count} type arguments but found {genericInstantiation.GenericArguments.Count}");
        }

        for (var i = 0; i < instantiatedFunction.TypeArguments.Count; i++)
        {
            var typeReference = GetTypeReference(genericInstantiation.GenericArguments[i], genericPlaceholders);
            var typeArgument = instantiatedFunction.TypeArguments[i];
            ExpectType(typeReference, typeArgument);
        }

        return instantiatedFunction;
    }

    private ITypeReference TypeCheckMemberAccess(
        MemberAccess memberAccess,
        HashSet<string> genericPlaceholders)
    {
        var ownerExpression = memberAccess.Owner;
        var ownerType = TypeCheckExpression(ownerExpression, genericPlaceholders);

        if (ownerType is not InstantiatedClass classType)
        {
            // todo: generic argument constraints with interfaces?
            throw new InvalidOperationException("Can only access members on instantiated types");
        }

        if (classType.TryInstantiateFunction(memberAccess.MemberName.StringValue, null, out var function))
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
        HashSet<string> genericPlaceholders)
    {
        var type = GetTypeReference(staticMemberAccess.Type, genericPlaceholders);

        var memberName = staticMemberAccess.MemberName.StringValue;
        if (type is InstantiatedClass { StaticFields: var staticFields } instantiatedClass)
        {
            var field = staticFields.FirstOrDefault(x => x.Name == memberName);
            if (field is not null)
            {
                return field.Type;
            }

            if (instantiatedClass.TryInstantiateFunction(memberName, null, out var function))
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
        ObjectInitializer objectInitializer,
        HashSet<string> genericPlaceholders)
    {
        var foundType = GetTypeReference(objectInitializer.Type, genericPlaceholders);
        if (foundType is not InstantiatedClass instantiatedClass)
        {
            // todo: more checks
            throw new InvalidOperationException($"Type {foundType} cannot be initialized");
        }

        if (objectInitializer.FieldInitializers.GroupBy(x => x.FieldName.StringValue)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Field can only be initialized once");
        }

        if (objectInitializer.FieldInitializers.Count != instantiatedClass.Fields.Count)
        {
            throw new InvalidOperationException("Not all fields were initialized");
        }
        
        var fields = instantiatedClass.Fields.ToDictionary(x => x.Name);
        
        foreach (var fieldInitializer in objectInitializer.FieldInitializers)
        {
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                throw new InvalidOperationException($"No field named {fieldInitializer.FieldName.StringValue}");
            }

            if ((CurrentTypeSignature is not ClassSignature currentClassSignature
                || !instantiatedClass.MatchesSignature(currentClassSignature))
                && !field.IsPublic)
            {
                throw new InvalidOperationException("Cannot access private field");
            }
            
            var valueType = TypeCheckExpression(fieldInitializer.Value, genericPlaceholders);
            
            ExpectType(field.Type, valueType);
        }

        return foundType;
    }

    private ITypeReference TypeCheckBinaryOperatorExpression(
        BinaryOperator @operator,
        HashSet<string> genericPlaceholders)
    {
        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
            {
                var leftType = TypeCheckExpression(@operator.Left, genericPlaceholders);
                var rightType = TypeCheckExpression(@operator.Right, genericPlaceholders);
                ExpectType(leftType, InstantiatedClass.Int);
                ExpectType(rightType, InstantiatedClass.Int);

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
            {
                var leftType = TypeCheckExpression(@operator.Left, genericPlaceholders);
                var rightType = TypeCheckExpression(@operator.Right, genericPlaceholders);
                ExpectType(leftType, InstantiatedClass.Int);
                ExpectType(rightType, InstantiatedClass.Int);

                return InstantiatedClass.Int;
            }
            case BinaryOperatorType.EqualityCheck:
            {
                var leftType = TypeCheckExpression(@operator.Left, genericPlaceholders);
                var rightType = TypeCheckExpression(@operator.Right, genericPlaceholders);
                ExpectType(rightType, leftType);

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.ValueAssignment:
            {
                var leftType = TypeCheckExpression(@operator.Left, genericPlaceholders, allowUninstantiatedVariable: true);
                var rightType = TypeCheckExpression(@operator.Right, genericPlaceholders);
                if (!IsExpressionAssignable(@operator.Left, genericPlaceholders))
                {
                    throw new InvalidOperationException($"{@operator.Left} is not assignable");
                }

                if (@operator.Left is ValueAccessorExpression
                    {
                        ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken variableName }
                    } && GetScopedVariable(variableName.StringValue) is { Instantiated: false } variable)
                {
                    variable.Instantiated = true;
                }

                ExpectType(rightType, leftType);
                
                return rightType;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private bool IsExpressionAssignable(IExpression expression, HashSet<string> genericPlaceholders)
    {
        if (expression is ValueAccessorExpression { ValueAccessor: {AccessType: ValueAccessType.Variable, Token: StringToken valueToken} })
        {
            var variable = GetScopedVariable(valueToken.StringValue);
            return !variable.Instantiated || variable.Mutable;
        }

        if (expression is MemberAccessExpression memberAccess)
        {
            var owner = memberAccess.MemberAccess.Owner;

            var isOwnerAssignable = IsExpressionAssignable(owner, genericPlaceholders);

            // todo: this has already been type checked, we just need to reference the type
            return isOwnerAssignable 
                   && TypeCheckExpression(owner, genericPlaceholders) is InstantiatedClass { Fields: var fields }
                   && fields.Single(x => x.Name == memberAccess.MemberAccess.MemberName.StringValue).IsMutable;
        }

        if (expression is StaticMemberAccessExpression staticMemberAccess)
        {
            var ownerType = GetTypeReference(staticMemberAccess.StaticMemberAccess.Type, genericPlaceholders);

            return ownerType is InstantiatedClass { StaticFields: var staticFields }
                   && staticFields.Single(x => x.Name == staticMemberAccess.StaticMemberAccess.MemberName.StringValue)
                       .IsMutable;
        }

        return false;
    }
    
    private ITypeReference TypeCheckIfExpression(IfExpression ifExpression,
        HashSet<string> genericPlaceholders)
    {
        // scope around the entire if expression. Variables declared in the check expression (eg with matches) will be
        // conditionally available in the body
        using var _ = PushScope();
        var checkExpressionType =
            TypeCheckExpression(ifExpression.CheckExpression, genericPlaceholders);
        
        ExpectType(checkExpressionType, InstantiatedClass.Boolean);

        IReadOnlyList<string> conditionallyInstantiatedVariables = [];

        if (ifExpression.CheckExpression is MatchesExpression {DeclaredVariables: var declaredVariables})
        {
            conditionallyInstantiatedVariables = declaredVariables;
        }

        foreach (var variable in conditionallyInstantiatedVariables)
        {
            GetScopedVariable(variable).Instantiated = true;
        }

        TypeCheckExpression(ifExpression.Body, genericPlaceholders);

        foreach (var variable in conditionallyInstantiatedVariables)
        {
            GetScopedVariable(variable).Instantiated = false;
        }
        
        foreach (var elseIf in ifExpression.ElseIfs)
        {
            using var __ = PushScope();
            var elseIfCheckExpressionType
                = TypeCheckExpression(elseIf.CheckExpression, genericPlaceholders);
            ExpectType(elseIfCheckExpressionType, InstantiatedClass.Boolean);

            conditionallyInstantiatedVariables = elseIf.CheckExpression is MatchesExpression { DeclaredVariables: var elseIfDeclaredVariables }
                ? elseIfDeclaredVariables
                : [];
            
            foreach (var variable in conditionallyInstantiatedVariables)
            {
                GetScopedVariable(variable).Instantiated = true;
            }

            TypeCheckExpression(elseIf.Body, genericPlaceholders);
            
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
        HashSet<string> genericPlaceholders)
    {
        var methodType = TypeCheckExpression(methodCall.Method, genericPlaceholders);

        if (methodType is not InstantiatedFunction functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (methodCall.ParameterList.Count != functionType.Arguments.Count)
        {
            throw new InvalidOperationException($"Expected {functionType.Arguments.Count} parameters, got {methodCall.ParameterList.Count}");
        }

        for (var i = 0; i < functionType.Arguments.Count; i++)
        {
            var functionArgument = functionType.Arguments[i];
            var expectedParameterType = functionArgument.Type;
            
            var parameterExpression = methodCall.ParameterList[i];
            var givenParameterType = TypeCheckExpression(parameterExpression, genericPlaceholders);

            ExpectType(givenParameterType, expectedParameterType);

            if (functionArgument.Mutable && !IsExpressionAssignable(parameterExpression, genericPlaceholders))
            {
                throw new InvalidOperationException("Function argument is mutable, but provided expression is not");
            }
        }

        return functionType.ReturnType;
    }

    private ITypeReference TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        HashSet<string> genericPlaceholders)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? InstantiatedClass.Unit
            : TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, genericPlaceholders);
        
        ExpectType(returnExpressionType, ExpectedReturnType);
        
        return InstantiatedClass.Never;
    }

    private ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression, bool allowUninstantiatedVariables)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => InstantiatedClass.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => InstantiatedClass.String,
            {AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedClass.Boolean,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}} =>
                TypeCheckVariableAccess(variableName, allowUninstantiatedVariables),
            {AccessType: ValueAccessType.Variable, Token.Type: TokenType.Ok } => TypeCheckResultVariantKeyword("Ok"),
            {AccessType: ValueAccessType.Variable, Token.Type: TokenType.Error } => TypeCheckResultVariantKeyword("Error"),
            {AccessType: ValueAccessType.Variable, Token.Type: TokenType.This} => TypeCheckThis(),
            {AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo} => InstantiatedClass.Never,
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
                UnionSignature unionSignature => unionSignature.Instantiate(null),
                ClassSignature classSignature => classSignature.Instantiate(null),
                _ => throw new UnreachableException($"Unknown signature type {CurrentTypeSignature.GetType()}")
            };

        }

        ITypeReference TypeCheckResultVariantKeyword(string variantName)
        {
            if (!_types.TryGetValue("Result", out var resultType) || resultType is not UnionSignature unionSignature)
            {
                throw new UnreachableException("Result is a built in union");
            }

            var okVariant = unionSignature.Variants.FirstOrDefault(x => x.Name == variantName)
                ?? throw new UnreachableException($"{variantName} is a built in variant of Result");

            if (okVariant is not TupleUnionVariant tupleVariant)
            {
                throw new UnreachableException($"{variantName} is a tuple variant");
            }

            var instantiatedUnion = InstantiatedUnion.Result(null, null);
            
            return GetTupleUnionFunction(tupleVariant, instantiatedUnion);
        }
    }

    private static InstantiatedFunction GetTupleUnionFunction(TupleUnionVariant tupleVariant, InstantiatedUnion instantiatedUnion)
    {
        var signature = new FunctionSignature(
            tupleVariant.Name,
            [],
            [..tupleVariant.TupleMembers.Select((x, i) => new FunctionArgument(i.ToString(), x, Mutable: false))],
            IsStatic: true)
        {
            ReturnType = instantiatedUnion
        };

        return signature.Instantiate(typeReferences: null, ownerTypeArguments: instantiatedUnion.TypeArguments);
    }

    private ITypeReference TypeCheckVariableAccess(
        string variableName, bool allowUninstantiated)
    {
        if (ScopedFunctions.TryGetValue(variableName, out var function))
        {
            return function.Instantiate(null, null);
        }
        
        if (!TryGetScopedVariable(variableName, out var value))
        {
            throw new InvalidOperationException($"No symbol found with name {variableName}");
        }

        if (!allowUninstantiated && !value.Instantiated)
        {
            throw new InvalidOperationException($"{value.Name} is not instantiated");
        }

        return value.Type;
    }

    private record Variable(string Name, ITypeReference Type, bool Instantiated, bool Mutable)
    {
        public bool Instantiated { get; set; } = Instantiated;
    }

    private ITypeReference TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression,
        HashSet<string> genericPlaceholders)
    {
        var varName = expression.VariableDeclaration.VariableNameToken.StringValue;
        if (VariableIsDefined(varName))
        {
            throw new InvalidOperationException(
                $"Variable with name {varName} already exists");
        }

        switch (expression.VariableDeclaration)
        {
            case {Value: null, Type: null}:
                throw new InvalidOperationException("Variable declaration must have a type specifier or a value");
            case { Value: { } value, Type: var type, MutabilityModifier: var mutModifier} :
            {
                var valueType = TypeCheckExpression(value, genericPlaceholders);
                if (type is not null)
                {
                    var expectedType = GetTypeReference(type, genericPlaceholders);

                    ExpectType(valueType, expectedType);
                }

                AddScopedVariable(varName, new Variable(varName, valueType, Instantiated: true, Mutable: mutModifier is not null));

                break;
            }
            case { Value: null, Type: { } type, MutabilityModifier: var mutModifier }:
            {
                var langType = GetTypeReference(type, genericPlaceholders);
                AddScopedVariable(varName, new Variable(varName, langType, Instantiated: false, Mutable: mutModifier is not null));

                break;
            }
        }
        
        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedClass.Unit;
    }

    private ITypeReference GetTypeReference(
        TypeIdentifier typeIdentifier,
        HashSet<string> genericPlaceholders)
    {
        if (typeIdentifier.Identifier.Type == TokenType.StringKeyword)
        {
            return InstantiatedClass.String;
        }

        if (typeIdentifier.Identifier.Type == TokenType.IntKeyword)
        {
            return InstantiatedClass.Int;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Bool)
        {
            return InstantiatedClass.Boolean;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Result)
        {
            if (typeIdentifier.TypeArguments.Count != 2)
            {
                throw new InvalidOperationException("Result expects 2 arguments");
            }
            
            return InstantiatedUnion.Result(
                GetTypeReference(typeIdentifier.TypeArguments[0], genericPlaceholders),
                GetTypeReference(typeIdentifier.TypeArguments[1], genericPlaceholders));
        }

        if (typeIdentifier.Identifier is StringToken { Type: TokenType.Identifier } stringToken)
        {
            if (_types.TryGetValue(stringToken.StringValue, out var nameMatchingType))
            {
                if (nameMatchingType is ClassSignature classSignature)
                {
                    return classSignature.Instantiate([
                        ..typeIdentifier.TypeArguments
                            .Select(x => GetTypeReference(x, genericPlaceholders))
                    ]);
                }

                if (nameMatchingType is UnionSignature unionSignature)
                {
                    return unionSignature.Instantiate([
                        ..typeIdentifier.TypeArguments
                            .Select(x => GetTypeReference(x, genericPlaceholders))
                    ]);
                }
            }

            if (genericPlaceholders.Contains(stringToken.StringValue))
            {
                return new GenericTypeReference
                {
                    GenericName = stringToken.StringValue
                };
            }
        }
        
        throw new InvalidOperationException($"No type found {typeIdentifier}");
    }

    private static void ExpectType(ITypeReference actual, ITypeReference expected)
    {
        if (actual is InstantiatedClass x && x.IsSameSignature(InstantiatedClass.Never)
            || expected is InstantiatedClass y && y.IsSameSignature(InstantiatedClass.Never))
        {
            return;
        }
        
        switch (actual, expected)
        {
            case (InstantiatedClass actualClass, InstantiatedClass expectedClass):
            {
                if (!actualClass.IsSameSignature(expectedClass))
                {
                    throw new InvalidOperationException($"Expected {expected} but found {actual}");
                }

                for (var i = 0; i < actualClass.TypeArguments.Count; i++)
                {
                    ExpectType(actualClass.TypeArguments[i], expectedClass.TypeArguments[i]);
                }
                
                break;
            }
            case (InstantiatedUnion actualUnion, InstantiatedUnion expectedUnion):
            {
                if (!actualUnion.IsSameSignature(expectedUnion))
                {
                    throw new InvalidOperationException($"Expected {expected} but found {actual}");
                }

                for (var i = 0; i < actualUnion.TypeArguments.Count; i++)
                {
                    ExpectType(actualUnion.TypeArguments[i], expectedUnion.TypeArguments[i]);
                }
                
                break;
            }
            case (InstantiatedUnion union, GenericTypeReference generic):
            {
                if (generic.ResolvedType is not null)
                {
                    ExpectType(union, generic.ResolvedType);
                }
                else
                {
                    generic.ResolvedType = union;
                }

                break;
            }
            case (GenericTypeReference generic, InstantiatedUnion union):
            {
                if (generic.ResolvedType is not null)
                {
                    ExpectType(union, generic.ResolvedType);
                }
                else
                {
                    generic.ResolvedType = union;
                }

                break;
            }
            case (InstantiatedClass @class, GenericTypeReference generic):
            {
                if (generic.ResolvedType is not null)
                {
                    ExpectType(@class, generic.ResolvedType);
                }
                else
                {
                    generic.ResolvedType = @class;
                }

                break;
            }
            case (GenericTypeReference generic, InstantiatedClass @class):
            {
                if (generic.ResolvedType is not null)
                {
                    ExpectType(@class, generic.ResolvedType);
                }
                else
                {
                    generic.ResolvedType = @class;
                }

                break;
            }
            case (GenericTypeReference genericTypeReference, GenericTypeReference expectedGeneric):
            {
                if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is not null)
                {
                    ExpectType(genericTypeReference.ResolvedType, expectedGeneric.ResolvedType);
                }
                else if (genericTypeReference.ResolvedType is null && expectedGeneric.ResolvedType is not null)
                {
                    genericTypeReference.ResolvedType = expectedGeneric.ResolvedType;
                }
                else if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is null)
                {
                    expectedGeneric.ResolvedType = genericTypeReference.ResolvedType;
                }
                else
                {
                    genericTypeReference.Link(expectedGeneric);
                }
                break;
            }
            default:
                throw new UnreachableException($"{actual}, {expected.GetType()}");
        }
    }

    public interface ITypeReference;

    public class GenericTypeReference : ITypeReference
    {
        public required string GenericName { get; init; }

        private ITypeReference? _resolvedType;
        public ITypeReference? ResolvedType
        {
            get => _resolvedType;
            set
            {
                if (_resolvedType is not null)
                {
                    throw new Exception("Should not set resolved when resolved is already set");
                }
                
                if (value is GenericTypeReference)
                {
                    throw new UnreachableException("Resolved type should not be a generic type reference");
                }
                _resolvedType = value;
                foreach (var linkedReference in LinkedGenericTypeReferences)
                {
                    linkedReference.ResolvedType ??= value;
                }

                LinkedGenericTypeReferences.Clear();
            }
        }

        public void Link(GenericTypeReference other)
        {
            if (other.ResolvedType is not null || ResolvedType is not null)
            {
                throw new Exception("No need to link generic types, we have already resolved at least one of them");
            }

            LinkedGenericTypeReferences.Add(other);
            other.LinkedGenericTypeReferences.Add(this);
        }

        private HashSet<GenericTypeReference> LinkedGenericTypeReferences { get; } = [];

        public override string ToString()
        {
            var sb = new StringBuilder($"{GenericName}=[");
            sb.Append(ResolvedType?.ToString() ?? "??");
            sb.Append(']');
            
            return sb.ToString();
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
            TypeArguments = typeArguments;
            // todo: don't think this model is going to work. Need to be able to access multiple layers of generic arguments
            Arguments =
            [
                ..signature.Arguments.Select(x => x with { Type = x.Type switch
                    {
                        GenericTypeReference genericTypeReference => typeArguments.FirstOrDefault(y => y.GenericName == genericTypeReference.GenericName)
                            ?? ownerTypeArguments.First(y => y.GenericName == genericTypeReference.GenericName),
                        _ => x.Type
                    } }
                )
            ];
            ReturnType = signature.ReturnType switch
            {
                GenericTypeReference genericTypeReference => typeArguments.FirstOrDefault(y =>
                        y.GenericName == genericTypeReference.GenericName)
                    ?? ownerTypeArguments.First(y => y.GenericName == genericTypeReference.GenericName),
                _ => signature.ReturnType
            };
        }
        
        private FunctionSignature Signature { get; }

        public bool IsStatic => Signature.IsStatic;
        
        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ITypeReference ReturnType { get; }
        
        public IReadOnlyList<FunctionArgument> Arguments { get; }
    }
    
    public class InstantiatedClass : ITypeReference
    {
        public InstantiatedClass(ClassSignature signature, IReadOnlyList<GenericTypeReference> typeArguments)
        {
            Signature = signature;
            TypeArguments = typeArguments;

            Fields = [..signature.Fields.Select(x => new TypeField
            {
                Name = x.Name,
                IsMutable = x.IsMutable,
                IsPublic = x.IsPublic,
                Type = x.Type switch
                {
                    GenericTypeReference genericTypeReference => typeArguments.First(y => y.GenericName == genericTypeReference.GenericName),
                    var type => type
                }
            })];
            StaticFields = [..signature.StaticFields.Select(x => new TypeField
            {
                Name = x.Name,
                IsMutable = x.IsMutable,
                IsPublic = x.IsPublic,
                Type = x.Type switch
                {
                    GenericTypeReference genericTypeReference => typeArguments.First(y => y.GenericName == genericTypeReference.GenericName),
                    var type => type
                }
            })];
        }

        public bool TryInstantiateFunction(string functionName, IReadOnlyList<ITypeReference>? typeArguments, [NotNullWhen(true)] out InstantiatedFunction? function)
        {
            var signature = Signature.Functions.FirstOrDefault(x => x.Name == functionName);

            if (signature is null)
            {
                function = null;
                return false;
            }

            function = signature.Instantiate(typeArguments, ownerTypeArguments: TypeArguments);
            return true;
        }
        
        public static InstantiatedClass String { get; } = ClassSignature.String.Instantiate(null);
        public static InstantiatedClass Boolean { get; } = ClassSignature.Boolean.Instantiate(null);
        
        public static InstantiatedClass Int { get; } = ClassSignature.Int.Instantiate(null);

        public static InstantiatedClass Unit { get; } = ClassSignature.Unit.Instantiate(null);
        
        public static InstantiatedClass Never { get; } = ClassSignature.Never.Instantiate(null);

        public static InstantiatedClass Tuple(IReadOnlyList<ITypeReference> types) => ClassSignature.Tuple(types)
            .Instantiate(types);
        
        // todo: be consistent with argument/parameter
        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        private ClassSignature Signature { get; }
        
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
                        TupleMembers = [..tuple.TupleMembers.Select(y => y switch
                        {
                            GenericTypeReference genericTypeReference => typeArguments.First(z => z.GenericName == genericTypeReference.GenericName),
                            _ => y
                        })]
                    },
                    ClassUnionVariant classVariant => new ClassUnionVariant
                    {
                        Name = classVariant.Name,
                        Fields = [..classVariant.Fields.Select(y => new TypeField
                        {
                            Name = y.Name,
                            IsMutable = y.IsMutable,
                            IsPublic = y.IsPublic,
                            Type = y.Type switch
                            {
                                GenericTypeReference genericTypeReference => typeArguments.First(z => z.GenericName == genericTypeReference.GenericName),
                                _ => y.Type
                            }
                        })]
                    },
                    _ => x
                })
            ];
        }
        
        public static InstantiatedUnion Result(ITypeReference? value, ITypeReference? error)
        {
            if (value is null != error is null)
            {
                throw new InvalidOperationException("Either all or no type arguments must be specified");
            }

            return UnionSignature.Result.Instantiate(value is null
                ? null
                : [value, error!]);
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
        IReadOnlyList<string> GenericParameters { get; }
    }

    public record FunctionSignature(
        string Name,
        IReadOnlyList<string> GenericParameters,
        IReadOnlyList<FunctionArgument> Arguments,
        bool IsStatic) : ITypeSignature
    {
        // mutable due to setting up signatures and generic stuff
        public required ITypeReference ReturnType { get; set; }

        public InstantiatedFunction Instantiate(
            IReadOnlyList<ITypeReference>? typeReferences,
            IReadOnlyList<GenericTypeReference>? ownerTypeArguments)
        {
            GenericTypeReference[] typeArguments =
            [
                ..GenericParameters.Select(x => new GenericTypeReference
                {
                    GenericName = x,
                })
            ];
            if (typeReferences is not null)
            {
                if (typeReferences.Count != GenericParameters.Count)
                {
                    throw new InvalidOperationException(
                        $"Expected {GenericParameters.Count} type parameters, but found {typeReferences.Count}");
                }
                
                for (var i = 0; i < typeReferences.Count; i++)
                {
                    var typeReference = typeReferences[i];
                    ExpectType(typeReference, typeArguments[i]);
                }
            }
            
            return new InstantiatedFunction(this, typeArguments, ownerTypeArguments ?? []);
        }
    }

    public record FunctionArgument(string Name, ITypeReference Type, bool Mutable);

    public class UnionSignature : ITypeSignature
    {
        public static UnionSignature Result { get; } = new ()
        {
            GenericParameters = ["TValue", "TError"],
            Name = "Result",
            Variants =
            [
                new TupleUnionVariant
                {
                    Name = "Ok",
                    TupleMembers = [new GenericTypeReference { GenericName = "TValue" }],
                },
                new TupleUnionVariant
                {
                    Name = "Error",
                    TupleMembers = [new GenericTypeReference { GenericName = "TError" }],
                }
            ],
            Functions = []
        };
        
        public static readonly IReadOnlyList<ITypeSignature> BuiltInTypes = [Result];
        
        public required string Name { get; init; }
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public required IReadOnlyList<IUnionVariant> Variants { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }


        public InstantiatedUnion Instantiate(IReadOnlyList<ITypeReference>? typeReferences)
        {
            GenericTypeReference[] typeArguments =
            [
                ..GenericParameters.Select(x => new GenericTypeReference
                {
                    GenericName = x,
                })
            ];

            if (typeReferences is not null)
            {
                if (typeReferences.Count != GenericParameters.Count)
                {
                    throw new InvalidOperationException($"Expected {GenericParameters.Count} type parameters, but found {typeReferences.Count}");
                }

                for (var i = 0; i < typeReferences.Count; i++)
                {
                    var typeReference = typeReferences[i];
                    
                    ExpectType(typeReference, typeArguments[i]);
                }
            }

            return new InstantiatedUnion(this, typeArguments);
        }
    }

    public interface IUnionVariant
    {
        string Name { get; }
    }
    
    // todo: better names
    private class TupleUnionVariant : IUnionVariant
    {
        public required string Name { get; init; }
        
        public required IReadOnlyList<ITypeReference> TupleMembers { get; init; }
    }

    private class ClassUnionVariant : IUnionVariant
    {
        public required string Name { get; init; }
        
        public required IReadOnlyList<TypeField> Fields { get; init; }
    }

    private class NoMembersUnionVariant : IUnionVariant
    {
        public required string Name { get; init; }
    }
    
    public class ClassSignature : ITypeSignature
    {
        public static ClassSignature Unit { get; } = new() { GenericParameters = [], Name = "Unit", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature String { get; } = new() { GenericParameters = [], Name = "String", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature Int { get; } = new() { GenericParameters = [], Name = "Int", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature Boolean { get; } = new() { GenericParameters = [], Name = "Boolean", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature Never { get; } = new() { GenericParameters = [], Name = "!", Fields = [], StaticFields = [], Functions = []};

        public static ClassSignature Tuple(IReadOnlyList<ITypeReference> elements) => new()
        {
            GenericParameters = [..Enumerable.Range(0, elements.Count).Select(x => $"T{x}")],
            Name = $"Tuple`{elements.Count}",
            Fields = [..elements.Select((type, i) => new TypeField
            {
                // todo: verify this
                IsMutable = false,
                Name = TupleFieldNames.TryGetValue(i, out var name) ? name : throw new InvalidOperationException("Tuple can only contain at most 10 elements"),
                Type = type,
                IsPublic = true
            })],
            Functions = [],
            StaticFields = []
        };

        private static readonly Dictionary<int, string> TupleFieldNames = new ()
        {
            {0, "First"},
            {1, "Second"},
            {2, "Third"},
            {3, "Fourth"},
            {4, "Fifth"},
            {5, "Sixth"},
            {6, "Seventh"},
            {7, "Eighth"},
            {8, "Ninth"},
            {9, "Tenth"},
        };
        
        public static IEnumerable<ITypeSignature> BuiltInTypes { get; } = [Unit, String, Int, Never, Boolean];
        
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public required string Name { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<TypeField> StaticFields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        
        public InstantiatedClass Instantiate(IReadOnlyList<ITypeReference>? typeReferences)
        {
            GenericTypeReference[] typeArguments =
            [
                ..GenericParameters.Select(x => new GenericTypeReference
                {
                    GenericName = x,
                })
            ];

            if (typeReferences is not null)
            {
                if (typeReferences.Count != GenericParameters.Count)
                {
                    throw new InvalidOperationException($"Expected {GenericParameters.Count} type parameters, but found {typeReferences.Count}");
                }

                for (var i = 0; i < typeReferences.Count; i++)
                {
                    var typeReference = typeReferences[i];
                    ExpectType(typeReference, typeArguments[i]);
                }
            }
            
            return new InstantiatedClass(this, typeArguments);
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