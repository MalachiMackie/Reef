using System.Diagnostics.CodeAnalysis;

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
            && (variable is not FunctionSignatureParameter { ContainingFunction: var parameterOwner }
                || parameterOwner != CurrentFunctionSignature)
            && (variable is not FieldVariable { ContainingSignature: var fieldOwner }
                || fieldOwner != CurrentTypeSignature)
            && (variable is not LocalVariable { ContainingFunction: var localOwner }
                || localOwner != CurrentFunctionSignature)
            && (variable is not ThisVariable { ContainingFunction: var thisOwner }
                || thisOwner != CurrentFunctionSignature)
            && !CurrentFunctionSignature.AccessedOuterVariables.Contains(variable))
        {
            if (CurrentFunctionSignature.IsStatic)
            {
                _errors.Add(TypeCheckerError.StaticLocalFunctionAccessesOuterVariable(name));
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
            var localVariables = CurrentFunctionSignature?.LocalVariables ?? _program.TopLevelLocalVariables;
            localVariables.Add(localVariable);
        }

        return _typeCheckingScopes.Peek().TryAddVariable(name, variable);
    }

    private void AddScopedVariable(string name, IVariable variable)
    {
        if (variable is LocalVariable localVariable)
        {
            var localVariables = CurrentFunctionSignature?.LocalVariables ?? _program.TopLevelLocalVariables;
            localVariables.Add(localVariable);
        }

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
        IEnumerable<GenericPlaceholder>? genericPlaceholders = null)
    {
        var currentScope = _typeCheckingScopes.Peek();

        _typeCheckingScopes.Push(new TypeCheckingScope(
            currentScope,
            new Dictionary<string, FunctionSignature>(currentScope.Functions),
            expectedReturnType ?? currentScope.ExpectedReturnType,
            currentTypeSignature ?? currentScope.CurrentTypeSignature,
            currentFunctionSignature ?? currentScope.CurrentFunctionSignature,
            [.. currentScope.GenericPlaceholders, .. genericPlaceholders ?? []]));

        return new ScopeDisposable(PopScope);
    }

    private void PopScope() => _typeCheckingScopes.Pop();

    private record TypeCheckingScope(
        TypeCheckingScope? ParentScope,
        Dictionary<string, FunctionSignature> Functions,
        ITypeReference ExpectedReturnType,
        ITypeSignature? CurrentTypeSignature,
        FunctionSignature? CurrentFunctionSignature,
        HashSet<GenericPlaceholder> GenericPlaceholders)
    {
        private Dictionary<string, IVariable> CurrentScopeVariables { get; } = new();

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
