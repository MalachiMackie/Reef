using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Reef.Core.Tests.IntegrationTests;

public class IntegrationTestBase
{
    protected static async Task SetupTest(string reefSource, [CallerMemberName] string testName = "", [CallerFilePath] string callerFilePath = "")
    {
        var testRunFolder = TestRunFolder(testName, callerFilePath);

        if (Directory.Exists(testRunFolder))
        {
            DeleteDirectory(testRunFolder);
        }
        Directory.CreateDirectory(testRunFolder);

        var fileName = $"{testName}.rf";
        await File.WriteAllTextAsync(Path.Join(testRunFolder, fileName), reefSource);
    }

    protected static async Task<TestRunOutput> Run([CallerMemberName] string testName = "", [CallerFilePath] string callerFilePath = "")
    {
        var testRunFolder = TestRunFolder(testName, callerFilePath);
        await Compiler.Compile(Path.Join(testRunFolder, $"{testName}.rf"));

        var exeFileName = Path.Join(testRunFolder, "build", $"{testName}.exe");
        if (!File.Exists(exeFileName))
        {
            Assert.Fail($"Expected Exe file to exist: {exeFileName}");
        }
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(exeFileName)
            {
                RedirectStandardOutput = true
            },
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new TestRunOutput(process.ExitCode, output);
    }

    private static string TestRunFolder(string testName, string callerFilePath)
    {
        var callerFileName = Path.GetFileNameWithoutExtension(callerFilePath);
        var callerFolder = Path.GetDirectoryName(callerFilePath);
        return Path.Join(callerFolder, $"{callerFileName}_TestRuns", testName);
    }

    private static void DeleteDirectory(string directory)
    {
        foreach (var subDirectory in Directory.EnumerateDirectories(directory))
        {
            DeleteDirectory(subDirectory);
        }
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.Delete(file);
        }
        Directory.Delete(directory);
    }
}

public record TestRunOutput(int ExitCode, string StandardOutput);
