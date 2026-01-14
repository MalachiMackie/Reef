using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;

namespace Reef.Core;


public class Compiler
{
    public static async Task Compile(
        string inputFile, 
        bool outputIr,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inputFile)
            || Path.GetExtension(inputFile) is not ".rf")
        {
            logger.LogError("Expected a single .rf file as an argument");
            return;
        }

        var buildDirectory = Path.GetDirectoryName(inputFile);
        if (string.IsNullOrEmpty(buildDirectory))
        {
            buildDirectory = "./";
        }
        buildDirectory = Path.Join(buildDirectory, "build");

        if (!Directory.Exists(buildDirectory))
        {
            Directory.CreateDirectory(buildDirectory);
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFile);

        var contents = await File.ReadAllTextAsync(inputFile, ct);

        logger.LogInformation("Tokenizing...");
        var tokens = Tokenizer.Tokenize(contents);

        logger.LogInformation("Parsing...");
        var parsedProgram = Parser.Parse(fileNameWithoutExtension, tokens);
        if (parsedProgram.Errors.Count > 0)
        {
            foreach (var error in parsedProgram.Errors)
            {
                logger.LogError("Error: {Error}", error.Format());
            }

            return;
        }

        logger.LogInformation("TypeChecking...");
        var typeCheckErrors = TypeChecker.TypeCheck(parsedProgram.ParsedProgram);
        if (typeCheckErrors.Count > 0)
        {
            foreach (var error in typeCheckErrors)
            {
                logger.LogError("Error: {Message}. {Contents}", error.Message, GetSourceRange(contents, error.Range).ToString());
            }

            return;
        }

        logger.LogInformation("Lowering...");
        var (loweredProgram, importedModules) = ProgramAbseil.Lower(parsedProgram.ParsedProgram);


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
            
            await File.WriteAllTextAsync(Path.Join(buildDirectory, $"{fileNameWithoutExtension}.ir"), stringBuilder.ToString(), ct);
        }

        logger.LogInformation("Generating Assembly...");
        IReadOnlyList<LoweredModule> allModules = [..importedModules.Append(loweredProgram)];

        var usefulMethodIds = new TreeShaker(allModules).Shake();

        if (usefulMethodIds.Count == 0)
        {
            logger.LogError("No main method was found");
            return;
        }

        var assembly =
            AssemblyLine.Process(allModules, usefulMethodIds, logger);
        var asmFile = $"{fileNameWithoutExtension}.nasm";
        await File.WriteAllTextAsync(Path.Join(buildDirectory, asmFile), assembly, ct);

        var objFile = $"{fileNameWithoutExtension}.obj";
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
                    $"/out:{Path.Join(buildDirectory, $"{fileNameWithoutExtension}.exe")}",
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

        ct.Register(() =>
        {
            linkProcess.Kill();
        });

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
