#define V2

using System.Diagnostics;
using Reef.Core.Abseil;
using Reef.Core.Abseil.New;
using Reef.Core.IL;
using Reef.Core.LoweredExpressions.New;
using Reef.Core.TypeChecking;

namespace Reef.Core;


public class Compiler
{
    public static async Task Compile(
        string inputFile, 
        bool outputIr,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inputFile)
            || Path.GetExtension(inputFile) is not ".rf")
        {
            await Console.Error.WriteLineAsync("Expected a single .rf file as an argument");
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

        Console.WriteLine("Tokenizing...");
        var tokens = Tokenizer.Tokenize(contents);

        Console.WriteLine("Parsing...");
        var parsedProgram = Parser.Parse(fileNameWithoutExtension, tokens);
        if (parsedProgram.Errors.Count > 0)
        {
            foreach (var error in parsedProgram.Errors)
            {
                await Console.Error.WriteAsync("Error: ");
                Console.Error.WriteLine(error);
            }

            return;
        }

        Console.WriteLine("TypeChecking...");
        var typeCheckErrors = TypeChecker.TypeCheck(parsedProgram.ParsedProgram);
        if (typeCheckErrors.Count > 0)
        {
            foreach (var error in typeCheckErrors)
            {
                await Console.Error.WriteAsync("Error: ");
                await Console.Error.WriteLineAsync(error.Message);
                await Console.Error.WriteLineAsync(contents[(int)error.Range.Start.Position.Start..(int)(error.Range.End.Position.Start + error.Range.End.Length)]);
            }

            return;
        }

        Console.WriteLine("Lowering...");
        #if V2
        var (newLoweredProgram, newImportedModules) = NewProgramAbseil.Lower(parsedProgram.ParsedProgram);
        #else
        var loweredProgram = ProgramAbseil.Lower(parsedProgram.ParsedProgram);
        #endif


        if (outputIr)
        {
            var irStr = NewPrettyPrinter.PrettyPrintLoweredProgram(newLoweredProgram, false, false);
            await File.WriteAllTextAsync(Path.Join(buildDirectory, $"{fileNameWithoutExtension}.ir"), irStr, ct);
        }

        Console.WriteLine("Compiling to IL...");
        #if V2
        #else
        var (il, importedModules) = ILCompile.CompileToIL(loweredProgram);
#endif

        Console.WriteLine("Generating Assembly...");
#if V2
        IReadOnlyList<NewLoweredModule> allNewModules = [..newImportedModules.Append(newLoweredProgram)];
        #else
        IReadOnlyList<ReefILModule> allModules = [..importedModules.Append(il)];
#endif

        #if V2
        var newUsefulMethodIds = new NewTreeShaker(allNewModules).Shake();
        #else
        var usefulMethodIds = new TreeShaker(allModules).Shake();
#endif

        var assembly =
#if V2
            AssemblyLine2.Process(allNewModules, newUsefulMethodIds);
                #else
                AssemblyLine.Process(allModules, usefulMethodIds);
#endif
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

        Console.WriteLine("Assembling...");
        nasmProcess.Start();
        
        ct.Register(() =>
        {
            nasmProcess.Kill(true);
        });

        var nasmOutput = await nasmProcess.StandardOutput.ReadToEndAsync(ct);
        var nasmError = await nasmProcess.StandardError.ReadToEndAsync(ct);

        if (nasmOutput.Length > 0)
            Console.WriteLine(nasmOutput);
        if (nasmError.Length > 0)
            await Console.Error.WriteLineAsync(nasmError);

        await nasmProcess.WaitForExitAsync(ct);


        // var msvcVersion = "14.40.33807";
        var msvcVersion = "14.41.34120";


        // var windowsKitsVersion = "10.0.22621.0";
        var windowsKitsVersion = "10.0.22000.0";

        Console.WriteLine("Linking...");
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
            Console.WriteLine(linkOutput);
        if (linkError.Length > 0)
            await Console.Error.WriteLineAsync(linkError);

        await linkProcess.WaitForExitAsync(ct);

        Console.WriteLine("Done!");
    }
}
