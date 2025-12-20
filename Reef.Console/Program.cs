using Microsoft.Extensions.Logging;
using Reef.Core;

await Compiler.Compile(args[0], true, ConsoleLogger.Instance, CancellationToken.None);

file class ConsoleLogger : ILogger
{
    public static readonly ConsoleLogger Instance = new();

    private ConsoleLogger()
    {
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return NoopDisposable.Instance;
    }
}

file sealed class NoopDisposable : IDisposable
{
    public static readonly NoopDisposable Instance = new(); 
        
    private NoopDisposable()
    {
    }

    public void Dispose()
    {
    }

}