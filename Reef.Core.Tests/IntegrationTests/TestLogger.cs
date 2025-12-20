using Microsoft.Extensions.Logging;

namespace Reef.Core.Tests.IntegrationTests;

public class TestLogger(ITestOutputHelper testOutputHelper) : ILogger
{
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        testOutputHelper.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        private NoopDisposable()
        {
        }

        public void Dispose(){}
    }
}