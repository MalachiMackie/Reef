using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reef.Core;

var cts = new CancellationTokenSource();

if (args.Length == 0)
{
    Console.WriteLine("Expected command");
    return;
}

Console.CancelKeyPress += (sender, eventArgs) =>
{
    cts.Cancel();
    eventArgs.Cancel = true;
};

switch (args[0].ToLower())
{
    case "build":
        {
            if (!await Compiler.Compile(args.Length >= 2 ? args[1] : null, true, ConsoleLogger.Instance, cts.Token))
            {
                Console.WriteLine("Compilation failed");
                Environment.ExitCode = 1;
                return;
            }
            break;
        }
    case "run":
        {
            if (!await Compiler.Compile(null, true, ConsoleLogger.Instance, cts.Token))
            {
                Console.WriteLine("Compilation failed");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine("Running...");

            var exeFileName = Path.Join("./", "build", "main.exe");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(exeFileName),
            };

            cts.Token.Register(process.Kill);

            process.Start();

            await process.WaitForExitAsync();

            Environment.ExitCode = process.ExitCode;

            break;
        }
    case "test":
        {
            break;
        }
}

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
