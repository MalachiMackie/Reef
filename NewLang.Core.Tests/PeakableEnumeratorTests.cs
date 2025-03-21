using System.Collections;
using FluentAssertions;

namespace NewLang.Core.Tests;

public class PeakableEnumeratorTests
{
    private readonly PeakableEnumerator<int> _sut = new(((IEnumerable<int>) [1, 2, 3, 4, 5]).GetEnumerator());

    [Fact]
    public void Should_EnumerateCorrectly_When_NotPeaked()
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
    public void Should_PeakFirstValueCorrectly()
    {
        _sut.TryPeak(out var peaked).Should().BeTrue();
        peaked.Should().Be(1);
    }

    [Fact]
    public void Should_PeakLastValueCorrectly()
    {
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.TryPeak(out var peaked).Should().BeTrue();
        peaked.Should().Be(5);
    }

    [Fact]
    public void Should_ReturnFalse_When_NoValueToPeak()
    {
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.MoveNext();
        _sut.TryPeak(out var peaked).Should().BeFalse();
        peaked.Should().Be(0);
    }

    [Fact]
    public void Should_ContinueEnumeratingCorrectly_When_Peaked()
    {
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(1);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(2);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(3);

        _sut.TryPeak(out var peaked).Should().BeTrue();
        peaked.Should().Be(4);

        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(4);
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(5);
        _sut.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Should_PeakTwiceCorrectly()
    {
        _sut.MoveNext().Should().BeTrue();
        _sut.Current.Should().Be(1);
        _sut.TryPeak(out var peaked1).Should().BeTrue();
        peaked1.Should().Be(2);
        _sut.TryPeak(out var peaked2).Should().BeTrue();
        peaked2.Should().Be(2);
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
        
        var peakable = new PeakableEnumerator<int>(outer);
        peakable.Dispose();

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