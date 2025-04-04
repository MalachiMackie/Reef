using System.Collections;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NewLang.Core;

[MustDisposeResource]
public sealed class PeekableEnumerator<T>(IEnumerator<T> inner) : IEnumerator<T>
{
    private T? _peeked;
    // cant rely on _peeked being not null, because T maybe nullable, and null may be a valid value
    private bool _hasPeekedValue;
    private bool _hasPeeked;

    public bool TryPeek([MaybeNullWhen(false)]out T peeked)
    {
        if (_hasPeeked)
        {
            peeked = _hasPeekedValue ? _peeked : default;
            return _hasPeekedValue;
        }

        _hasPeeked = true;
        _hasPeekedValue = inner.MoveNext();
        _peeked = _hasPeekedValue ? inner.Current : default;
        peeked = _peeked;

        return _hasPeekedValue;
    }

    public bool MoveNext()
    {
        if (_hasPeeked)
        {
            _hasPeeked = false;
            return _hasPeekedValue;
        }

        return inner.MoveNext();
    }

    public void Reset()
    {
        _peeked = default;
        _hasPeeked = false;
        inner.Reset();
    }

    public T Current => _hasPeeked ? _peeked! : inner.Current;

    T IEnumerator<T>.Current => Current;

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _peeked = default;
        _hasPeeked = false;
        inner.Dispose();
    }
}