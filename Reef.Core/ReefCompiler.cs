using System.Diagnostics;
using System.IO.Abstractions;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public class ReefCompiler(IFileSystem fileSystem, string? workingDirectory = null)
{
    public async Task<Dictionary<string, TypeCheckResult>> TypeCheck()
    {
        var parseErrors = new Dictionary<string, IReadOnlyList<ParserError>>();

        var currentDirectory = workingDirectory ?? fileSystem.Directory.GetCurrentDirectory();

        var parsedModules = await GetReefModules(currentDirectory, currentDirectory).ToArrayAsync();
        var modulesDictionary = new Dictionary<string, LangModule>();

        foreach (var (parseResult, fileName) in parsedModules)
        {
            modulesDictionary[fileName] = parseResult.ParsedModule;
            if (parseResult.Errors.Count > 0)
            {
                parseErrors[fileName] = parseResult.Errors;
            }
        }

        var typeCheckErrors = TypeChecker.TypeCheck(modulesDictionary);

        return parsedModules
            .ToDictionary(x => x.fileName, x => new TypeCheckResult(x.Item1.ParsedModule, parseErrors.GetValueOrDefault(x.fileName, []), typeCheckErrors.GetValueOrDefault(x.fileName, [])));
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

    private static string GetModuleIdFromProjectRelativeFileName(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);

        var segments = withoutExtension.Split(Path.DirectorySeparatorChar);

        Debug.Assert(segments.Length > 0);

        return string.Join(":::", segments);
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
