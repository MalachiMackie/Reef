using System.Diagnostics;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Reef.Core.Abseil;
using Reef.Core.LoweredExpressions;

namespace Reef.Core;

public class Compiler
{
    public static async Task<bool> CompileTest(
        string? workingDirectory,
        bool outputIr,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            workingDirectory = "./";
        }
        var buildDirectory = Path.Join(workingDirectory, "build-test");

        if (!Directory.Exists(buildDirectory))
        {
            Directory.CreateDirectory(buildDirectory);
        }

        var innerCompiler = new ReefCompiler(new FileSystem(), new ModuleId("main"), logger, workingDirectory);

        var (typeCheckResults, moduleIdToFileName, mainModuleId, importedModules) = await innerCompiler.TypeCheck();

        var hadError = false;

        foreach (var (moduleId, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.TokenizerErrors)
            {
                logger.LogError("{fileName}({Position}): {Error}", moduleIdToFileName[moduleId], error.SourceSpan.Position.ToString() ?? "EOF", error.Message);
                hadError = true;
            }
        }

        foreach (var (moduleId, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.ParserErrors)
            {
                logger.LogError("{fileName}({Position}): {Error}", moduleIdToFileName[moduleId], error.SourceRange?.Start.Position.ToString() ?? "EOF", error.Message);
                hadError = true;
            }
        }

        foreach (var (fileName, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.TypeCheckerErrors)
            {
                logger.LogError(
                    "{FileName}({Position}): TypeCheckError: {Message}",
                    fileName,
                    error.Range.Start.Position,
                    error.Message);
                hadError = true;
            }
        }

        if (hadError)
        {
            return false;
        }

        var programName = "main-test";

        logger.LogInformation("Lowering...");
        var allModules = typeCheckResults.Select(x => x.Value.Module.NotNull()).Concat(importedModules).ToDictionary(x => x.ModuleId);

        var loweredProgram = ProgramAbseil.Lower(
            allModules,
            mainModuleId,
            generateTestMain: true
        );

        if (outputIr)
        {
            await File.WriteAllTextAsync(
                Path.Join(buildDirectory, $"{programName}.ir"),
                PrettyPrinter.PrettyPrintLoweredProgram(loweredProgram),
                ct);
        }

        return await BuildExeFromLoweredCode(loweredProgram, programName, buildDirectory, logger, ct);
    }

    public static async Task<bool> Compile(
        string? workingDirectory,
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

        var innerCompiler = new ReefCompiler(new FileSystem(), new ModuleId("main"), logger, workingDirectory);

        var (typeCheckResults, moduleIdToFileName, mainModuleId, importedModules) = await innerCompiler.TypeCheck();

        var hadError = false;

        foreach (var (moduleId, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.TokenizerErrors)
            {
                logger.LogError("{fileName}({Position}): {Error}", moduleIdToFileName[moduleId], error.SourceSpan.Position.ToString() ?? "EOF", error.Message);
                hadError = true;
            }
        }

        foreach (var (moduleId, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.ParserErrors)
            {
                logger.LogError("{fileName}({Position}): {Error}", moduleIdToFileName[moduleId], error.SourceRange?.Start.Position.ToString() ?? "EOF", error.Message);
                hadError = true;
            }
        }

        foreach (var (fileName, typeCheckResult) in typeCheckResults)
        {
            foreach (var error in typeCheckResult.TypeCheckerErrors)
            {
                logger.LogError(
                    "{FileName}({Position}): TypeCheckError: {Message}",
                    fileName,
                    error.Range.Start.Position,
                    error.Message);
                hadError = true;
            }
        }

        if (hadError)
        {
            return false;
        }

        var programName = "main";

        logger.LogInformation("Lowering...");
        var loweredProgram = ProgramAbseil.Lower(
            typeCheckResults.Select(x => x.Value.Module.NotNull()).Concat(importedModules).ToDictionary(x => x.ModuleId),
            mainModuleId
        );


        if (outputIr)
        {
            await File.WriteAllTextAsync(
                Path.Join(buildDirectory, $"{programName}.ir"),
                PrettyPrinter.PrettyPrintLoweredProgram(loweredProgram),
                ct);
        }

        return await BuildExeFromLoweredCode(loweredProgram, programName, buildDirectory, logger, ct);
    }

    private static async Task<(bool, string)> Assemble(string programName, string buildDirectory, string asmFile, ILogger logger, CancellationToken ct)
    {
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

        var nasmError = await nasmProcess.StandardError.ReadToEndAsync(ct);

        if (nasmError.Length > 0)
            logger.LogError("NasmError: {Error}", nasmError);

        var nasmOutput = await nasmProcess.StandardOutput.ReadToEndAsync(ct);

        if (nasmOutput.Length > 0)
            logger.LogInformation("{NasmOutput}", nasmOutput);

        await nasmProcess.WaitForExitAsync(ct);

        return (nasmProcess.ExitCode == 0, objFile);
    }

    // cache dev env vars because they don't change
    private static Dictionary<string, string?>? _devEnvVars;
    private static readonly Lock _devEnvVarsLock = new();
    private static async Task<Dictionary<string, string?>> GetVSDevEnvVars(ILogger logger, CancellationToken ct)
    {
        lock (_devEnvVarsLock)
        {
            if (_devEnvVars is not null)
            {
                logger.LogInformation("Using cached dev env vars");
                return _devEnvVars;
            }
            logger.LogInformation("No cached dev env vars found");
        }

        var vsDevCmd = @"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{vsDevCmd}\" -arch=x64 -host_arch=x64 && set\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var env = new Dictionary<string, string?>();

        using var p = Process.Start(psi) ?? throw new InvalidOperationException();

        var lines = await p.StandardOutput.ReadToEndAsync(ct);
        foreach (var line in lines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = line.IndexOf('=');
            if (idx > 0)
            {
                var key = line[..idx];
                var value = line[(idx + 1)..];
                env[key] = value;
            }
        }
        await p.WaitForExitAsync(ct);

        lock (_devEnvVarsLock)
        {
            if (_devEnvVars is not null)
            {
                // devEnvVars was written to in the time we ran the command, so just discard what we just did
                return _devEnvVars;
            }

            _devEnvVars = env;
        }

        return _devEnvVars;
    }

    private static async Task<bool> Link(
        string buildDirectory,
        string objFile,
        string programName,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogInformation("Linking...");

        var assemblyDirectory = Path.GetDirectoryName(typeof(Compiler).Assembly.Location).NotNull();

        var runtimeLibraryLocation = Path.Combine(assemblyDirectory, "reef-runtime.lib");
        var runtimeLibraryPdb = Path.Combine(assemblyDirectory, "reef-runtime.pdb");
        File.Copy(runtimeLibraryPdb, Path.Combine(buildDirectory, "reef-runtime.pdb"), overwrite: true);

        var vsPath = await GetVSInstallPath(logger, ct);
        if (string.IsNullOrWhiteSpace(vsPath))
        {
            throw new InvalidOperationException("Could not find vs installation");
        }

        logger.LogInformation("VS Installation Path: {Path}", vsPath);

        var msvcBase = Path.Combine(vsPath, "VC", "Tools", "MSVC");

        var versionDir = Directory.GetDirectories(msvcBase)
            .OrderByDescending(x => x)
            .First();

        var clPath = Path.Combine(versionDir, "bin", "HostX64", "x64", "cl.exe");
        var linkPath = Path.Combine(versionDir, "bin", "HostX64", "x64", "link.exe");

        var linkProcessStartInfo = new ProcessStartInfo(
            linkPath,
            [
                // "/Zs", // no compilation, just linking
                $"{Path.Join(buildDirectory, objFile)}",

                // "/link", // following are linker flags
                "/nologo",
                "/subsystem:console",
                $"/out:{Path.Join(buildDirectory, $"{programName}.exe")}",
                "/machine:x64",
                "/debug:full",
                "/incremental:no",
                "/defaultlib:libcmt.lib",
                runtimeLibraryLocation,
            ])
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        var env = await GetVSDevEnvVars(logger, ct);

        foreach (var kv in env)
        {
            linkProcessStartInfo.Environment[kv.Key] = kv.Value;
        }

        var linkProcess = new Process
        {
            StartInfo = linkProcessStartInfo
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

        return linkProcess.ExitCode == 0;
    }

    private static string? _vsInstallPath;
    private static readonly Lock _vsInstallPathLock = new();

    private static async Task<string> GetVSInstallPath(ILogger logger, CancellationToken ct)
    {
        lock (_vsInstallPathLock)
        {
            if (_vsInstallPath is not null)
            {
                logger.LogInformation("Using cached vs install path");
                return _vsInstallPath;
            }
            logger.LogInformation("No cached vs install path found");
        }

        var vswhere = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";

        var psi = new ProcessStartInfo
        {
            FileName = vswhere,
            Arguments = "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var vsInstallPath = await Process.Start(psi)!.StandardOutput.ReadToEndAsync(ct);

        lock (_vsInstallPathLock)
        {
            _vsInstallPath ??= vsInstallPath.Trim();
        }

        return _vsInstallPath;
    }

    private static async Task<bool> BuildExeFromLoweredCode(LoweredProgram loweredProgram, string programName, string buildDirectory, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Generating Assembly...");

        var usefulMethodIds = new TreeShaker(loweredProgram).Shake();

        if (usefulMethodIds.Count == 0)
        {
            logger.LogError("No main method was found");
            return false;
        }

        var assembly =
            AssemblyLine.Process(loweredProgram, usefulMethodIds, logger);
        var asmFile = $"{programName}.nasm";
        await File.WriteAllTextAsync(Path.Join(buildDirectory, asmFile), assembly, ct);

        var (nasmResult, objFile) = await Assemble(programName, buildDirectory, asmFile, logger, ct);
        if (!nasmResult)
        {
            return false;
        }

        var linkResult = await Link(buildDirectory, objFile, programName, logger, ct);

        logger.LogInformation("Done!");

        return linkResult;
    }
}
