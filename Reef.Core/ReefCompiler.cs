using System.Diagnostics;
using System.IO.Abstractions;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public class ReefCompiler(IFileSystem fileSystem, string? workingDirectory = null)
{
    public async Task<(Dictionary<ModuleId, TypeCheckResult>, Dictionary<ModuleId, string>)> TypeCheck()
    {
        var currentDirectory = workingDirectory ?? fileSystem.Directory.GetCurrentDirectory();

        var parsedModules = await GetReefModules(currentDirectory, currentDirectory).ToArrayAsync();
        var modules = new List<LangModule>();
        var moduleIdToFileName = new Dictionary<ModuleId, string>();

        var parseErrors = new Dictionary<ModuleId, IReadOnlyList<ParserError>>();
        foreach (var (parseResult, fileName) in parsedModules)
        {
            var moduleId = parseResult.ParsedModule.ModuleId;
            moduleIdToFileName[moduleId] = fileName;
            modules.Add(parseResult.ParsedModule);
            if (parseResult.Errors.Count > 0)
            {
                parseErrors[moduleId] = parseResult.Errors;
            }
        }

        var typeCheckErrors = TypeChecker.TypeCheck(modules);

        return (parsedModules
            .ToDictionary(x => x.Item1.ParsedModule.ModuleId, x => new TypeCheckResult(
                x.Item1.ParsedModule,
                parseErrors.GetValueOrDefault(x.Item1.ParsedModule.ModuleId, []),
                typeCheckErrors.GetValueOrDefault(x.Item1.ParsedModule.ModuleId, []))), moduleIdToFileName);
    }

    private async IAsyncEnumerable<(Parser.ParseResult, string fileName)> GetReefModules(string projectDir, string directory)
    {
        var contentTasks = fileSystem.Directory.EnumerateFiles(directory)
            .Where(x => Path.GetExtension(x) == ".rf")
            .Select(async fileName => (fileName, await fileSystem.File.ReadAllTextAsync(fileName)));

        var contents = await Task.WhenAll(contentTasks);

        foreach (var (fileName, fileContents) in contents)
        {
            var projectRelativeFileName = GetProjectRelativeFileName(projectDir, fileName);
            var moduleId = GetModuleIdFromProjectRelativeFileName(projectRelativeFileName);
            var tokens = Tokenizer.Tokenize(fileContents);
            var parseResult = Parser.Parse(moduleId, tokens);

            yield return (parseResult, projectRelativeFileName);
        }

        foreach (var subDirectory in fileSystem.Directory.EnumerateDirectories(directory))
        {
            await foreach (var parseResult in GetReefModules(projectDir, subDirectory))
            {
                yield return parseResult;
            }
        }
    }

    private static ModuleId GetModuleIdFromProjectRelativeFileName(string fileName)
    {
        var segments = fileName.Split(Path.DirectorySeparatorChar);
        Debug.Assert(segments.Length > 0);

        var withoutExtension = Path.GetFileNameWithoutExtension(segments[^1]);

        // If the fileName matches the directory it's in, then the module name is that directory name
        if (segments.Length >= 2 && segments[^2] == withoutExtension)
        {
            segments = [.. segments[..^1]];
        }

        return new ModuleId(string.Join(":::", [.. segments[..^1], withoutExtension]));
    }

    private static string GetProjectRelativeFileName(string projectDir, string fileName)
    {
        Debug.Assert(fileName.StartsWith(projectDir));
        Debug.Assert(fileName.Length > projectDir.Length);
        var result = fileName[projectDir.Length..];

        if (result.StartsWith('\\'))
        {
            return result[1..];
        }
        return result;
    }

    public sealed record TypeCheckResult(LangModule Module, IReadOnlyList<ParserError> ParserErrors, IReadOnlyList<TypeCheckerError> TypeCheckerErrors);
}
