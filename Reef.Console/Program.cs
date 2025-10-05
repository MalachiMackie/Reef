using Reef.Core;
using Reef.Core.Abseil;
using Reef.Core.TypeChecking;

if (args is not [var fileName]
    || string.IsNullOrWhiteSpace(fileName)
    || Path.GetExtension(fileName) is not ".rf")
{
    Console.Error.WriteLine("Expected a single .rf file as an argument");
    return;
}

var contents = await File.ReadAllTextAsync(fileName);

var tokens = Tokenizer.Tokenize(contents);

var parsedProgram = Parser.Parse(tokens);
if (parsedProgram.Errors.Count > 0)
{
    foreach (var error in parsedProgram.Errors)
    {
        Console.Error.Write("Error: ");
        Console.Error.WriteLine(error);
    }

    return;
}

var typeCheckErrors = TypeChecker.TypeCheck(parsedProgram.ParsedProgram);
if (typeCheckErrors.Count > 0)
{
    foreach (var error in typeCheckErrors)
    {
        Console.Error.Write("Error: ");
        Console.Error.WriteLine(error.Message);
        Console.Error.WriteLine(contents[(int)error.Range.Start.Position.Start..(int)(error.Range.End.Position.Start + error.Range.End.Length)]);
    }

    return;
}

var loweredProgram = ProgramAbseil.Lower(parsedProgram.ParsedProgram);
var il = ILCompile.CompileToIL(loweredProgram);

var assembly = AssemblyLine.Process(il);
await File.WriteAllTextAsync("output.asm", assembly);

// nasm -F cv8 -f win64 -o output.obj output.asm
// link output.obj /subsystem:console /out:output.exe kernel32.lib legacy_stdio_definitions.lib msvcrt.lib /debug