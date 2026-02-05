using System.IO.Abstractions;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public class ReefCompiler(IFileSystem fileSystem)
{
    public async Task<Dictionary<string, TypeCheckResult>> TypeCheck()
    {
        var parseErrors = new Dictionary<string, IReadOnlyList<ParserError>>();

        var currentDirectory = fileSystem.Directory.GetCurrentDirectory();

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

        return parsedModules.Select(x => x.fileName)
            .ToDictionary(x => x, x => new TypeCheckResult(parseErrors.GetValueOrDefault(x, []), typeCheckErrors.GetValueOrDefault(x, [])));
    }

    private async IAsyncEnumerable<(Parser.ParseResult, string fileName)> GetReefModules(string projectDir, string directory)
    {
        var contentTasks = fileSystem.Directory.EnumerateFiles(directory)
            .Where(x => Path.GetExtension(x) == ".rf")
            .Select(async fileName => (fileName, await fileSystem.File.ReadAllTextAsync(fileName)));

        var contents = await Task.WhenAll(contentTasks);

        foreach (var (fileName, fileContents) in contents)
        {
            var moduleId = GetModuleIdFromFileName(projectDir, fileName);
            var tokens = Tokenizer.Tokenize(fileContents);
            var parseResult = Parser.Parse(moduleId, tokens);

            yield return (parseResult, fileName);
        }

        foreach (var subDirectory in fileSystem.Directory.EnumerateDirectories(directory))
        {
            await foreach (var parseResult in GetReefModules(projectDir, subDirectory))
            {
                yield return parseResult;
            }
        }
    }

    private string GetModuleIdFromFileName(string projectDir, string fileName)
    {
        return fileName;
    }

    public sealed record TypeCheckResult(IReadOnlyList<ParserError> ParserErrors, IReadOnlyList<TypeCheckerError> TypeCheckerErrors);
}
