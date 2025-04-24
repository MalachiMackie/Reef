using System.Collections;
using FluentAssertions;

namespace NewLang.Core.Tests;

public class PeekableEnumeratorTests
{
    private readonly PeekableEnumerator<int> _sut = new(((IEnumerable<int>) [1, 2, 3, 4, 5]).GetEnumerator());

    [Fact]
    public void GetCurrent_Should_Throw_When_MoveNextHasNotBeenCalled()
    {
        _sut.Invoking(x => x.Current).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_NotChangeCurrent_When_Peeking()
    {
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.Current.Should().Be(2);
        _sut.TryPeek(out var peeked).Should().BeTrue();
        _sut.Current.Should().Be(2);
        peeked.Should().Be(3);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(3);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(4);
    }

    [Fact]
    public void Should_EnumerateCorrectly_When_NotPeeked()
    {
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(1);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(2);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(3);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(4);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(5);
        _sut.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Should_PeekFirstValueCorrectly()
    {
        _sut.TryPeek(out var peeked).Should().BeTrue();
        peeked.Should().Be(1);
    }

    [Fact]
    public void Should_PeekLastValueCorrectly()
    {
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.TryPeek(out var peeked).Should().BeTrue();
        peeked.Should().Be(5);
    }

    [Fact]
    public void Should_ReturnFalse_When_NoValueToPeek()
    {
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.TryPeek(out var peeked).Should().BeFalse();
        peeked.Should().Be(0);
    }

    [Fact]
    public void Should_ContinueEnumeratingCorrectly_When_Peeked()
    {
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(1);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(2);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(3);

        _sut.TryPeek(out var peeked).Should().BeTrue();
        peeked.Should().Be(4);

        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(4);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(5);
        _sut.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Should_PeekTwiceCorrectly()
    {
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(1);
        _sut.TryPeek(out var peeked1).Should().BeTrue();
        peeked1.Should().Be(2);
        _sut.TryPeek(out var peeked2).Should().BeTrue();
        peeked2.Should().Be(2);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(2);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(3);
    }

    [Fact]
    public void Should_DisposeOfInnerEnumerator()
    {
        var outer = new DisposeTestEnumerator();
        outer.Disposed.Should().BeFalse();
        
        var peekable = new PeekableEnumerator<int>(outer);
        peekable.Dispose();

        outer.Disposed.Should().BeTrue();
    }

    private class DisposeTestEnumerator : IEnumerator<int>
    {
        private readonly IEnumerator<int> _enumeratorImplementation = null!;
        public bool MoveNext()
        {
            return _enumeratorImplementation.MoveNext();
        }

        public void Reset()
        {
            _enumeratorImplementation.Reset();
        }

        int IEnumerator<int>.Current => _enumeratorImplementation.Current;

        object? IEnumerator.Current => ((IEnumerator)_enumeratorImplementation).Current;

        public bool Disposed { get; private set; }
        public void Dispose()
        {
            Disposed = true;
        }
    }
}