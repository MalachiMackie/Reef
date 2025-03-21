using System.Collections;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NewLang.Core;

[MustDisposeResource]
public class PeakableEnumerator<T>(IEnumerator<T> inner) : IEnumerator<T>
{
    private T? _peaked;
    // cant rely on _peaked being not null, because T maybe nullable, and null may be a valid value
    private bool _hasPeakedValue;
    private bool _hasPeaked;

    public bool TryPeak([MaybeNullWhen(false)]out T peaked)
    {
        if (_hasPeaked)
        {
            peaked = _hasPeakedValue ? _peaked : default;
            return _hasPeakedValue;
        }

        _hasPeaked = true;
        _hasPeakedValue = inner.MoveNext();
        _peaked = _hasPeakedValue ? inner.Current : default;
        peaked = _peaked;

        return _hasPeakedValue;
    }

    public bool MoveNext()
    {
        if (_hasPeaked)
        {
            _hasPeaked = false;
            return _hasPeakedValue;
        }

        return inner.MoveNext();
    }

    public void Reset()
    {
        _peaked = default;
        _hasPeaked = false;
        inner.Reset();
    }

    public T Current => _hasPeaked ? _peaked! : inner.Current;

    T IEnumerator<T>.Current => Current;

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _peaked = default;
        _hasPeaked = false;
        inner.Dispose();
    }
}