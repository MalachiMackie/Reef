using System.Diagnostics.CodeAnalysis;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private IVariable GetScopedVariable(string name)
    {
        return _typeCheckingScopes.Peek().GetVariable(name);
    }

    private IEnumerable<IVariable> GetScopedVariables()
    {
        return _typeCheckingScopes.Peek().GetVariables();
    }

    private bool TryGetScopedVariable(StringToken name, [NotNullWhen(true)] out IVariable? variable)
    {
        if (!_typeCheckingScopes.Peek().TryGetVariable(name.StringValue, out variable))
        {
            return false;
        }

        if (CurrentFunctionSignature is not null
            && (
                (variable is FunctionSignatureParameter { ContainingFunction: var parameterOwner }
                && parameterOwner != CurrentFunctionSignature)
            || (variable is FieldVariable { IsStaticField: false }
                && CurrentFunctionSignature.OwnerType is null)
            || (variable is LocalVariable { ContainingFunction: var localOwner }
                && localOwner != CurrentFunctionSignature)
            || (variable is ThisVariable && CurrentFunctionSignature.OwnerType is null))
            && !CurrentFunctionSignature.AccessedOuterVariables.Contains(variable))
        {
            if (CurrentFunctionSignature.IsStatic)
            {
                AddError(TypeCheckerError.StaticLocalFunctionAccessesOuterVariable(name));
            }
            else
            {
                CurrentFunctionSignature.AccessedOuterVariables.Add(variable);
                variable.ReferencedInClosure = true;
            }
        }

        return true;
    }

    private bool TryAddScopedVariable(string name, IVariable variable)
    {
        if (variable is LocalVariable localVariable)
        {
            var localVariables = CurrentFunctionSignature?.LocalVariables ?? _modules[CurrentModuleId].TopLevelLocalVariables;
            localVariables.Add(localVariable);
        }

        return _typeCheckingScopes.Peek().TryAddVariable(name, variable);
    }

    private void AddScopedVariable(string name, IVariable variable)
    {
        if (variable is LocalVariable localVariable)
        {
            var localVariables = CurrentFunctionSignature?.LocalVariables ?? _modules[CurrentModuleId].TopLevelLocalVariables;
            localVariables.Add(localVariable);
        }

        _typeCheckingScopes.Peek().AddVariable(name, variable);
    }

    private bool IsVariableNameAvailable(string name, [NotNullWhen(false)] out Token? conflictingToken)
    {
        if (_typeCheckingScopes.Peek().TryGetVariable(name, out var variable))
        {
            conflictingToken = variable.Name;
            return false;
        }

        if (CurrentTypeSignature is ClassSignature classSignature)
        {
            var inStaticFunction = CurrentFunctionSignature is { IsStatic: true };

            var fn = classSignature.Functions.FirstOrDefault(x => x.Name == name);
            var field = classSignature.Fields.FirstOrDefault(x => x.Name == name);

            conflictingToken = fn?.NameToken ?? field?.NameToken;

            return (fn, field) switch
            {
                (null, null) => true,
                // variable name is available if we are in a static function but the found
                // member is not available in a static context (ie an instance field or function)
                ({ IsStatic: false }, _) or
                (_, { IsStatic: false }) => inStaticFunction,
                // at least one was not null, so not available
                _ => false
            };
        }
        else if (CurrentTypeSignature is UnionSignature unionSignature)
        {
            var inStaticFunction = CurrentFunctionSignature is { IsStatic: true };

            var fn = unionSignature.Functions.FirstOrDefault(x => x.Name == name);

            conflictingToken = fn?.NameToken;

            return fn switch
            {
                null => true,
                { IsStatic: false } => inStaticFunction,
                _ => false
            };
        }

        conflictingToken = null;

        return true;
    }

    private ScopeDisposable PushScope(
        ITypeSignature? currentTypeSignature = null,
        FunctionSignature? currentFunctionSignature = null,
        ITypeReference? expectedReturnType = null,
        IEnumerable<GenericPlaceholder>? genericPlaceholders = null,
        DefId? defId = null,
        ModuleId? moduleId = null,
        IReadOnlyList<FunctionSignature>? functionSignatures = null,
        IReadOnlyList<ModuleImport>? moduleImports = null,
        Block? block = null,
        IExpression? loopExpression = null)
    {
        var currentScope = _typeCheckingScopes.Peek();

        _typeCheckingScopes.Push(new TypeCheckingScope(
            currentScope,
            functionSignatures is null ? [] : [.. functionSignatures],
            expectedReturnType ?? currentScope.ExpectedReturnType,
            currentTypeSignature ?? currentScope.CurrentTypeSignature,
            currentFunctionSignature ?? currentScope.CurrentFunctionSignature,
            [.. currentScope.GenericPlaceholders, .. genericPlaceholders ?? []],
            defId ?? currentScope.CurrentDefId,
            moduleId ?? currentScope.ModuleId,
            moduleImports ?? [],
            block,
            loopExpression ?? currentScope.LoopExpression));

        return new ScopeDisposable(PopScope);
    }

    private void PopScope() => _typeCheckingScopes.Pop();

    private record TypeCheckingScope(
        TypeCheckingScope? ParentScope,
        List<FunctionSignature> Functions,
        ITypeReference? ExpectedReturnType,
        ITypeSignature? CurrentTypeSignature,
        FunctionSignature? CurrentFunctionSignature,
        HashSet<GenericPlaceholder> GenericPlaceholders,
        DefId? CurrentDefId,
        ModuleId? ModuleId,
        IReadOnlyList<ModuleImport> ModuleImports,
        Block? Block,
        IExpression? LoopExpression)
    {
        private Dictionary<string, IVariable> CurrentScopeVariables { get; } = [];
        public GrabExpression? GrabExpression { get; set; }

        public IVariable GetVariable(string name)
        {
            if (ParentScope?.TryGetVariable(name, out var parentScopeVariable) ?? false)
            {
                return parentScopeVariable;
            }

            return CurrentScopeVariables[name];
        }

        public bool TryGetVariable(string name, [NotNullWhen(true)] out IVariable? variable)
        {
            if (ParentScope?.TryGetVariable(name, out variable) ?? false)
            {
                return true;
            }

            return CurrentScopeVariables.TryGetValue(name, out variable);
        }

        public bool TryAddVariable(string name, IVariable variable)
        {
            return CurrentScopeVariables.TryAdd(name, variable);
        }

        public void AddVariable(string name, IVariable variable)
        {
            CurrentScopeVariables.Add(name, variable);
        }

        public bool ContainsVariable(string name)
        {
            return CurrentScopeVariables.ContainsKey(name) || (ParentScope?.ContainsVariable(name) ?? false);
        }

        public IEnumerable<IVariable> GetVariables()
        {
            return [.. CurrentScopeVariables.Values, .. ParentScope?.GetVariables() ?? []];
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

}
