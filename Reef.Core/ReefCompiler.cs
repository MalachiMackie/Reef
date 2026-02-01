using System.IO.Abstractions;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public class ReefCompiler(IFileSystem fileSystem)
{
    public async Task<TypeCheckResult> TypeCheck(string file)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
        var fileContents = await fileSystem.File.ReadAllTextAsync(file);
        var tokens = Tokenizer.Tokenize(fileContents);
        var parseResult = Parser.Parse(fileNameWithoutExtension, tokens);
        var typeCheckErrors = TypeChecker.TypeCheck(parseResult.ParsedProgram);
        return new TypeCheckResult(parseResult.Errors, typeCheckErrors);
    }

    public sealed record TypeCheckResult(IReadOnlyList<ParserError> ParserErrors, IReadOnlyList<TypeCheckerError> TypeCheckerErrors);
}