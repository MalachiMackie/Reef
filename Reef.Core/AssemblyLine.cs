using System.Diagnostics;
using System.Text;
using Reef.IL;

namespace Reef.Core;

public class AssemblyLine(ReefModule reefModule)
{
    private readonly ReefModule _reefModule = reefModule;
    
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

        var labels = method.Instructions.Labels.ToLookup(x => x.ReferencesInstructionIndex, x => x.Name);
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
    private const uint _shadowSpaceBytes = 32;

    /*
       Flags
          0000000000 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
                     │ │ │ │ │ │   │ │ │ │ │ │ │ │ │   │   │   │
       x - Id Flag  -┘ │ │ │ │ │   │ │ │ │ │ │ │ │ │   │   │   │
       x - VIP Flag   -┘ │ │ │ │   │ │ │ │ │ │ │ │ │   │   │   │
       x - VIF          -┘ │ │ │   │ │ │ │ │ │ │ │ │   │   │   │
       x - AC             -┘ │ │   │ │ │ │ │ │ │ │ │   │   │   │
       x - VM               -┘ │   │ │ │ │ │ │ │ │ │   │   │   │
       x - RF                 -┘   │ │ │ │ │ │ │ │ │   │   │   │
       x - NT                     -┘ │ │ │ │ │ │ │ │   │   │   │
       x - IOPL                     -┴-┘ │ │ │ │ │ │   │   │   │
       s - OF                           -┘ │ │ │ │ │   │   │   │
       c - DF                             -┘ │ │ │ │   │   │   │
       x - IF                               -┘ │ │ │   │   │   │
       x - TF                                 -┘ │ │   │   │   │
       s - SF                                   -┘ │   │   │   │
       s - ZF                                     -┘   │   │   │
       s - AF                                         -┘   │   │
       s - PF                                             -┘   │
       s - CF                                                 -┘
      
       Legend:
       s - Status flag
       c - control flag
       x - system flag

       Status Flags:
       CF - Carry Flag - Set if an arithmetic operation generates a carry or a borrow out of the most-
            significant bit of the result; cleared otherwise. This flag indicates an overflow condition for
            unsigned-integer arithmetic. It is also used in multiple-precision arithmetic
       PF - Parity Flag - Set if the least-significant byte of the result contains an even number of 1 bits;
            cleared otherwise
       AF - Auxiliary Carry Flag - Set if an arithmetic operation generates a carry or a borrow out of bit
            3 of the result; cleared otherwise. This flag is used in binary-coded decimal (BCD) arithmetic
       ZF - Zero Flag - Set if the result is zero; cleared otherwise
       SF - Sign Flag - Set equal to the most-significant bit of the result, which is the sign bit of a signed
            integer. (0 indicates a positive value and 1 indicates a negative value.)
       OF - Overflow Flag -  Set if the integer result is too large a positive number or too small a negative
            number (excluding the sign-bit) to fit in the destination operand; cleared otherwise. This flag
            indicates an overflow condition for signed-integer (two’s complement) arithmetic
    */


    private void ProcessInstruction(IInstruction instruction)
    {
        switch (instruction)
        {
            case BoolNot boolNot:
                _codeSegment.AppendLine($"; BOOL_NOT");
                throw new NotImplementedException();
            case Branch branch:
                {
                    _codeSegment.AppendLine($"; BRANCH({branch.BranchToLabelName})");
                    _codeSegment.AppendLine($"    jmp     {branch.BranchToLabelName}");
                    break;
                }
            case BranchIfFalse branchIfFalse:
                _codeSegment.AppendLine($"; BRANCH_IF_FALSE({branchIfFalse.BranchToLabelName})");
                throw new NotImplementedException();
            case BranchIfTrue branchIfTrue:
                _codeSegment.AppendLine($"; BRANCH_IF_TRUE({branchIfTrue.BranchToLabelName})");
                throw new NotImplementedException();
            case Call call:
            {
                _codeSegment.AppendLine($"; CALL({call.Arity})");
                if (call.Arity != 1)
                {
                    throw new NotImplementedException();
                }

                var functionDefinition = _functionStack.Pop();

                if (functionDefinition.TypeArguments.Count > 0)
                {
                    throw new NotImplementedException();
                }


                // move top of stack into rcx as first argument
                _codeSegment.AppendLine("    mov     rcx, [rsp]");

                _codeSegment.AppendLine("    mov     rax, rsp");
                _codeSegment.AppendLine("    and     rax, 0fffffffffffffff0h");
                _codeSegment.AppendLine("    mov     rsp, rax");
                _codeSegment.AppendLine($"    sub     rsp, {_shadowSpaceBytes}");
                
                // ensure we're byte aligned and give the callee 32 bytes of shadow space
                var bytesToOffset = 16 - _byteOffset;
                _byteOffset = 0;
                
                _codeSegment.AppendLine($"    call    {functionDefinition.Name}");
                
                // pop off the 32 bytes of shadow space as well as it's return value
                // TODO: we need to negate the stack alignment we did before calling the function
                _codeSegment.AppendLine($"    add     rsp, {_shadowSpaceBytes + 8}");
                _byteOffset = 8;
                break;
            }
            case CastBoolToInt castBoolToInt:
                {
                    _codeSegment.AppendLine($"; CAST_BOOL_TO_INT");
                    // noop
                    break;
                }
            case CompareIntEqual compareIntEqual:
                {
                    _codeSegment.AppendLine($"; COMPARE_INT_EQUAL");

                    _codeSegment.AppendLine("    pop    rax");
                    _codeSegment.AppendLine("    cmp    rax, [rsp]");
                    _codeSegment.AppendLine("    pop    rax");
                    _codeSegment.AppendLine("    pushf");
                    _codeSegment.AppendLine("    pop    rax");
                    _codeSegment.AppendLine("    and    rax, 1000000b"); // zero flag
                    _codeSegment.AppendLine("    shr    rax, 6");
                    _codeSegment.AppendLine("    push   rax");

                    break;
                }
            case CompareIntGreaterOrEqualTo compareIntGreaterOrEqualTo:
                _codeSegment.AppendLine($"; COMPARE_INT_GREATER_OR_EQUAL");
                throw new NotImplementedException();
            case CompareIntGreaterThan compareIntGreaterThan:
                {
                    _codeSegment.AppendLine($"; COMPARE_INT_GREATER");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    CMP     rax, [rsp]");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    PUSHF");
                    _byteOffset += 8;
                    _byteOffset %= 16;
                    _codeSegment.AppendLine($"; ByteOffest: {_byteOffset}");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    AND     rax, 10000000b"); // sign flag
                    _codeSegment.AppendLine("    SHR     rax, 7");
                    _codeSegment.AppendLine("    PUSH    rax");
                    _byteOffset += 8;
                    _byteOffset %= 16;
                    _codeSegment.AppendLine($"; ByteOffset: {_byteOffset}");
                    break;
                }
            case CompareIntLessOrEqualTo compareIntLessOrEqualTo:
                _codeSegment.AppendLine($"; COMPARE_INT_LESS_OR_EQUAL");
                throw new NotImplementedException();
            case CompareIntLessThan compareIntLessThan:
                {
                    _codeSegment.AppendLine($"; COMPARE_INT_LESS");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    CMP     [rsp], rax");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    PUSHF");
                    _byteOffset += 8;
                    _byteOffset %= 16;
                    _codeSegment.AppendLine($"; ByteOffest: {_byteOffset}");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    AND     rax, 10000000b"); // sign flag
                    _codeSegment.AppendLine("    SHR     rax, 7");
                    _codeSegment.AppendLine("    PUSH    rax");
                    _byteOffset += 8;
                    _byteOffset %= 16;
                    _codeSegment.AppendLine($"; ByteOffset: {_byteOffset}");
                    break;
                }
            case CopyStack copyStack:
                _codeSegment.AppendLine($"; COPY_STACK");
                throw new NotImplementedException();
            case CreateObject createObject:
                _codeSegment.AppendLine($"; CREATE_OBJECT({createObject.ReefType.Name}:<{string.Join(",", createObject.ReefType.TypeArguments)}>)");
                throw new NotImplementedException();
            case Drop drop:
                _codeSegment.AppendLine($"; DROP");
                throw new NotImplementedException();
            case IntDivide intDivide:
                _codeSegment.AppendLine($"; INT_DIVIDE");
                throw new NotImplementedException();
            case IntMinus intMinus:
                _codeSegment.AppendLine($"; INT_MINUS");
                throw new NotImplementedException();
            case IntMultiply intMultiply:
                _codeSegment.AppendLine($"; INT_MULTIPLY");
                throw new NotImplementedException();
            case IntPlus intPlus:
                {
                    _codeSegment.AppendLine($"; INT_PLUS");

                    _codeSegment.AppendLine($"    POP     rax");
                    _codeSegment.AppendLine("    ADD    [rsp], rax");
                    break;
                }
            case LoadArgument loadArgument:
                _codeSegment.AppendLine($"; LOAD_ARGUMENT({loadArgument.ArgumentIndex})");
                throw new NotImplementedException();
            case LoadBoolConstant loadBoolConstant:
                _codeSegment.AppendLine($"; LOAD_BOOL_CONSTANT({loadBoolConstant.Value})");
                throw new NotImplementedException();
            case LoadField loadField:
                _codeSegment.AppendLine($"; LOAD_FIELD({loadField.VariantIndex}:{loadField.FieldName})");
                throw new NotImplementedException();
            case LoadFunction loadFunction:
            {
                _codeSegment.AppendLine($"; LOAD_FUNCTION({loadFunction.FunctionDefinitionReference.Name})");
                _functionStack.Push(loadFunction.FunctionDefinitionReference);
                break;
            }
            case LoadIntConstant loadIntConstant:
                {
                _codeSegment.AppendLine($"; LOAD_INT_CONSTANT({loadIntConstant.Value})");
                    if (loadIntConstant.Value < 0) throw new NotImplementedException();
                    
                    var binaryFormatted = loadIntConstant.Value.ToString("x");
                    _codeSegment.AppendLine($"    push    {binaryFormatted}");
                    _byteOffset += 8;
                    _byteOffset %= 16;
                    _codeSegment.AppendLine($"; ByteOffset: {_byteOffset}");
                    break;
                }
            case LoadLocal loadLocal:
            {
                _codeSegment.AppendLine($"; LOAD_LOCAL({loadLocal.LocalName})");
                var localIndex = _locals.Index().First(x => x.Item.DisplayName == loadLocal.LocalName).Index;
                _codeSegment.AppendLine($"    push     [rbp-{_shadowSpaceBytes + localIndex*8}]");
                _byteOffset += 8;
                _byteOffset %= 16;
                    _codeSegment.AppendLine($"; ByteOffset: {_byteOffset}");
                break;
            }
            case LoadStaticField loadStaticField:
                _codeSegment.AppendLine($"; LOAD_STATIC_FIELD({loadStaticField})");
                throw new NotImplementedException();
            case LoadStringConstant loadStringConstant:
            {
                _codeSegment.AppendLine($"; LOAD_STRING_CONSTANT(\"{loadStringConstant.Value}\")");
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
                    _codeSegment.AppendLine($"; ByteOffset: {_byteOffset}");
                break;
            }
            case LoadType loadType:
                _codeSegment.AppendLine($"; LOAD_TYPE({loadType.ReefType})");
                throw new NotImplementedException();
            case LoadUnitConstant loadUnitConstant:
                _codeSegment.AppendLine($"; LOAD_UNIT_CONSTANT");
                // noop
                break;
            case Return:
            {
                _codeSegment.AppendLine($"; RETURN");
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
                _codeSegment.AppendLine($"; STORE_FIELD({storeField.VariantIndex}:{storeField.FieldName})");
                throw new NotImplementedException();
            case StoreLocal storeLocal:
            {
                _codeSegment.AppendLine($"; STORE_LOCAL({storeLocal.LocalName})");
                var localIndex = _locals.Index().First(x => x.Item.DisplayName == storeLocal.LocalName).Index;
                // pop the value on the stack into rax
                _codeSegment.AppendLine("    pop     rax");
                // we want to decrement the byte offset by 8, but we may increment by 8 so we don't overflow
                if (_byteOffset < 8)
                    _byteOffset += 8;
                else
                    _byteOffset -= 8;
                _codeSegment.AppendLine($"; ByteOffset: {_byteOffset}");
                
                // move rax into the local's dedicated stack space
                _codeSegment.AppendLine($"    mov     [rbp-{_shadowSpaceBytes + localIndex*8}], rax");
                break;
            }
            case StoreStaticField storeStaticField:
                _codeSegment.AppendLine($"; STORE_STATIC_FIELD({storeStaticField.StaticFieldName})");
                throw new NotImplementedException();
            case SwitchInt switchInt:
                {
                _codeSegment.AppendLine($"; SWITCH_INT");
                    foreach (var branch in switchInt.BranchLabels)
                    {
                        _codeSegment.AppendLine("    mov     rax, [rsp]");
                        _codeSegment.AppendLine($"    cmp     rax, {branch.Key:x}");
                        _codeSegment.AppendLine($"    je      {branch.Value}");
                    }
                    _codeSegment.AppendLine($"    jmp     {switchInt.Otherwise}");
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(instruction));
        }
    }
}