using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.Logging;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;

namespace Reef.Core;


public class Compiler
{
    public static async Task Compile(
        string workingDirectory,
        bool outputIr,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            workingDirectory = "./";
        }
        var buildDirectory = Path.Join(workingDirectory, "build");

        if (!Directory.Exists(buildDirectory))
        {
            Directory.CreateDirectory(buildDirectory);
        }

        var innerCompiler = new ReefCompiler(new FileSystem(), workingDirectory);

        var typeCheckResults = await innerCompiler.TypeCheck();

        var hadError = false;
        foreach (var (fileName, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.ParserErrors)
            {
                logger.LogError("{fileName}({lineNumber}): Parser Error: {Error}", fileName, error.ReceivedToken?.SourceSpan.Position.LineNumber, error.Format());
                hadError = true;
            }
        }

        foreach (var (fileName, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.TypeCheckerErrors)
            {
                logger.LogError(
                    "{FileName}({LineNumber}): TypeCheckError: {Message}",
                    fileName,
                    error.Range.Start.Position.LineNumber,
                    error.Message);
                hadError = true;
            }
        }

        if (hadError)
        {
            return;
        }

        var programName = "main";

        Debug.Assert(typeCheckResults.Count == 1);
        var parsedProgram = typeCheckResults["main.rf"].Module;

        logger.LogInformation("Lowering...");
        var (loweredProgram, importedModules) = ProgramAbseil.Lower(parsedProgram);


        if (outputIr)
        {
            var stringBuilder = new StringBuilder();
            foreach (var importedModule in importedModules.Prepend(loweredProgram))
            {
                var importedModuleIrStr = PrettyPrinter.PrettyPrintLoweredProgram(importedModule, false, false);
                if (!string.IsNullOrWhiteSpace(importedModuleIrStr))
                {
                    stringBuilder.AppendLine($"Module: {importedModule.Id}");
                    stringBuilder.AppendLine(importedModuleIrStr);
                }
            }

            await File.WriteAllTextAsync(Path.Join(buildDirectory, $"{programName}.ir"), stringBuilder.ToString(), ct);
        }

        logger.LogInformation("Generating Assembly...");
        IReadOnlyList<LoweredModule> allModules = [.. importedModules.Append(loweredProgram)];

        var usefulMethodIds = new TreeShaker(allModules).Shake();

        if (usefulMethodIds.Count == 0)
        {
            logger.LogError("No main method was found");
            return;
        }

        var assembly =
            AssemblyLine.Process(allModules, usefulMethodIds, logger);
        var asmFile = $"{programName}.nasm";
        await File.WriteAllTextAsync(Path.Join(buildDirectory, asmFile), assembly, ct);

        var objFile = $"{programName}.obj";
        // todo: move out to its own process, so Reef.Core doesn't interact with the file system
        var nasmProcess = new Process
        {
            StartInfo = new ProcessStartInfo(
                @"C:\Programs\nasm-3.00\nasm.exe",
                [
                    "-F", "cv8",
                    "-f", "win64",
                    "-o", Path.Join(buildDirectory, objFile),
                    Path.Join(buildDirectory, asmFile),
                ])
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        logger.LogInformation("Assembling...");
        nasmProcess.Start();

        ct.Register(() =>
        {
            nasmProcess.Kill(true);
        });

        var nasmOutput = await nasmProcess.StandardOutput.ReadToEndAsync(ct);
        var nasmError = await nasmProcess.StandardError.ReadToEndAsync(ct);

        if (nasmOutput.Length > 0)
            logger.LogInformation("{NasmOutput}", nasmOutput);
        if (nasmError.Length > 0)
            logger.LogError("NasmError: {Error}", nasmError);

        await nasmProcess.WaitForExitAsync(ct);


        // var msvcVersion = "14.40.33807";
        var msvcVersion = "14.41.34120";


        // var windowsKitsVersion = "10.0.22621.0";
        var windowsKitsVersion = "10.0.22000.0";

        logger.LogInformation("Linking...");

        var runtimeLibraryLocation =
            Path.Join(Path.GetDirectoryName(typeof(Compiler).Assembly.Location),
                "libreef_runtime.a");

        var linkProcess = new Process
        {
            StartInfo = new ProcessStartInfo(
                $@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\{msvcVersion}\bin\HostX64\x64\link.exe",
                [
                    Path.Join(buildDirectory, objFile),
                    "/nologo",
                    "/subsystem:console",
                    $"/out:{Path.Join(buildDirectory, $"{programName}.exe")}",
                    "/machine:x64",
                    runtimeLibraryLocation,
                    $@"C:\Program Files (x86)\Windows Kits\10\Lib\{windowsKitsVersion}\um\x64\kernel32.lib",
                    @$"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\{msvcVersion}\lib\x64\legacy_stdio_definitions.lib",
                    @$"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\{msvcVersion}\lib\x64\legacy_stdio_wide_specifiers.lib",
                    @$"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\{msvcVersion}\lib\x64\vcruntime.lib",
                    @$"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\{msvcVersion}\lib\x64\msvcrt.lib",
                    $@"C:\Program Files (x86)\Windows Kits\10\Lib\{windowsKitsVersion}\ucrt\x64\ucrt.lib",
                    "/debug:full",
                    "/incremental:no"
                ])
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        linkProcess.Start();

        ct.Register(linkProcess.Kill);

        var linkOutput = await linkProcess.StandardOutput.ReadToEndAsync(ct);
        var linkError = await linkProcess.StandardError.ReadToEndAsync(ct);

        if (linkOutput.Length > 0)
            logger.LogInformation("{LinkOutput}", linkOutput);
        if (linkError.Length > 0)
            logger.LogError("{LinkError}", linkError);

        await linkProcess.WaitForExitAsync(ct);

        logger.LogInformation("Done!");
    }

    private static ReadOnlySpan<char> GetSourceRange(string source, SourceRange range)
    {
        return source.AsSpan()[(int)range.Start.Position.Start..(int)(range.End.Position.Start + range.End.Length)];
    }
}
