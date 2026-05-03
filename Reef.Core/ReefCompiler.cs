using System.Diagnostics;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public class ReefCompiler(
    IFileSystem fileSystem,
    ModuleId mainModuleId,
    ILogger logger,
    string? workingDirectory = null,
    string? standardLibraryDirectory = null)
{
    public async Task<
        (
            Dictionary<ModuleId, TypeCheckResult> ModuleTypeCheckResults,
            Dictionary<ModuleId, string> ModuleToFileName,
            ModuleId MainModuleId,
            IReadOnlyList<LangModule> ImportedModules
        )> TypeCheck(bool throwOnError = false)
    {
        var currentDirectory = workingDirectory ?? fileSystem.Directory.GetCurrentDirectory();

        var parsedModules = await GetReefModules(currentDirectory, currentDirectory).ToArrayAsync();
        var modules = new List<LangModule>(parsedModules.Length);
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

        var parsedStandardLibraryModules = GetStandardLibraryModules();

        var standardLibraryModules = new List<LangModule>();
        await foreach (var module in parsedStandardLibraryModules)
        {
            if (module.Errors.Count > 0)
            {
                logger.LogError("Parser error in module {ModuleId}", module.ParsedModule.ModuleId);
                foreach (var error in module.Errors)
                {
                    logger.LogError("{LineNumber}: {ErrorType}, {ExpectedTokens} - {ReceivedToken}", error.ReceivedToken?.SourceSpan.Position.LineNumber, error.Type, string.Join(", ", error.ExpectedTokenTypes ?? []), error.ReceivedToken);
                }

                throw new InvalidOperationException($"Parser error in module {module.ParsedModule.ModuleId}");
            }
            standardLibraryModules.Add(module.ParsedModule);
        }

        var stdLibHadTypeCheckErrors = false;
        foreach (var (moduleId, errors) in TypeChecker.TypeCheck(standardLibraryModules, []))
        {
            if (errors.Count == 0) continue;
            logger.LogError("TypeChecker errors in {ModuleId}", moduleId);
            foreach (var error in errors)
            {
                logger.LogError("{Position} - {Error}", error.Range.Start.Position, error.Message);
            }
            stdLibHadTypeCheckErrors = true;
        }
        if (stdLibHadTypeCheckErrors)
        {
            throw new InvalidOperationException("Standard Library had type checking errors D:");
        }

        var typeCheckErrors = TypeChecker.TypeCheck(modules, standardLibraryModules, throwOnError);

        return (
            parsedModules
                .ToDictionary(x => x.Item1.ParsedModule.ModuleId, x => new TypeCheckResult(
                    x.Item1.ParsedModule,
                    parseErrors.GetValueOrDefault(x.Item1.ParsedModule.ModuleId, []),
                    typeCheckErrors.GetValueOrDefault(x.Item1.ParsedModule.ModuleId, []))),
            moduleIdToFileName,
            mainModuleId,
            standardLibraryModules
        );
    }

    private IEnumerable<(string fileName, Func<StreamReader> fileStream)> GetStandardLibraryFileStreams()
    {
        if (standardLibraryDirectory is null)
        {
            var assembly = typeof(ReefCompiler).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            return resourceNames.Where(x => x.StartsWith("reef-std\\") && Path.GetExtension(x) == ".rf")
                .Select(x => (x, (Func<StreamReader>)(() => new StreamReader(assembly.GetManifestResourceStream(x).NotNull()))));
        }

        return EnumerateReefFiles(standardLibraryDirectory)
            .Select(x => (x, (Func<StreamReader>)(() => fileSystem.File.OpenText(x))));
    }

    private async IAsyncEnumerable<Parser.ParseResult> GetStandardLibraryModules()
    {
        var contentTasks = GetStandardLibraryFileStreams()
            .Select(async x =>
            {
                using var stream = x.fileStream();
                return (x.fileName, await stream.ReadToEndAsync());
            });

        await foreach (var task in Task.WhenEach(contentTasks))
        {
            var (fileName, contents) = await task;
            var projectRelativeFileName = GetProjectRelativeFileName(standardLibraryDirectory ?? "reef-std", fileName);

            var moduleId = GetModuleIdFromProjectRelativeFileName(projectRelativeFileName, new ModuleId("Reef:::Core"));

            var tokens = Tokenizer.Tokenize(contents);
            var parseResult = Parser.Parse(moduleId, tokens);

            yield return parseResult;
        }
    }

    private IEnumerable<string> EnumerateReefFiles(string directory)
    {
        foreach (var fileName in fileSystem.Directory.EnumerateFiles(directory)
            .Where(x => Path.GetExtension(x) == ".rf"))
        {
            yield return fileName;
        }

        foreach (var subDirectory in fileSystem.Directory.EnumerateDirectories(directory))
        {
            foreach (var fileName in EnumerateReefFiles(subDirectory))
            {
                yield return fileName;
            }
        }
    }

    private async IAsyncEnumerable<(Parser.ParseResult, string fileName)> GetReefModules(string projectDir, string directory)
    {
        var contentTasks = EnumerateReefFiles(directory)
            .Select(async fileName => (fileName, await fileSystem.File.ReadAllTextAsync(fileName)));

        var contents = await Task.WhenAll(contentTasks);

        foreach (var (fileName, fileContents) in contents)
        {
            var projectRelativeFileName = GetProjectRelativeFileName(projectDir, fileName);
            var moduleId = GetModuleIdFromProjectRelativeFileName(projectRelativeFileName, mainModuleId);
            var tokens = Tokenizer.Tokenize(fileContents);
            var parseResult = Parser.Parse(moduleId, tokens);

            yield return (parseResult, projectRelativeFileName);
        }
    }

    private static ModuleId GetModuleIdFromProjectRelativeFileName(string fileName, ModuleId mainModuleId)
    {
        var segments = fileName.Split(Path.DirectorySeparatorChar);
        Debug.Assert(segments.Length > 0);

        var withoutExtension = Path.GetFileNameWithoutExtension(segments[^1]);

        if (withoutExtension == "main" && segments.Length == 1)
        {
            return mainModuleId;
        }

        // If the fileName matches the directory it's in, then the module name is that directory name
        if (segments.Length >= 2 && segments[^2] == withoutExtension)
        {
            segments = [.. segments[..^1]];
        }

        return new ModuleId($"{mainModuleId.Value}:::{string.Join(":::", [.. segments[..^1], withoutExtension])}");
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
