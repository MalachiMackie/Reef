using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reef.Core;

var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));



if (args.Length == 0)
{
    Console.WriteLine("Expected command");
    return;
}

var argsQueue = new Queue<string>(args);

Console.CancelKeyPress += (sender, eventArgs) =>
{
    cts.Cancel();
    eventArgs.Cancel = true;
};

var command = argsQueue.Dequeue();

string? workingDirectory = null;
var logLevel = LogLevel.Information;
var timeout = true;

while (argsQueue.TryDequeue(out var nextArg))
{
    if (nextArg.StartsWith("--"))
    {
        switch (nextArg.ToLower())
        {
            case "--log-level":
                {
                    logLevel = Enum.Parse<LogLevel>(argsQueue.Dequeue(), ignoreCase: true);
                    break;
                }
            case "--no-timeout":
                {
                    timeout = false;
                    break;
                }
            default:
                {
                    throw new InvalidOperationException($"Unknown option: \"{nextArg}\"");
                }
        }
        continue;
    }

    if (workingDirectory is null)
    {
        workingDirectory = nextArg;
        continue;
    }

    throw new InvalidOperationException($"Unknown argument/option: \"{nextArg}\"");
}

if (timeout)
{
    _ = Task.Run(async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        // if we haven't exited the process 3 seconds after cancellation is requested, just kill the process
        await Task.Delay(TimeSpan.FromSeconds(3));

        await Console.Error.WriteLineAsync(
            "Cancellation has been requested, and we haven't cancelled yet. probably stuck in a loop somewhere");

        Environment.Exit(1);
    });
}

var logger = new ConsoleLogger(logLevel);

switch (command.ToLower())
{
    case "build":
        {
            if (!await Compiler.Compile(workingDirectory, outputIr: true, logger, cts.Token))
            {
                logger.LogError("Compilation failed");
                Environment.ExitCode = 1;
                return;
            }
            break;
        }
    case "run":
        {
            if (!await Compiler.Compile(workingDirectory, outputIr: true, logger, cts.Token))
            {
                logger.LogError("Compilation failed");
                Environment.ExitCode = 1;
                return;
            }
            logger.LogInformation("Running...");

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
            if (!await Compiler.CompileTest(workingDirectory, outputIr: true, logger, cts.Token))
            {
                logger.LogError("Compilation failed");
                Environment.ExitCode = 1;
                return;
            }

            var exeFileName = Path.Join("./", "build-test", "main-test.exe");

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
}

file class ConsoleLogger(LogLevel logLevel) : ILogger
{
    private readonly LogLevel _logLevel = logLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _logLevel;
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
