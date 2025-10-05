using System.Diagnostics;
using System.Text;
using Reef.IL;

namespace Reef.Core;

public class AssemblyLine(ReefModule reefModule)
{
    private readonly ReefModule _reefModule = reefModule;
    
    /*
     bits 64
default rel

segment .data
    msg db "Hello world!", 0xd, 0xa, 0

segment .text
global main
extern ExitProcess
extern _CRT_INIT

extern printf

main:
    push    rbp
    mov     rbp, rsp
    sub     rsp, 32
    
    call    _CRT_INIT

    lea     rcx, [msg]
    call    printf

    xor     rax, rax
    call    ExitProcess
     */

    private const string AsmHeader = """
                                     bits 64
                                     default rel
                                     
                                     """;

    private readonly StringBuilder _dataSegment = new("segment .data\n");

    private readonly StringBuilder _codeSegment = new("""
                                                      segment .text
                                                      global main
                                                      extern ExitProcess
                                                      extern printf
                                                      extern _CRT_INIT 
                                                      
                                                      """);

    /// <summary>
    /// Dictionary of string constants to their data segment labels
    /// </summary>
    private readonly Dictionary<string, string> _strings = new();
    
    public static string Process(ReefModule reefModule)
    {
        var assemblyLine = new AssemblyLine(reefModule);
        return assemblyLine.ProcessInner();
    }

    private string ProcessInner()
    {
        CreateMain();
        _codeSegment.AppendLine();
        
        foreach (var method in _reefModule.Methods)
        {
            ProcessMethod(method);
            _codeSegment.AppendLine();
        }

        return $"""
                {AsmHeader}
                {_dataSegment}
                {_codeSegment}
                """;
    }

    private void CreateMain()
    {
        _codeSegment.AppendLine("main:");
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");
        _codeSegment.AppendLine("    sub     rsp, 32");
        // ensure stack is 16-byte aligned
        _codeSegment.AppendLine("    and     rsp, 0xFFFFFFFFFFFFFFF0");
        _codeSegment.AppendLine("    call    _CRT_INIT");

        _codeSegment.AppendLine("    call    _Main");

        // zero out rax as return value
        _codeSegment.AppendLine("    xor     rax, rax");
        // move rax into rcx for exit process parameter
        _codeSegment.AppendLine("    mov     rcx, rax");
        _codeSegment.AppendLine("    call    ExitProcess");
    }

    private IReefTypeReference _returnType = null!;
    private IReadOnlyList<ReefMethod.Local> _locals = null!;
    private void ProcessMethod(ReefMethod method)
    {
        _returnType = method.ReturnType;
        _locals = method.Locals;
        if (method.TypeParameters.Count > 0)
        {
            throw new NotImplementedException();
        }

        _codeSegment.AppendLine($"{method.DisplayName}:");
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");

        var stackSpaceNeeded = _locals.Count * 8;
        // ensure stack space is 16 byte aligned
        stackSpaceNeeded += stackSpaceNeeded % 16;
        _codeSegment.AppendLine($"    sub     rsp, {32 + stackSpaceNeeded}");

        var labels = method.Instructions.Labels.ToLookup(x => x.ReferencesInstructionIndex);
        for (var i = 0; i < method.Instructions.Instructions.Count; i++)
        {
            foreach (var label in labels[(uint)i])
            {
                _codeSegment.AppendLine($"{label}:");
            }
            ProcessInstruction(method.Instructions.Instructions[i]);
        }
    }

    private readonly Stack<FunctionDefinitionReference> _functionStack = [];
    private uint _byteOffset;

    private void ProcessInstruction(IInstruction instruction)
    {
        switch (instruction)
        {
            case BoolNot boolNot:
                throw new NotImplementedException();
            case Branch branch:
                throw new NotImplementedException();
            case BranchIfFalse branchIfFalse:
                throw new NotImplementedException();
            case BranchIfTrue branchIfTrue:
                throw new NotImplementedException();
            case Call call:
            {
                if (call.Arity != 1)
                {
                    throw new NotImplementedException();
                }

                var functionDefinition = _functionStack.Pop();

                if (functionDefinition.TypeArguments.Count > 0)
                {
                    throw new NotImplementedException();
                }

                _codeSegment.AppendLine($"    mov     rcx, [rsp+8]");
                var bytesToOffset = 16 - _byteOffset;
                if (_byteOffset > 0)
                {
                    _codeSegment.AppendLine($"    sub     rsp, {bytesToOffset}");
                    _byteOffset = 0;
                    // _codeSegment.AppendLine($"    mov     rcx, [rsp + {bytesToOffset}]");
                }
                else
                {
                    // _codeSegment.AppendLine("    mov     rcx, [rsp]");
                }
                _codeSegment.AppendLine($"    call    {functionDefinition.Name}");
                break;
            }
            case CastBoolToInt castBoolToInt:
                throw new NotImplementedException();
            case CompareIntEqual compareIntEqual:
                throw new NotImplementedException();
            case CompareIntGreaterOrEqualTo compareIntGreaterOrEqualTo:
                throw new NotImplementedException();
            case CompareIntGreaterThan compareIntGreaterThan:
                throw new NotImplementedException();
            case CompareIntLessOrEqualTo compareIntLessOrEqualTo:
                throw new NotImplementedException();
            case CompareIntLessThan compareIntLessThan:
                throw new NotImplementedException();
            case CopyStack copyStack:
                throw new NotImplementedException();
            case CreateObject createObject:
                throw new NotImplementedException();
            case Drop drop:
                throw new NotImplementedException();
            case IntDivide intDivide:
                throw new NotImplementedException();
            case IntMinus intMinus:
                throw new NotImplementedException();
            case IntMultiply intMultiply:
                throw new NotImplementedException();
            case IntPlus intPlus:
                throw new NotImplementedException();
            case LoadArgument loadArgument:
                throw new NotImplementedException();
            case LoadBoolConstant loadBoolConstant:
                throw new NotImplementedException();
            case LoadField loadField:
                throw new NotImplementedException();
            case LoadFunction loadFunction:
            {
                _functionStack.Push(loadFunction.FunctionDefinitionReference);
                break;
            }
            case LoadIntConstant loadIntConstant:
                throw new NotImplementedException();
            case LoadLocal loadLocal:
            {
                var localIndex = _locals.Index().First(x => x.Item.DisplayName == loadLocal.LocalName).Index;
                _codeSegment.AppendLine($"    push     [rbp-{32 + 8 + localIndex*8}]");
                _byteOffset += 8;
                _byteOffset %= 16;
                break;
            }
            case LoadStaticField loadStaticField:
                throw new NotImplementedException();
            case LoadStringConstant loadStringConstant:
            {
                if (!_strings.TryGetValue(loadStringConstant.Value, out var stringName))
                {
                    stringName = $"_str_{_strings.Count}";
                    _strings[loadStringConstant.Value] = stringName;
                    // todo: no null terminated strings
                    _dataSegment.AppendLine(
                        $"    {stringName} db \"{loadStringConstant.Value}\", 0");
                }

                _codeSegment.AppendLine($"    lea     rax, [{stringName}]");
                _codeSegment.AppendLine("    push    rax");
                _byteOffset += 8;
                _byteOffset %= 16;
                break;
            }
            case LoadType loadType:
                throw new NotImplementedException();
            case LoadUnitConstant loadUnitConstant:
                // noop
                break;
            case Return:
            {
                if (_returnType is not ConcreteReefTypeReference { Name: "Unit" })
                {
                    throw new NotImplementedException();
                }

                // zero out return value for null return
                _codeSegment.AppendLine("    xor     rax, rax");
                _codeSegment.AppendLine("    leave");
                _codeSegment.AppendLine("    ret");
                break;
            }
            case StoreField storeField:
                throw new NotImplementedException();
            case StoreLocal storeLocal:
            {
                var localIndex = _locals.Index().First(x => x.Item.DisplayName == storeLocal.LocalName).Index;
                _codeSegment.AppendLine($"    mov     [rbp-{32 + 8 + localIndex*8}], rsp");
                break;
            }
            case StoreStaticField storeStaticField:
                throw new NotImplementedException();
            case SwitchInt switchInt:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(instruction));
        }
    }
}