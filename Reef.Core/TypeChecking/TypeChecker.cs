using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private readonly Dictionary<ModuleId, LangModule> _modules;
    private readonly bool _throwOnError;
    private readonly Dictionary<ModuleId, List<TypeCheckerError>> _errors;

    private void AddError(TypeCheckerError error)
    {
        if (_throwOnError)
        {
            throw new InvalidOperationException(error.ToString());
        }

        _errors[CurrentModuleId].Add(error);
    }

    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new();

    private bool ModuleIdAndNameMatchesImport(
        ModuleId moduleId,
        string itemName,
        ModuleImport moduleImport)
    {
        var apparentModuleId = moduleImport.IsGlobal ? "" : CurrentModuleId.Value;

        return SegmentMatches(moduleId, itemName, apparentModuleId, moduleImport.RootModulePathSegment);

        static bool SegmentMatches(
            ModuleId moduleId,
            string itemName,
            string apparentModuleId,
            ModulePathSegment pathSegment)
        {
            if (!moduleId.Value.StartsWith(apparentModuleId))
            {
                return false;
            }

            if (pathSegment.UseAll)
            {
                return true;
            }

            if (pathSegment.SubSegments.Count == 0)
            {
                return pathSegment.Identifier.StringValue == itemName;
            }

            return pathSegment.SubSegments.Any(
                x => SegmentMatches(
                    moduleId,
                    itemName,
                    apparentModuleId == ""
                        ? pathSegment.Identifier.StringValue
                        : $"{apparentModuleId}:::{pathSegment.Identifier.StringValue}",
                        x)
            );
        }
    }

    private ITypeSignature? SearchForType(string name, IReadOnlyList<string>? modulePath = null, bool modulePathIsGlobal = false)
    {
        var modulePathStr = string.Join(":::", modulePath ?? []);

        var matchedTypes = new List<ITypeSignature>();
        var imports = _typeCheckingScopes.SelectMany(x => x.ModuleImports).ToArray();

        foreach (var moduleId in _moduleSignatures.Keys)
        {
            var canMatchModule = CanMatchModule(moduleId, name, modulePath, modulePathStr, modulePathIsGlobal, imports);

            if (canMatchModule
                && GetModuleTypes(moduleId).TryGetValue(name, out var type)
                && (moduleId == CurrentModuleId || type.IsPublic))
            {
                matchedTypes.Add(type);
            }
        }

        if (matchedTypes.Count > 1)
        {
            throw new NotImplementedException();
        }

        return matchedTypes.FirstOrDefault();
    }

    private Dictionary<string, ITypeSignature> GetModuleTypes(ModuleId? moduleId = null)
    {
        moduleId ??= CurrentModuleId;
        if (!_moduleSignatures.TryGetValue(moduleId, out var moduleSignatures))
        {
            return [];
        }

        return moduleSignatures.Unions.Cast<ITypeSignature>().Concat(moduleSignatures.Classes).ToDictionary(x => x.Name);
    }

    private Dictionary<string, FunctionSignature> GetModuleFunctions(ModuleId? moduleId = null)
    {

        moduleId ??= CurrentModuleId;
        if (!_moduleSignatures.TryGetValue(moduleId, out var moduleSignatures))
        {
            return [];
        }

        return moduleSignatures.Functions.ToDictionary(x => x.Name);
    }

    private TypeChecker(
        Dictionary<ModuleId, LangModule> modules,
        IEnumerable<LangModule> importedModules,
        bool throwOnError = false)
    {
        if (modules.Count == 0)
        {
            throw new InvalidOperationException("At least one module is required");
        }

        // _moduleSignatures = new()
        // {
        //     {
        //         DefId.DiagnosticsModuleId,
        //         (
        //             Functions: [..FunctionSignature.DiagnosticFunctions],
        //             Unions: [],
        //             Classes: []
        //         )
        //     },
        //     {
        //         DefId.CoreLibModuleId,
        //         (
        //             [
        //                 FunctionSignature.Box,
        //                 FunctionSignature.Unbox,
        //                 // FunctionSignature.PrintString,
        //                 FunctionSignature.PrintI8,
        //                 FunctionSignature.PrintI16,
        //                 FunctionSignature.PrintI32,
        //                 FunctionSignature.PrintI64,
        //                 FunctionSignature.PrintU8,
        //                 FunctionSignature.PrintU16,
        //                 FunctionSignature.PrintU32,
        //                 FunctionSignature.PrintU64,
        //             ],
        //             [],
        //             []
        //             // [..UnionSignature.BuiltInTypes],
        //             // [..ClassSignature.BuiltInTypes.Value]
        //         )
        //     }
        // };

        _moduleSignatures = [];
        foreach (var module in importedModules)
        {
            Debug.Assert(module.TypeChecked);
            if (!_moduleSignatures.TryGetValue(module.ModuleId, out var lists))
            {
                lists = ([], [], []);
                _moduleSignatures.Add(module.ModuleId, lists);
            }

            lists.Functions.AddRange(module.Functions.Select(x => x.Signature.NotNull()));
            lists.Classes.AddRange(module.Classes.Select(x => x.Signature.NotNull()));
            lists.Unions.AddRange(module.Unions.Select(x => x.Signature.NotNull()));
        }

        _modules = modules;
        _throwOnError = throwOnError;
        _errors = modules.ToDictionary(x => x.Key, _ => new List<TypeCheckerError>());
    }

    private HashSet<GenericPlaceholder> GenericPlaceholders => _typeCheckingScopes.Peek().GenericPlaceholders;
    private ITypeSignature? CurrentTypeSignature => _typeCheckingScopes.Peek().CurrentTypeSignature;
    private FunctionSignature? CurrentFunctionSignature => _typeCheckingScopes.Peek().CurrentFunctionSignature;
    private ITypeReference? ExpectedReturnType => _typeCheckingScopes.Peek().ExpectedReturnType;
    private DefId? CurrentDefId => _typeCheckingScopes.Peek().CurrentDefId;
    private ModuleId CurrentModuleId => _typeCheckingScopes.Peek().ModuleId ?? throw new InvalidOperationException("No current module id");
    private readonly Dictionary<ModuleId, (List<FunctionSignature> Functions, List<UnionSignature> Unions, List<ClassSignature> Classes)> _moduleSignatures;

    private bool AddScopedFunction(FunctionSignature functionSignature)
    {
        if (GetFunctionSignature(functionSignature.Name) is not null)
        {
            return false;
        }
        _typeCheckingScopes.Peek().Functions.Add(functionSignature);
        return true;
    }

    private bool CanMatchModule(
        ModuleId moduleId,
        string name,
        IReadOnlyList<string>? modulePath,
        string modulePathStr,
        bool modulePathIsGlobal,
        IReadOnlyList<ModuleImport> imports)
    {
        return (modulePathStr, modulePathIsGlobal) switch
        {
            ({ Length: > 0 }, true) => moduleId.Value == modulePathStr,
            ({ Length: > 0 }, false) => moduleId.Value == modulePathStr
                                        || moduleId.Value.StartsWith(string.Join(":::", CurrentModuleId.Value, modulePathStr))
                                        || imports.Any(import => ModuleIdAndNameMatchesImport(moduleId, modulePath?.FirstOrDefault() ?? name, import)),
            _ => moduleId == CurrentModuleId
                || imports.Any(import => ModuleIdAndNameMatchesImport(moduleId, name, import))
        };
    }

    private FunctionSignature? GetFunctionSignature(string name, IReadOnlyList<string>? modulePath = null, bool modulePathIsGlobal = false)
    {
        var modulePathStr = string.Join(":::", modulePath ?? []);

        var matchedFunctions = new List<FunctionSignature>();
        var imports = _typeCheckingScopes.SelectMany(x => x.ModuleImports).ToArray();

        if (string.IsNullOrWhiteSpace(modulePathStr))
        {
            if (_typeCheckingScopes.SelectMany(x => x.Functions).FirstOrDefault(x => x.Name == name) is { } foundFunction)
            {
                matchedFunctions.Add(foundFunction);
            }
        }
        else if (modulePathStr == CurrentModuleId.Value
            && GetModuleFunctions(CurrentModuleId).TryGetValue(name, out var x))
        {
            matchedFunctions.Add(x);
        }

        foreach (var moduleId in _moduleSignatures.Keys.Except([CurrentModuleId]))
        {
            var canMatchModule = CanMatchModule(moduleId, name, modulePath, modulePathStr, modulePathIsGlobal, imports);

            if (canMatchModule
                && GetModuleFunctions(moduleId).TryGetValue(name, out var fn)
                && fn.IsPublic)
            {
                matchedFunctions.Add(fn);
            }
        }

        if (matchedFunctions.Count > 1)
        {
            throw new NotImplementedException();
        }

        return matchedFunctions.FirstOrDefault();
    }

    private void TypeCheckImport(ModuleImport moduleImport)
    {
        var apparentModuleId = moduleImport.IsGlobal ? "" : CurrentModuleId.Value;
        TypeCheckModulePathSegment(moduleImport.RootModulePathSegment, new ModuleId(apparentModuleId));
    }

    private void TypeCheckModulePathSegment(ModulePathSegment segment, ModuleId apparentModuleId)
    {
        var found = false;
        if (_modules.ContainsKey(apparentModuleId) || apparentModuleId == DefId.DiagnosticsModuleId)
        {
            if (GetModuleFunctions(apparentModuleId).TryGetValue(segment.Identifier.StringValue, out var fn))
            {
                if (!fn.IsPublic && apparentModuleId != CurrentModuleId)
                {
                    AddError(TypeCheckerError.ImportedItemNotPublic(segment.Identifier));
                }
                found = true;
            }
            if (GetModuleTypes(apparentModuleId).TryGetValue(segment.Identifier.StringValue, out var type))
            {
                if (!type.IsPublic && apparentModuleId != CurrentModuleId)
                {
                    AddError(TypeCheckerError.ImportedItemNotPublic(segment.Identifier));
                }
                found = true;
            }
        }

        var nextModuleId = new ModuleId(apparentModuleId.Value == "" ? segment.Identifier.StringValue : $"{apparentModuleId.Value}:::{segment.Identifier.StringValue}");

        found |= _modules.Keys.Append(DefId.DiagnosticsModuleId).Any(x => x.Value.StartsWith(nextModuleId.Value));

        if (!found)
        {
            AddError(TypeCheckerError.SymbolNotFound(segment.Identifier));
        }

        foreach (var subSegment in segment.SubSegments)
        {
            TypeCheckModulePathSegment(subSegment, nextModuleId);
        }
    }

    public static Dictionary<ModuleId, IReadOnlyList<TypeCheckerError>> TypeCheck(
        IEnumerable<LangModule> modules,
        IEnumerable<LangModule> importedModules,
        bool throwOnError = false)
    {
        var typeChecker = new TypeChecker(modules.ToDictionary(x => x.ModuleId), importedModules, throwOnError);
        typeChecker.TypeCheckInner();

        return typeChecker._errors.ToDictionary(x => x.Key, x => (IReadOnlyList<TypeCheckerError>)x.Value);
    }

    private void TypeCheckInner()
    {
        // initial scope
        _typeCheckingScopes.Push(new TypeCheckingScope(
            null,
            [],
            null,
            null,
            null,
            [],
            null,
            null,
            [
                // implicitly add `use :::Reef:::Core:::*`
                new ModuleImport(
                    IsGlobal: true,
                    new ModulePathSegment(
                        Token.Identifier("Reef", SourceSpan.Default),
                        [
                            new ModulePathSegment(
                                Token.Identifier("Core", SourceSpan.Default),
                                [],
                                UseAll: true
                            )
                        ],
                        UseAll: false))
            ]));

        SetupSignatures();

        foreach (var module in _modules.Values)
        {
            using var _ = PushScope(moduleId: module.ModuleId);

            foreach (var import in module.TopLevelImports)
            {
                TypeCheckImport(import);
            }
        }


        foreach (var moduleId in _modules.Keys)
        {
            var (functions, unions, classes) = _moduleSignatures[moduleId];

            var module = _modules[moduleId];

            using var __ = PushScope(moduleId: moduleId, moduleImports: module.TopLevelImports, functionSignatures: functions);

            foreach (var unionSignature in unions)
            {
                using var _ = PushScope(unionSignature, genericPlaceholders: unionSignature.TypeParameters);

                foreach (var function in unionSignature.Functions)
                {
                    TypeCheckFunctionBody(function);
                }
            }

            foreach (var classSignature in classes)
            {
                using var _ = PushScope(genericPlaceholders: classSignature.TypeParameters);

                var instanceFieldVariables = new List<IVariable>();
                var staticFieldVariables = new List<IVariable>();

                var @class = _modules.First(x => x.Value.ModuleId == moduleId).Value.Classes.First(x => x.Name.StringValue == classSignature.Name);

                foreach (var (fieldIndex, field) in @class.Fields.Index())
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
                        field.InitializerValue.ValueUseful = true;

                        ExpectType(valueType, fieldTypeReference, field.InitializerValue.SourceRange);

                        staticFieldVariables.Add(new FieldVariable(
                            classSignature,
                            field.Name,
                            fieldTypeReference,
                            field.MutabilityModifier is not null,
                            IsStaticField: true,
                            (uint)fieldIndex));
                    }
                    else
                    {
                        if (field.InitializerValue is not null)
                        {
                            throw new InvalidOperationException("Instance fields cannot have initializers");
                        }

                        instanceFieldVariables.Add(new FieldVariable(
                            classSignature,
                            field.Name,
                            fieldTypeReference,
                            field.MutabilityModifier is not null,
                            IsStaticField: false,
                            (uint)fieldIndex));
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

                    foreach (var function in classSignature.Functions.Where(x => x.IsStatic))
                    {
                        TypeCheckFunctionBody(function);
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

                    foreach (var function in classSignature.Functions.Where(x => !x.IsStatic))
                    {
                        TypeCheckFunctionBody(function);
                    }
                }
            }
        }

        foreach (var (moduleId, module) in _modules)
        {
            using var _ = PushScope(
                moduleId: module.ModuleId,
                functionSignatures: _moduleSignatures[module.ModuleId].Functions,
                defId: DefId.Main(module.ModuleId),
                moduleImports: module.TopLevelImports);

            if (moduleId.Value != "main" && module.Expressions.Count > 0)
            {
                _errors[moduleId].Add(TypeCheckerError.TopLevelStatementsInNonMainModule(
                    new SourceRange(
                        module.Expressions[0].SourceRange.Start,
                        module.Expressions[^1].SourceRange.End
                    ),
                    module.ModuleId));
            }

            foreach (var expression in module.Expressions)
            {
                TypeCheckExpression(expression);
            }

            foreach (var functionSignature in _moduleSignatures[module.ModuleId].Functions)
            {
                TypeCheckFunctionBody(functionSignature);
            }
        }

        PopScope();

        foreach (var (fileName, module) in _modules)
        {
            var moduleErrors = _errors[fileName];
            if (moduleErrors.Count == 0)
            {
                moduleErrors.AddRange(TypeTwoTypeChecker.TypeTwoTypeCheck(_moduleSignatures, module, _throwOnError));
            }

            module.TypeChecked = true;
        }
    }

    private InstantiatedClass TypeCheckBlock(
        Block block)
    {
        using var _ = PushScope(moduleImports: block.ScopedImports);

        foreach (var import in block.ScopedImports)
        {
            TypeCheckImport(import);
        }

        var currentDefId = CurrentDefId.NotNull(expectedReason: "Block must be in a type");

        foreach (var fn in block.Functions)
        {
            var signature = fn.Signature ?? TypeCheckFunctionSignature(new DefId(currentDefId.ModuleId, currentDefId.FullName + $"__{fn.Name}"), fn, ownerType: null);

            var localFunctions = CurrentFunctionSignature?.LocalFunctions
                ?? _modules[CurrentModuleId].TopLevelLocalFunctions;

            localFunctions.Add(signature);

            AddScopedFunction(signature);
        }

        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(GetFunctionSignature(fn.Name.StringValue).NotNull());
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression);
        }

        // todo: tail expressions
        return Unit();
    }

    private TypeField? TryGetClassField(InstantiatedClass classType, StringToken fieldName)
    {
        var field = classType.Fields.FirstOrDefault(x => x.Name == fieldName.StringValue);

        if (field is null)
        {
            AddError(TypeCheckerError.UnknownTypeMember(fieldName, classType.Signature.Name));
            return null;
        }

        if ((CurrentTypeSignature is not ClassSignature currentClassSignature
             || !classType.MatchesSignature(currentClassSignature))
            && !field.IsPublic)
        {
            AddError(TypeCheckerError.PrivateFieldReferenced(fieldName));
        }

        return field;
    }

    private InstantiatedClass TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression)
    {
        var varName = expression.VariableDeclaration.VariableNameToken;
        var isVariableDefined = VariableIsDefined(varName.StringValue);
        if (isVariableDefined)
        {
            // todo: variable shadowing?
            AddError(TypeCheckerError.DuplicateVariableDeclaration(varName));
        }

        LocalVariable? variable = null;
        switch (expression.VariableDeclaration)
        {
            case { Value: null, Type: null, MutabilityModifier: var mutModifier }:
                {
                    variable = new LocalVariable(
                        CurrentFunctionSignature,
                        varName, new UnknownInferredType(), Instantiated: false, Mutable: mutModifier is not null);
                    break;
                }
            case { Value: { } value, Type: var type, MutabilityModifier: var mutModifier }:
                {
                    var valueType = TypeCheckExpression(value);
                    value.ValueUseful = true;
                    ITypeReference variableType;
                    if (type is not null)
                    {
                        variableType = GetTypeReference(type);

                        ExpectExpressionType(variableType, value);
                    }
                    else
                    {
                        variableType = valueType;
                    }

                    variable = new LocalVariable(CurrentFunctionSignature, varName, variableType, true, mutModifier is not null);
                    break;
                }
            case { Value: null, Type: { } type, MutabilityModifier: var mutModifier }:
                {
                    var langType = GetTypeReference(type);
                    variable = new LocalVariable(CurrentFunctionSignature, varName, langType, false, mutModifier is not null);
                    break;
                }
        }

        if (variable is not null && !isVariableDefined)
        {
            AddScopedVariable(varName.StringValue, variable);
        }
        expression.VariableDeclaration.Variable = variable;

        // todo: need a way to be able to reassign a variable, but mutate any inner fields in case the parent expression is not mutable
        if (expression.VariableDeclaration is { MutabilityModifier.Modifier.Type: TokenType.Mut, Value: { } expressionValue })
        {
            ExpectMutableExpression(expressionValue);
        }

        // variable declaration return type is always unit, regardless of the variable type
        return Unit();
    }

    private ITypeReference GetTypeReference(
        ITypeIdentifier typeIdentifier)
    {
        return typeIdentifier switch
        {
            FnTypeIdentifier fnTypeIdentifier => GetFnTypeReference(fnTypeIdentifier),
            NamedTypeIdentifier namedTypeIdentifier => GetNamedTypeReference(namedTypeIdentifier),
            TupleTypeIdentifier tupleTypeIdentifier => GetTupleTypeReference(tupleTypeIdentifier),
            UnitTypeIdentifier => Unit(),
            ArrayTypeIdentifier arrayTypeIdentifier => GetArrayTypeReference(arrayTypeIdentifier),
            _ => throw new ArgumentOutOfRangeException(nameof(typeIdentifier))
        };
    }

    private ITypeReference GetArrayTypeReference(ArrayTypeIdentifier arrayTypeIdentifier)
    {
        if (arrayTypeIdentifier.LengthSpecifier is null)
        {
            var type = new ArrayType(GetTypeReference(arrayTypeIdentifier.ElementTypeIdentifier));
            if (arrayTypeIdentifier.BoxingSpecifier is { Type: TokenType.Unboxed })
            {
                AddError(TypeCheckerError.BoxedOnlyTypeCannotBeUnboxed(type, arrayTypeIdentifier.SourceRange));
            }

            return type;
        }

        return new ArrayType(
            GetTypeReference(arrayTypeIdentifier.ElementTypeIdentifier),
            boxed: arrayTypeIdentifier.BoxingSpecifier?.Type switch
            {
                TokenType.Boxed => true,
                TokenType.Unboxed => false,
                null => ArrayTypeSignature.Instance.Boxed,
                _ => throw new UnreachableException(arrayTypeIdentifier.BoxingSpecifier.Type.ToString())
            },
            (uint)arrayTypeIdentifier.LengthSpecifier.IntValue);
    }

    private InstantiatedClass Unit() => GetBuiltInType(DefId.Unit);
    private InstantiatedClass Never() => GetBuiltInType(DefId.Never);
    private InstantiatedClass Boolean() => GetBuiltInType(DefId.Boolean);
    private InstantiatedClass String() => GetBuiltInType(DefId.String);
    private InstantiatedClass UInt64() => GetBuiltInType(DefId.UInt64);
    private InstantiatedClass UInt32() => GetBuiltInType(DefId.UInt32);
    private InstantiatedClass UInt16() => GetBuiltInType(DefId.UInt16);
    private InstantiatedClass UInt8() => GetBuiltInType(DefId.UInt8);
    private InstantiatedClass Int64() => GetBuiltInType(DefId.Int64);
    private InstantiatedClass Int32() => GetBuiltInType(DefId.Int32);
    private InstantiatedClass Int16() => GetBuiltInType(DefId.Int16);
    private InstantiatedClass Int8() => GetBuiltInType(DefId.Int8);
    private IReadOnlyList<InstantiatedClass> IntTypes() => [.. DefId.IntTypes.Select(GetBuiltInType)];

    private InstantiatedClass GetBuiltInType(DefId defId)
    {
        var signature = GetClassSignature(defId);
        Debug.Assert(signature.TypeParameters.Count == 0);
        return InstantiateClass(signature, null);
    }

    private FunctionObject GetFnTypeReference(FnTypeIdentifier identifier)
    {
        var unitSignature = GetClassSignature(DefId.Unit);
        var unit = InstantiateClass(unitSignature, null);

        return new FunctionObject(
            [.. identifier.Parameters.Select(x => new FunctionParameter(GetTypeReference(x.ParameterType), x.Mut))],
            identifier.ReturnType is null ? Unit() : GetTypeReference(identifier.ReturnType),
            identifier.ReturnMutabilityModifier?.Type == TokenType.Mut,
            identifier.BoxingModifier switch
            {
                null or { Token.Type: TokenType.Boxed } => true,
                { Token.Type: TokenType.Unboxed } => false,
                _ => throw new InvalidOperationException(),
            });
    }

    private InstantiatedClass GetTupleTypeReference(TupleTypeIdentifier tupleTypeIdentifier)
    {
        return InstantiateTuple(
            [.. tupleTypeIdentifier.Members.Select(x => (GetTypeReference(x), x.SourceRange))],
            tupleTypeIdentifier.SourceRange,
            tupleTypeIdentifier.BoxingSpecifier);
    }

    private ITypeReference GetNamedTypeReference(
        NamedTypeIdentifier typeIdentifier)
    {
        var identifierName = typeIdentifier.Identifier.StringValue;

        if (SearchForType(
            identifierName,
            [.. typeIdentifier.ModulePath.Select(x => x.StringValue)],
            typeIdentifier.ModulePathIsGlobal) is { } nameMatchingType)
        {
            switch (nameMatchingType)
            {
                case ClassSignature classSignature:
                    return InstantiateClass(classSignature, [
                        ..typeIdentifier.TypeArguments
                            .Select(x => (GetTypeReference(x), x.SourceRange))
                    ], typeIdentifier.BoxedSpecifier, typeIdentifier.SourceRange);
                case UnionSignature unionSignature:
                    return InstantiateUnion(unionSignature, [
                        ..typeIdentifier.TypeArguments
                            .Select(x => (GetTypeReference(x), x.SourceRange))
                    ], typeIdentifier.BoxedSpecifier, typeIdentifier.SourceRange);
            }
        }

        if (GenericPlaceholders.FirstOrDefault(x => x.GenericName == identifierName) is { } genericTypeReference)
        {
            return genericTypeReference;
        }

        AddError(TypeCheckerError.SymbolNotFound(typeIdentifier.Identifier));
        return UnknownType.Instance;
    }

    private bool ExpectExpressionType(IReadOnlyList<ITypeReference> expected, IExpression? actual)
    {
        if (actual is null)
        {
            return false;
        }
        if (actual.ResolvedType is null)
        {
            throw new InvalidOperationException("Expected should have been type checked first before expecting it's value type");
        }

        return actual switch
        {
            MatchExpression matchExpression => ExpectMatchExpressionType(matchExpression),
            BlockExpression blockExpression => ExpectBlockExpressionType(blockExpression),
            IfExpressionExpression ifExpressionExpression => ExpectIfExpressionType(ifExpressionExpression),
            // these expression types are considered to provide their own types, rather than deferring to inner expressions
            BinaryOperatorExpression or MatchesExpression or MemberAccessExpression or MethodCallExpression
                or MethodReturnExpression or ObjectInitializerExpression or StaticMemberAccessExpression
                or TupleExpression or UnaryOperatorExpression or UnionClassVariantInitializerExpression
                or ValueAccessorExpression or VariableDeclarationExpression => ExpectType(actual.ResolvedType!,
                    expected, actual.SourceRange),
            _ => throw new UnreachableException(actual.GetType().ToString())
        };

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
            IfExpressionExpression ifExpressionExpression => ExpectIfExpressionType(ifExpressionExpression),
            // these expression types are considered to provide their own types, rather than deferring to inner expressions
            BinaryOperatorExpression
                or MatchesExpression
                or MemberAccessExpression
                or MethodCallExpression
                or MethodReturnExpression
                or ObjectInitializerExpression
                or StaticMemberAccessExpression
                or TupleExpression
                or UnaryOperatorExpression
                or UnionClassVariantInitializerExpression
                or ValueAccessorExpression
                or VariableDeclarationExpression
                or IndexExpression
                or CollectionExpression
                or FillCollectionExpression => ExpectType(actual.ResolvedType!,
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

    private bool ExpectType(ITypeReference actual, IReadOnlyList<ITypeReference> expectedTypes,
        SourceRange actualSourceRange, bool reportError = true)
    {
        if ((actual is InstantiatedClass x && x.Signature.Id == DefId.Never)
            || expectedTypes.Any(y => y is InstantiatedClass z && z.Signature.Id == DefId.Never))
        {
            return true;
        }

        foreach (var expected in expectedTypes)
        {
            var result = true;
            switch (actual, expected)
            {
                case (GenericPlaceholder placeholder1, GenericTypeReference reference2):
                    {
                        if (reference2.ResolvedType is not null)
                        {
                            result = ExpectType(placeholder1, reference2.ResolvedType, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (GenericTypeReference reference1, GenericPlaceholder placeholder2):
                    {
                        if (reference1.ResolvedType is not null)
                        {
                            result = ExpectType(reference1.ResolvedType, placeholder2, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (GenericPlaceholder placeholder1, GenericPlaceholder placeholder2):
                    {
                        result = placeholder1 == placeholder2;
                        break;
                    }
                case (GenericPlaceholder, _):
                case (_, GenericPlaceholder):
                    {
                        result = false;
                        break;
                    }
                case (InstantiatedClass actualClass, InstantiatedClass expectedClass):
                    {
                        if (!actualClass.IsSameSignature(expectedClass))
                        {
                            result = false;
                            break;
                        }

                        var argumentsPassed = true;

                        for (var i = 0; i < actualClass.TypeArguments.Count; i++)
                        {
                            argumentsPassed &= ExpectType(actualClass.TypeArguments[i], expectedClass.TypeArguments[i], actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        result &= argumentsPassed;

                        if (result && actualClass.Boxed != expectedClass.Boxed)
                        {
                            result = false;
                            if (reportError)
                            {
                                AddError(TypeCheckerError.MismatchedTypeBoxing(
                                    actualSourceRange,
                                    expectedClass,
                                    expectedClass.Boxed,
                                    actualClass,
                                    actualClass.Boxed));
                            }
                        }

                        break;
                    }
                case (InstantiatedUnion actualUnion, InstantiatedUnion expectedUnion):
                    {
                        if (!actualUnion.IsSameSignature(expectedUnion))
                        {
                            result = false;
                            break;
                        }

                        var argumentsPassed = true;

                        for (var i = 0; i < actualUnion.TypeArguments.Count; i++)
                        {
                            argumentsPassed &= ExpectType(actualUnion.TypeArguments[i], expectedUnion.TypeArguments[i],
                                actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        result &= argumentsPassed;

                        if (result && actualUnion.Boxed != expectedUnion.Boxed)
                        {
                            result = false;
                            if (reportError)
                            {
                                AddError(TypeCheckerError.MismatchedTypeBoxing(
                                    actualSourceRange,
                                    expectedUnion,
                                    expectedUnion.Boxed,
                                    actualUnion,
                                    actualUnion.Boxed));
                            }
                        }

                        break;
                    }
                case (InstantiatedUnion union, GenericTypeReference generic):
                    {
                        if (generic.ResolvedType is not null)
                        {
                            result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (GenericTypeReference generic, InstantiatedUnion union):
                    {
                        if (generic.ResolvedType is not null)
                        {
                            result &= ExpectType(generic.ResolvedType, union, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (InstantiatedClass @class, GenericTypeReference generic):
                    {
                        if (generic.ResolvedType is not null)
                        {
                            result &= ExpectType(@class, generic.ResolvedType, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (GenericTypeReference generic, InstantiatedClass @class):
                    {
                        if (generic.ResolvedType is not null)
                        {
                            result &= ExpectType(generic.ResolvedType, @class, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (GenericTypeReference genericTypeReference, GenericTypeReference expectedGeneric):
                    {
                        if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is not null)
                        {
                            result &= ExpectType(genericTypeReference.ResolvedType, expectedGeneric.ResolvedType, actualSourceRange, reportError: false, assignInferredTypes: false);
                        }

                        break;
                    }
                case (FunctionObject functionObject1, FunctionObject functionObject2):
                    {
                        result &= ExpectType(functionObject1.ReturnType, functionObject2.ReturnType, actualSourceRange,
                            reportError: false, assignInferredTypes: false);

                        if (functionObject2.MutableReturn && !functionObject1.MutableReturn)
                        {
                            result = false;
                            AddError(TypeCheckerError.FunctionObjectReturnTypeMutabilityMismatch(actualSourceRange));
                        }

                        result &= functionObject1.Parameters.Count == functionObject2.Parameters.Count;
                        result &= functionObject1.Parameters.Zip(functionObject2.Parameters)
                            .All(z => z.First.Mutable == z.Second.Mutable
                                      && ExpectType(z.First.Type, z.Second.Type, actualSourceRange, reportError: false, assignInferredTypes: false));

                        break;
                    }
            }

            if (result)
            {
                return true;
            }
        }

        if (reportError)
        {
            AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expectedTypes, actual));
        }

        return false;
    }

    private bool ExpectTypeConstraint(
        ITypeConstraint constraint,
        IInstantiatedGeneric instantiatedGeneric,
        ITypeReference typeReference,
        SourceRange actualSourceRange,
        bool reportError)
    {
        switch (constraint)
        {
            case BoxedTypeConstraint boxedTypeConstraint:
                {
                    switch (boxedTypeConstraint.BoxedOfType)
                    {
                        case FunctionObject functionObject:
                            throw new NotImplementedException();
                        case GenericPlaceholder genericPlaceholder:
                            {
                                var result = true;
                                var referencedTypeArgument = instantiatedGeneric.TypeArguments.First(x => x.GenericName == genericPlaceholder.GenericName);

                                result &= ExpectTypeBoxing(typeReference, actualSourceRange, expectedBoxing: true, reportError);

                                ITypeReference unboxedTypeReference = typeReference switch
                                {
                                    FunctionObject functionObject => throw new NotImplementedException(),
                                    GenericPlaceholder genericPlaceholder1 => throw new NotImplementedException(),
                                    GenericTypeReference genericTypeReference => throw new NotImplementedException(),
                                    InstantiatedClass instantiatedClass => InstantiatedClass.Create(instantiatedClass.Signature, instantiatedClass.TypeArguments, boxed: false),
                                    InstantiatedUnion instantiatedUnion => InstantiatedUnion.Create(instantiatedUnion.Signature, instantiatedUnion.TypeArguments, boxed: false),
                                    ArrayType { Length: not null } array => new ArrayType(array.ElementType.ResolvedType, boxed: false, array.Length.Value),
                                    ArrayType { IsDynamic: true } array => DynamicArrayCase(),
                                    UnknownInferredType unknownInferredType => throw new NotImplementedException(),
                                    UnknownType unknownType => throw new NotImplementedException(),
                                    UnspecifiedSizedIntType { ResolvedIntType: null } => new UnspecifiedSizedIntType { Boxed = false },
                                    UnspecifiedSizedIntType { ResolvedIntType: var resolvedIntType } => new UnspecifiedSizedIntType
                                    {
                                        Boxed = false,
                                        ResolvedIntType = InstantiatedClass.Create(resolvedIntType.Signature, resolvedIntType.TypeArguments, boxed: false)
                                    },
                                    _ => throw new ArgumentOutOfRangeException(nameof(typeReference))
                                };

                                result &= ExpectType(referencedTypeArgument, unboxedTypeReference, actualSourceRange, reportError: false, checkConstraints: false);

                                return result;

                                ITypeReference DynamicArrayCase()
                                {
                                    if (reportError)
                                    {
                                        AddError(TypeCheckerError.BoxedOnlyTypeCannotBeUnboxed(typeReference, actualSourceRange));
                                    }
                                    return UnknownType.Instance;
                                }
                            }
                        case ArrayType:
                            throw new NotImplementedException();
                        case GenericTypeReference genericTypeReference:
                            throw new NotImplementedException();
                        case InstantiatedClass instantiatedClass:
                            throw new NotImplementedException();
                        case InstantiatedUnion instantiatedUnion:
                            throw new NotImplementedException();
                        case UnknownInferredType unknownInferredType:
                            throw new NotImplementedException();
                        case UnknownType unknownType:
                            throw new NotImplementedException();
                        case UnspecifiedSizedIntType unspecifiedSizedIntType:
                            throw new NotImplementedException();
                        default:
                            throw new ArgumentOutOfRangeException(nameof(typeReference));
                    }
                }
            case UnboxedTypeConstraint unboxedTypeConstraint:
                {
                    switch (unboxedTypeConstraint.BoxedOfType)
                    {
                        case FunctionObject functionObject:
                            throw new NotImplementedException();
                        case GenericPlaceholder genericPlaceholder:
                            {
                                /*
                                 * pub fn unbox<TParam, TResult>(param: TParam): TResult
                                 *  where TParam: boxed TResult,
                                 *        TResult: unboxed TParam
                                 * {}
                                 */

                                var result = true;
                                var referencedTypeArgument = instantiatedGeneric.TypeArguments.First(x => x.GenericName == genericPlaceholder.GenericName);

                                result &= ExpectTypeBoxing(typeReference, actualSourceRange, expectedBoxing: false, reportError);

                                ITypeReference boxedTypeReference = typeReference switch
                                {
                                    FunctionObject functionObject => throw new NotImplementedException(),
                                    GenericPlaceholder genericPlaceholder1 => throw new NotImplementedException(),
                                    GenericTypeReference genericTypeReference => throw new NotImplementedException(),
                                    InstantiatedClass instantiatedClass => InstantiatedClass.Create(instantiatedClass.Signature, instantiatedClass.TypeArguments, boxed: true),
                                    InstantiatedUnion instantiatedUnion => InstantiatedUnion.Create(instantiatedUnion.Signature, instantiatedUnion.TypeArguments, boxed: true),
                                    ArrayType { Length: not null } arrayType => new ArrayType(arrayType.ElementType.ResolvedType, boxed: true, arrayType.Length.Value),
                                    ArrayType { IsDynamic: true } => throw new UnreachableException(),
                                    UnknownInferredType unknownInferredType => throw new NotImplementedException(),
                                    UnknownType unknownType => throw new NotImplementedException(),
                                    UnspecifiedSizedIntType { ResolvedIntType: null } => new UnspecifiedSizedIntType { Boxed = true },
                                    UnspecifiedSizedIntType { ResolvedIntType: var resolvedIntType } => new UnspecifiedSizedIntType
                                    {
                                        Boxed = true,
                                        ResolvedIntType = InstantiatedClass.Create(resolvedIntType.Signature, resolvedIntType.TypeArguments, boxed: true)
                                    },
                                    _ => throw new ArgumentOutOfRangeException(nameof(typeReference))
                                };

                                result &= ExpectType(referencedTypeArgument, boxedTypeReference, actualSourceRange, reportError: false, checkConstraints: false);

                                return result;
                            }
                        case GenericTypeReference genericTypeReference:
                            throw new NotImplementedException();
                        case ArrayType:
                            throw new NotImplementedException();
                        case InstantiatedClass instantiatedClass:
                            throw new NotImplementedException();
                        case InstantiatedUnion instantiatedUnion:
                            throw new NotImplementedException();
                        case UnknownInferredType unknownInferredType:
                            throw new NotImplementedException();
                        case UnknownType unknownType:
                            throw new NotImplementedException();
                        case UnspecifiedSizedIntType unspecifiedSizedIntType:
                            throw new NotImplementedException();
                        default:
                            throw new ArgumentOutOfRangeException(nameof(typeReference));
                    }
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(constraint));
        }
    }

    private bool ExpectTypeBoxing(ITypeReference actual, SourceRange actualSourceRange, bool expectedBoxing, bool reportError)
    {
        switch (actual)
        {
            case FunctionObject functionObject:
                {
                    if (!expectedBoxing && reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypeBoxing(actualSourceRange, actual, expectedBoxing, actual, actualBoxed: true));
                    }

                    // for now, FunctionObjects are always boxed
                    return expectedBoxing;
                }
            case ArrayType arrayType:
                {
                    if (expectedBoxing != arrayType.Boxed)
                    {
                        AddError(TypeCheckerError.MismatchedTypeBoxing(
                            actualSourceRange, actual, expectedBoxing, actual, arrayType.Boxed));
                        return false;
                    }

                    return true;
                }
            case GenericPlaceholder genericPlaceholder:
                throw new NotImplementedException();
            case GenericTypeReference genericTypeReference:
                throw new NotImplementedException();
            case InstantiatedClass instantiatedClass:
                {
                    if (expectedBoxing != instantiatedClass.Boxed)
                    {
                        AddError(TypeCheckerError.MismatchedTypeBoxing(actualSourceRange, actual, expectedBoxing, actual,
                            instantiatedClass.Boxed));
                        return false;
                    }

                    return true;
                }
            case InstantiatedUnion instantiatedUnion:
                {
                    if (expectedBoxing != instantiatedUnion.Boxed)
                    {
                        AddError(TypeCheckerError.MismatchedTypeBoxing(actualSourceRange, actual, expectedBoxing, actual,
                            instantiatedUnion.Boxed));
                        return false;
                    }

                    return true;
                }

            case UnknownInferredType unknownInferredType:
                throw new NotImplementedException();
            case UnknownType unknownType:
                return true;
            case UnspecifiedSizedIntType unspecifiedSizedIntType:
                {
                    if (expectedBoxing != unspecifiedSizedIntType.Boxed)
                    {
                        AddError(TypeCheckerError.MismatchedTypeBoxing(actualSourceRange, actual, expectedBoxing, actual,
                            unspecifiedSizedIntType.Boxed));
                        return false;
                    }

                    return true;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(actual));
        }
    }

    private bool ExpectType(ITypeReference actual,
        ITypeReference expected,
        SourceRange actualSourceRange,
        bool reportError = true,
        bool assignInferredTypes = true,
        bool checkConstraints = true)
    {
        if ((actual is InstantiatedClass x && x.Signature.Id == DefId.Never)
            || (expected is InstantiatedClass y && y.Signature.Id == DefId.Never))
        {
            return true;
        }

        var result = true;

        switch (actual, expected)
        {
            case (GenericPlaceholder placeholder1, GenericTypeReference reference2):
                {
                    if (reference2.ResolvedType is not null)
                    {
                        result = ExpectType(placeholder1, reference2.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else if (assignInferredTypes)
                    {
                        reference2.ResolvedType = placeholder1;
                    }

                    break;
                }
            case (GenericTypeReference reference1, GenericPlaceholder placeholder2):
                {
                    if (reference1.ResolvedType is not null)
                    {
                        result = ExpectType(placeholder2, reference1.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else if (assignInferredTypes)
                    {
                        reference1.ResolvedType = placeholder2;
                    }

                    break;
                }
            case (GenericPlaceholder placeholder1, GenericPlaceholder placeholder2):
                {
                    result = placeholder1 == placeholder2;
                    if (!result && reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    break;
                }
            case (GenericPlaceholder, _):
            case (_, GenericPlaceholder):
                {
                    result = false;
                    if (reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    break;
                }
            case (UnspecifiedSizedIntType actualIntType, UnspecifiedSizedIntType expectedIntType):
                {
                    if (actualIntType.ResolvedIntType is not null && expectedIntType.ResolvedIntType is not null)
                    {
                        result &= ExpectType(actualIntType.ResolvedIntType, expectedIntType.ResolvedIntType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else if (assignInferredTypes)
                    {
                        if (actualIntType.ResolvedIntType is null && expectedIntType.ResolvedIntType is not null)
                        {
                            actualIntType.ResolvedIntType = expectedIntType.ResolvedIntType;
                        }
                        else if (actualIntType.ResolvedIntType is not null && expectedIntType.ResolvedIntType is null)
                        {
                            expectedIntType.ResolvedIntType = actualIntType.ResolvedIntType;
                        }
                        else
                        {
                            actualIntType.Link(expectedIntType);
                        }
                    }

                    if (actualIntType.Boxed != expectedIntType.Boxed)
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypeBoxing(
                                actualSourceRange,
                                expectedIntType, expectedIntType.Boxed,
                                actualIntType, actualIntType.Boxed));
                        }
                    }

                    break;
                }
            case (UnspecifiedSizedIntType actualIntType, InstantiatedClass expectedClass):
                {
                    if (!DefId.IntTypes.Any(intType => expectedClass.Signature.Id == intType))
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expectedClass, actualIntType));
                        }
                        break;
                    }

                    if (actualIntType.ResolvedIntType is not null)
                    {
                        result &= ExpectType(actualIntType.ResolvedIntType, expectedClass, actualSourceRange, reportError, assignInferredTypes);
                        break;
                    }

                    if (assignInferredTypes)
                    {
                        actualIntType.ResolvedIntType = expectedClass;
                    }

                    if (actualIntType.Boxed != expectedClass.Boxed)
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypeBoxing(
                                actualSourceRange,
                                expectedClass, expectedClass.Boxed,
                                actualIntType, actualIntType.Boxed));
                        }
                    }

                    break;
                }
            case (InstantiatedClass actualClass, UnspecifiedSizedIntType expectedIntType):
                {
                    if (!DefId.IntTypes.Any(intType => actualClass.Signature.Id == intType))
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expectedIntType, actualClass));
                        }
                        break;
                    }

                    if (expectedIntType.ResolvedIntType is not null)
                    {
                        result &= ExpectType(actualClass, expectedIntType.ResolvedIntType, actualSourceRange, reportError, assignInferredTypes);
                        break;
                    }

                    if (assignInferredTypes)
                    {
                        expectedIntType.ResolvedIntType = actualClass;
                    }

                    if (actualClass.Boxed != expectedIntType.Boxed)
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypeBoxing(
                                actualSourceRange,
                                expectedIntType, expectedIntType.Boxed,
                                actualClass, actualClass.Boxed));
                        }
                    }

                    break;
                }
            case (UnspecifiedSizedIntType actualIntType, GenericTypeReference expectedGeneric):
                {
                    if (expectedGeneric.ResolvedType is not null)
                    {
                        result &= ExpectType(actualIntType, expectedGeneric.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else
                    {
                        var genericPlaceholder =
                            expectedGeneric.OwnerType.TypeParameters.First(z => z.GenericName == expectedGeneric.GenericName);

                        if (checkConstraints)
                        {
                            foreach (var constraint in genericPlaceholder.Constraints)
                            {
                                result &= ExpectTypeConstraint(constraint, expectedGeneric.InstantiatedFrom, actualIntType, actualSourceRange, reportError);
                            }
                        }

                        if (assignInferredTypes)
                        {
                            expectedGeneric.ResolvedType = actualIntType;
                        }
                    }
                    break;
                }
            case (GenericTypeReference actualGeneric, UnspecifiedSizedIntType expectedIntType):
                {
                    if (actualGeneric.ResolvedType is not null)
                    {
                        result &= ExpectType(expectedIntType, actualGeneric.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else
                    {
                        var genericPlaceholder =
                            actualGeneric.OwnerType.TypeParameters.First(z => z.GenericName == actualGeneric.GenericName);

                        if (checkConstraints)
                        {
                            foreach (var constraint in genericPlaceholder.Constraints)
                            {
                                result &= ExpectTypeConstraint(constraint, actualGeneric.InstantiatedFrom, expectedIntType, actualSourceRange, reportError);
                            }
                        }

                        if (assignInferredTypes)
                        {
                            actualGeneric.ResolvedType = expectedIntType;
                        }
                    }
                    break;
                }
            case (UnspecifiedSizedIntType, not (UnknownType or UnknownInferredType)):
            case (not (UnknownType or UnknownInferredType), UnspecifiedSizedIntType):
                {
                    if (reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    break;
                }
            case (ArrayType actualArray, ArrayType expectedArray):
                {
                    result = ExpectType(actualArray.ElementType, expectedArray.ElementType, actualSourceRange, reportError,
                        assignInferredTypes);

                    if (actualArray.Boxed != expectedArray.Boxed)
                    {
                        result = false;
                        AddError(TypeCheckerError.MismatchedTypeBoxing(
                            actualSourceRange,
                            expectedArray,
                            expectedArray.Boxed,
                            actualArray,
                            actualArray.Boxed));
                    }

                    if (expectedArray.Length is not null && actualArray.Length is null)
                    {
                        throw new NotImplementedException();
                    }
                    if (actualArray.Length is not null
                        && expectedArray.Length is not null
                        && actualArray.Length != expectedArray.Length)
                    {
                        result = false;
                        AddError(TypeCheckerError.ArrayLengthMismatch(
                            expectedArray.Length.Value, actualArray.Length.Value, actualSourceRange));
                    }

                    break;
                }
            case (InstantiatedClass actualClass, InstantiatedClass expectedClass):
                {
                    if (!actualClass.IsSameSignature(expectedClass))
                    {
                        if (reportError)
                            AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                        break;
                    }

                    var argumentsPassed = true;

                    for (var i = 0; i < actualClass.TypeArguments.Count; i++)
                    {
                        argumentsPassed &= ExpectType(actualClass.TypeArguments[i], expectedClass.TypeArguments[i], actualSourceRange, reportError: false, assignInferredTypes);
                    }

                    if (!argumentsPassed && reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }

                    result &= argumentsPassed;

                    if (actualClass.Boxed != expectedClass.Boxed)
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypeBoxing(
                                actualSourceRange,
                                expectedClass, expectedClass.Boxed,
                                actualClass, actualClass.Boxed));
                        }
                    }

                    break;
                }
            case (InstantiatedUnion actualUnion, InstantiatedUnion expectedUnion):
                {
                    if (!actualUnion.IsSameSignature(expectedUnion))
                    {
                        if (reportError)
                            AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                        result = false;
                        break;
                    }

                    var argumentsPassed = true;

                    for (var i = 0; i < actualUnion.TypeArguments.Count; i++)
                    {
                        argumentsPassed &= ExpectType(actualUnion.TypeArguments[i], expectedUnion.TypeArguments[i],
                            actualSourceRange, reportError: false, assignInferredTypes);
                    }

                    if (!argumentsPassed && reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }
                    result &= argumentsPassed;

                    if (actualUnion.Boxed != expectedUnion.Boxed)
                    {
                        result = false;
                        if (reportError)
                        {
                            AddError(TypeCheckerError.MismatchedTypeBoxing(
                                actualSourceRange,
                                expectedUnion, expectedUnion.Boxed,
                                actualUnion, actualUnion.Boxed));
                        }
                    }

                    break;
                }
            case (ArrayType array, GenericTypeReference generic):
                {
                    if (generic.ResolvedType is { } resolvedGeneric)
                    {
                        result &= ExpectType(array, resolvedGeneric, actualSourceRange, reportError, assignInferredTypes);
                        break;
                    }

                    if (checkConstraints)
                    {
                        var genericPlaceholder =
                            generic.OwnerType.TypeParameters.First(z => z.GenericName == generic.GenericName);
                        foreach (var constraint in genericPlaceholder.Constraints)
                        {
                            result &= ExpectTypeConstraint(constraint, generic.InstantiatedFrom, array, actualSourceRange,
                                reportError);
                        }
                    }

                    if (assignInferredTypes)
                    {
                        generic.ResolvedType = array;
                    }

                    break;
                }
            case (GenericTypeReference generic, ArrayType array):
                {
                    if (generic.ResolvedType is { } resolvedGeneric)
                    {
                        result &= ExpectType(array, resolvedGeneric, actualSourceRange, reportError, assignInferredTypes);
                        break;
                    }

                    if (checkConstraints)
                    {
                        var genericPlaceholder =
                            generic.OwnerType.TypeParameters.First(z => z.GenericName == generic.GenericName);
                        foreach (var constraint in genericPlaceholder.Constraints)
                        {
                            result &= ExpectTypeConstraint(constraint, generic.InstantiatedFrom, array, actualSourceRange,
                                reportError);
                        }
                    }

                    if (assignInferredTypes)
                    {
                        generic.ResolvedType = array;
                    }

                    break;
                }
            case (InstantiatedUnion union, GenericTypeReference generic):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else
                    {
                        var genericPlaceholder =
                            generic.OwnerType.TypeParameters.First(z => z.GenericName == generic.GenericName);

                        if (checkConstraints)
                        {
                            foreach (var constraint in genericPlaceholder.Constraints)
                            {
                                result &= ExpectTypeConstraint(constraint, generic.InstantiatedFrom, union, actualSourceRange, reportError);
                            }
                        }

                        if (assignInferredTypes)
                        {
                            generic.ResolvedType = union;
                        }
                    }

                    break;
                }
            case (GenericTypeReference generic, InstantiatedUnion union):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(union, generic.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else
                    {
                        var genericPlaceholder =
                            generic.OwnerType.TypeParameters.First(z => z.GenericName == generic.GenericName);

                        if (checkConstraints)
                        {
                            foreach (var constraint in genericPlaceholder.Constraints)
                            {
                                result &= ExpectTypeConstraint(constraint, generic.InstantiatedFrom, union, actualSourceRange, reportError);
                            }
                        }

                        if (assignInferredTypes)
                        {
                            generic.ResolvedType = union;
                        }
                    }

                    break;
                }
            case (InstantiatedClass @class, GenericTypeReference generic):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(@class, generic.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else
                    {
                        var genericPlaceholder =
                            generic.OwnerType.TypeParameters.First(z => z.GenericName == generic.GenericName);

                        if (checkConstraints)
                        {
                            foreach (var constraint in genericPlaceholder.Constraints)
                            {
                                result &= ExpectTypeConstraint(constraint, generic.InstantiatedFrom, @class, actualSourceRange, reportError);
                            }
                        }

                        if (assignInferredTypes)
                        {
                            generic.ResolvedType = @class;
                        }
                    }

                    break;
                }
            case (GenericTypeReference generic, InstantiatedClass @class):
                {
                    if (generic.ResolvedType is not null)
                    {
                        result &= ExpectType(generic.ResolvedType, @class, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else
                    {
                        var genericPlaceholder =
                            generic.OwnerType.TypeParameters.First(z => z.GenericName == generic.GenericName);

                        if (checkConstraints)
                        {
                            foreach (var constraint in genericPlaceholder.Constraints)
                            {
                                result &= ExpectTypeConstraint(constraint, generic.InstantiatedFrom, @class, actualSourceRange, reportError);
                            }
                        }

                        if (assignInferredTypes && result)
                        {
                            generic.ResolvedType = @class;
                        }
                    }

                    break;
                }

            case (GenericTypeReference genericTypeReference, GenericTypeReference expectedGeneric):
                {
                    if (genericTypeReference.ResolvedType is not null && expectedGeneric.ResolvedType is not null)
                    {
                        result &= ExpectType(genericTypeReference.ResolvedType, expectedGeneric.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else if (assignInferredTypes)
                    {
                        if (genericTypeReference.ResolvedType is null && expectedGeneric.ResolvedType is not null)
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
                            if (expectedGeneric != genericTypeReference)
                            {
                                expectedGeneric.ResolvedType = genericTypeReference;
                            }
                        }
                    }

                    break;
                }
            case (FunctionObject functionObject1, FunctionObject functionObject2):
                {
                    result &= ExpectType(functionObject1.ReturnType, functionObject2.ReturnType, actualSourceRange,
                        reportError: false, assignInferredTypes);
                    result &= functionObject1.Parameters.Count == functionObject2.Parameters.Count;
                    result &= functionObject1.Parameters.Zip(functionObject2.Parameters)
                        .All(z => z.First.Mutable == z.Second.Mutable
                                  && ExpectType(z.First.Type, z.Second.Type, actualSourceRange, reportError: false, assignInferredTypes));

                    if (!result && reportError)
                    {
                        AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    }

                    if (functionObject2.MutableReturn && !functionObject1.MutableReturn)
                    {
                        result = false;
                        AddError(TypeCheckerError.FunctionObjectReturnTypeMutabilityMismatch(actualSourceRange));
                    }

                    break;
                }
            case (UnknownInferredType inferred, _):
                {
                    if (inferred.ResolvedType is not null)
                    {
                        ExpectType(inferred.ResolvedType, expected, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else if (assignInferredTypes)
                    {
                        inferred.ResolvedType = expected;
                    }
                    break;
                }
            case (_, UnknownInferredType inferred):
                {
                    if (inferred.ResolvedType is not null)
                    {
                        ExpectType(expected, inferred.ResolvedType, actualSourceRange, reportError, assignInferredTypes);
                    }
                    else if (assignInferredTypes)
                    {
                        inferred.ResolvedType = actual;
                    }
                    break;
                }
            case (UnknownType, _):
            case (_, UnknownType):
                {
                    // just bail out
                    break;
                }
            default:
                {
                    AddError(TypeCheckerError.MismatchedTypes(actualSourceRange, expected, actual));
                    break;
                }
        }

        return result;
    }

    public interface ITypeSignature
    {
        string Name { get; }
        DefId Id { get; }
        IReadOnlyList<GenericPlaceholder> TypeParameters { get; }
        bool IsPublic { get; }
    }

    public record TypeField
    {
        public required ITypeReference Type { get; init; }
        public required string Name { get; init; }
        public required bool IsStatic { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
        public required IExpression? StaticInitializer { get; init; }
    }
}
