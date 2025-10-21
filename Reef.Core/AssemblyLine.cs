using System.Text;
using Reef.Core.IL;

namespace Reef.Core;

public class AssemblyLine(IReadOnlyList<ReefILModule> modules, HashSet<DefId> usefulMethodIds)
{
    private readonly IReadOnlyList<ReefILModule> _modules = modules;
    private readonly HashSet<DefId> _usefulMethodIds = usefulMethodIds;
    
    private const string AsmHeader = """
                                     bits 64
                                     default rel
                                     
                                     """;

    private readonly StringBuilder _dataSegment = new("segment .data\n");

    private readonly StringBuilder _codeSegment = new("""
                                                      segment .text
                                                      global main
                                                      extern ExitProcess
                                                      extern _CRT_INIT 
                                                      
                                                      """);

    /// <summary>
    /// Dictionary of string constants to their data segment labels
    /// </summary>
    private readonly Dictionary<string, string> _strings = [];
    
    public static string Process(IReadOnlyList<ReefILModule> modules, HashSet<DefId> usefulMethodIds)
    {
        var assemblyLine = new AssemblyLine(modules, usefulMethodIds);
        return assemblyLine.ProcessInner();
    }

    private string ProcessInner()
    {
        var mainModule = _modules.Where(x => x.MainMethod is not null).ToArray();

        if (mainModule is not [{ MainMethod: { } mainMethod }])
        {
            throw new InvalidOperationException("Expected a single module with a main method");
        }

        foreach (var externMethod in _modules.SelectMany(x => x.Methods).Where(x => x.Extern && _usefulMethodIds.Contains(x.Id)))
        {
            _codeSegment.AppendLine($"extern {externMethod.DisplayName}");
        }

        CreateMain(mainMethod);

        foreach (var module in _modules)
        {
            _codeSegment.AppendLine();

            foreach (var method in module.Methods.Where(x => !x.Extern && _usefulMethodIds.Contains(x.Id)))
            {
                ProcessMethod(method);
                _codeSegment.AppendLine();
            }
        }

        return $"""
                {AsmHeader}
                {_dataSegment}
                {_codeSegment}
                """;
    }

    private ReefMethod? GetMethod(DefId defId)
    {
        return _modules.SelectMany(x => x.Methods)
            .FirstOrDefault(x => x.Id == defId);
    }

    private ReefILTypeDefinition? GetDataType(DefId defId)
    {
        return _modules.SelectMany(x => x.Types)
            .FirstOrDefault(x => x.Id == defId);
    }

    private void CreateMain(ReefMethod mainMethod)
    {
        _codeSegment.AppendLine("main:");
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");
        // give CRT_INIT it's shadow space
        _codeSegment.AppendLine($"    sub     rsp, {_shadowSpaceBytes}");

        _codeSegment.AppendLine("    call    _CRT_INIT");
        // put rsp back
        _codeSegment.AppendLine($"    add     rsp, {_shadowSpaceBytes}");

        // give main it's shadow space
        _codeSegment.AppendLine($"    sub     rsp, {_shadowSpaceBytes}");
        _codeSegment.AppendLine($"    call    {mainMethod.Id.FullName}");

        _codeSegment.AppendLine($"    add     rsp, {_shadowSpaceBytes}");

        // zero out rax as return value
        _codeSegment.AppendLine("    xor     rax, rax");
        // move rax into rcx for exit process parameter
        _codeSegment.AppendLine("    mov     rcx, rax");
        _codeSegment.AppendLine("    call    ExitProcess");
    }

    private IReefTypeReference _returnType = null!;
    private IReadOnlyList<ReefMethod.Local> _locals = null!;
    private IReadOnlyList<IReefTypeReference> _parameters = null!;

    private void ProcessMethod(ReefMethod method)
    {
        _returnType = method.ReturnType;
        _locals = method.Locals;
        if (method.TypeParameters.Count > 0)
        {
            throw new NotImplementedException();
        }

        _codeSegment.AppendLine($"{method.Id.FullName}:");
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");

        _parameters = method.Parameters;

        var stackSpaceNeeded = (_locals.Count + _parameters.Count) * 8;
        // ensure stack space is 16 byte aligned
        stackSpaceNeeded += stackSpaceNeeded % 16;
        _codeSegment.AppendLine($"; Allocate stack space for local variables");
        _codeSegment.AppendLine($"    sub     rsp, {stackSpaceNeeded}");

        // todo: support non integer and non 8 byte parameters
        for (var i = method.Parameters.Count - 1; i >= 0; i--)
        {
            var sourceRegister = i switch
            {
                0 => "rcx",
                1 => "rdx",
                2 => "r8",
                3 => "r9",
                _ => null
            };

            if (sourceRegister is null)
            {
                throw new NotImplementedException();
            }

            _codeSegment.AppendLine($"    mov    [rbp-{(i + 1)*8}], {sourceRegister}");
        }

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
                {
                    _codeSegment.AppendLine($"; BOOL_NOT");

                    _codeSegment.AppendLine("    xor    [rsp], 1h");
                    break;
                }
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
                    // loop through backwards because the last argument is on the top of the stack 
                    for (var i = ((int)call.Arity) - 1; i >= 0; i--)
                    {
                        // todo: this does not account for non integers or values larger than 8 bytes
                        var destinationRegister = i switch
                        {
                            0 => "rcx",
                            1 => "rdx",
                            2 => "r8",
                            3 => "r9",
                            _ => null
                        };

                        if (destinationRegister is not null)
                        {
                            _codeSegment.AppendLine($"    pop     {destinationRegister}");
                        }
                        else
                        {
                            throw new NotImplementedException("Functions with more than 4 arguments");
                        }

                    }

                    var functionDefinition = _functionStack.Pop();

                    if (functionDefinition.TypeArguments.Count > 0)
                    {
                        throw new NotImplementedException();
                    }

                    // calculate how many bytes we need to offset to be byte aligned by 16
                    var stackSize = call.TypeStack.Select(x => x switch
                    {
                        ConcreteReefTypeReference concrete => GetDataType(concrete.DefinitionId).NotNull().StackSize,
                        _ => throw new NotImplementedException()
                    }).DefaultIfEmpty().Aggregate((a, b) => a + b);

                    var bytesToOffset = stackSize % 16;

                    _codeSegment.AppendLine($"    sub     rsp, {(_shadowSpaceBytes + bytesToOffset).ToString("X")}h");
                    
                    _codeSegment.AppendLine($"    call    {functionDefinition.DefinitionId.FullName}");
                    
                    // move rsp back to where it was before we called the function
                    _codeSegment.AppendLine($"    add     rsp, {(_shadowSpaceBytes + bytesToOffset).ToString("X")}h");

                    var method = GetMethod(functionDefinition.DefinitionId) ?? throw new InvalidOperationException($"No method found with {functionDefinition.DefinitionId}");

                    if (call.ValueUseful && method.ReturnType is not ConcreteReefTypeReference { Name: "Unit" })
                    {
                        _codeSegment.AppendLine("    push    rax");
                    }

                    break;
                }
            case CastBoolToInt castBoolToInt:
                {
                    _codeSegment.AppendLine($"; CAST_BOOL_TO_INT");
                    // noop
                    break;
                }
            case CompareInt64Equal compareIntEqual:
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
            case CompareInt32Equal:
            case CompareInt16Equal:
            case CompareInt8Equal:
            case CompareUInt64Equal:
            case CompareUInt32Equal:
            case CompareUInt16Equal:
            case CompareUInt8Equal:
                throw new NotImplementedException();
            case CompareInt64NotEqual compareIntNotEqual:
                {
                    _codeSegment.AppendLine($"; COMPARE_INT_NOT_EQUAL");

                    _codeSegment.AppendLine("    pop    rax");
                    _codeSegment.AppendLine("    cmp    rax, [rsp]");
                    _codeSegment.AppendLine("    pop    rax");
                    _codeSegment.AppendLine("    pushf");
                    _codeSegment.AppendLine("    pop    rax");
                    _codeSegment.AppendLine("    and    rax, 1000000b"); // zero flag
                    _codeSegment.AppendLine("    shr    rax, 6");
                    _codeSegment.AppendLine("    xor    rax, 1b");
                    _codeSegment.AppendLine("    push   rax");
                    break;
                }
            case CompareInt32NotEqual:
            case CompareInt16NotEqual:
            case CompareInt8NotEqual:
            case CompareUInt64NotEqual:
            case CompareUInt32NotEqual:
            case CompareUInt16NotEqual:
            case CompareUInt8NotEqual:
                throw new NotImplementedException();
            case CompareInt64GreaterOrEqualTo compareIntGreaterOrEqualTo:
            case CompareInt32GreaterOrEqualTo:
            case CompareInt16GreaterOrEqualTo:
            case CompareInt8GreaterOrEqualTo:
            case CompareUInt64GreaterOrEqualTo:
            case CompareUInt32GreaterOrEqualTo:
            case CompareUInt16GreaterOrEqualTo:
            case CompareUInt8GreaterOrEqualTo:
                _codeSegment.AppendLine($"; COMPARE_INT_GREATER_OR_EQUAL");
                throw new NotImplementedException();
            case CompareInt64GreaterThan compareIntGreaterThan:
                {
                    _codeSegment.AppendLine($"; COMPARE_INT_GREATER");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    CMP     rax, [rsp]");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    PUSHF");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    AND     rax, 10000000b"); // sign flag
                    _codeSegment.AppendLine("    SHR     rax, 7");
                    _codeSegment.AppendLine("    PUSH    rax");
                    break;
                }
            case CompareInt32GreaterThan:
            case CompareInt16GreaterThan:
            case CompareInt8GreaterThan:
            case CompareUInt64GreaterThan:
            case CompareUInt32GreaterThan:
            case CompareUInt16GreaterThan:
            case CompareUInt8GreaterThan:
                throw new NotImplementedException();
            case CompareInt64LessOrEqualTo compareIntLessOrEqualTo:
                _codeSegment.AppendLine($"; COMPARE_INT_LESS_OR_EQUAL");
                throw new NotImplementedException();
            case CompareInt32LessOrEqualTo:
            case CompareInt16LessOrEqualTo:
            case CompareInt8LessOrEqualTo:
            case CompareUInt64LessOrEqualTo:
            case CompareUInt32LessOrEqualTo:
            case CompareUInt16LessOrEqualTo:
            case CompareUInt8LessOrEqualTo:
                throw new NotImplementedException();
            case CompareInt64LessThan compareIntLessThan:
                {
                    _codeSegment.AppendLine($"; COMPARE_INT_LESS");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    CMP     [rsp], rax");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    PUSHF");
                    _codeSegment.AppendLine("    POP     rax");
                    _codeSegment.AppendLine("    AND     rax, 10000000b"); // sign flag
                    _codeSegment.AppendLine("    SHR     rax, 7");
                    _codeSegment.AppendLine("    PUSH    rax");
                    break;
                }
            case CompareInt32LessThan:
            case CompareInt16LessThan:
            case CompareInt8LessThan:
            case CompareUInt64LessThan:
            case CompareUInt32LessThan:
            case CompareUInt16LessThan:
            case CompareUInt8LessThan:
                throw new NotImplementedException();
            case CopyStack copyStack:
                _codeSegment.AppendLine($"; COPY_STACK");
                throw new NotImplementedException();
            case CreateObject createObject:
                _codeSegment.AppendLine($"; CREATE_OBJECT({createObject.ReefType.Name}:<{string.Join(",", createObject.ReefType.TypeArguments)}>)");
                throw new NotImplementedException();
            case Drop drop:
                _codeSegment.AppendLine($"; DROP");
                throw new NotImplementedException();
            case Int64Divide intDivide:
                {
                    _codeSegment.AppendLine($"; INT_DIVIDE");

                    _codeSegment.AppendLine("    pop     rcx");
                    _codeSegment.AppendLine("    pop     rax");

                    // extend rax to double quad word for divide operation
                    _codeSegment.AppendLine("    cqo");
                    _codeSegment.AppendLine("    idiv    rcx");
                    _codeSegment.AppendLine("    mov     [rsp], rax");

                    // todo: panic/throw when dividing by zero

                    break;
                }
            case Int32Divide:
            case Int16Divide:
            case Int8Divide:
            case UInt64Divide:
            case UInt32Divide:
            case UInt16Divide:
            case UInt8Divide:
                throw new NotImplementedException();
            case Int64Minus intMinus:
                {
                    _codeSegment.AppendLine($"; INT_MINUS");

                    _codeSegment.AppendLine("    POP    rax");
                    _codeSegment.AppendLine("    SUB    [rsp], rax");
                    break;
                }
            case Int32Minus:
            case Int16Minus:
            case Int8Minus:
            case UInt64Minus:
            case UInt32Minus:
            case UInt16Minus:
            case UInt8Minus:
                throw new NotImplementedException();
            case Int64Multiply intMultiply:
                {
                    _codeSegment.AppendLine($"; INT_MULTIPLY");

                    _codeSegment.AppendLine("    pop     rax");
                    _codeSegment.AppendLine("    pop     rbx");
                    // imul requires both arguments to be in registers (I think)
                    _codeSegment.AppendLine("    imul    rax, rbx");
                    _codeSegment.AppendLine("    mov     [rsp], rax");
                    // todo: panic/throw on overflow
                    break;
                }
            case Int32Multiply:
            case Int16Multiply:
            case Int8Multiply:
            case UInt64Multiply:
            case UInt32Multiply:
            case UInt16Multiply:
            case UInt8Multiply:
                throw new NotImplementedException();
            case Int64Plus intPlus:
                {
                    _codeSegment.AppendLine($"; INT_PLUS");

                    _codeSegment.AppendLine($"    POP     rax");
                    _codeSegment.AppendLine("    ADD    [rsp], rax");
                    break;
                }
            case Int32Plus:
            case Int16Plus:
            case Int8Plus:
            case UInt64Plus:
            case UInt32Plus:
            case UInt16Plus:
            case UInt8Plus:
                throw new NotImplementedException();
            case LoadArgument loadArgument:
                {
                    _codeSegment.AppendLine($"; LOAD_ARGUMENT({loadArgument.ArgumentIndex})");

                    _codeSegment.AppendLine($"    push    [rbp-{(loadArgument.ArgumentIndex + 1)*8}]");

                    break;
                }
            case LoadBoolConstant loadBoolConstant:
                {
                    _codeSegment.AppendLine($"; LOAD_BOOL_CONSTANT({loadBoolConstant.Value})");

                    _codeSegment.AppendLine($"    PUSH    {(loadBoolConstant.Value ? 1 : 0)}h");

                    break;
                }
            case LoadField loadField:
                _codeSegment.AppendLine($"; LOAD_FIELD({loadField.VariantIndex}:{loadField.FieldName})");
                throw new NotImplementedException();
            case LoadFunction loadFunction:
            {
                _codeSegment.AppendLine($"; LOAD_FUNCTION({loadFunction.FunctionDefinitionReference.Name})");
                _functionStack.Push(loadFunction.FunctionDefinitionReference);
                break;
            }
            case LoadInt64Constant loadIntConstant:
                {
                _codeSegment.AppendLine($"; LOAD_INT_CONSTANT({loadIntConstant.Value})");
                    if (loadIntConstant.Value < 0) throw new NotImplementedException();
                    
                    var binaryFormatted = loadIntConstant.Value.ToString("x");
                    _codeSegment.AppendLine($"    push    {binaryFormatted}");
                    break;
                }
            case LoadInt32Constant:
            case LoadInt16Constant:
            case LoadInt8Constant:
            case LoadUInt64Constant:
            case LoadUInt32Constant:
            case LoadUInt16Constant:
            case LoadUInt8Constant:
                throw new NotImplementedException();
            case LoadLocal loadLocal:
            {
                _codeSegment.AppendLine($"; LOAD_LOCAL({loadLocal.LocalName})");
                var localIndex = _locals.Index().First(x => x.Item.DisplayName == loadLocal.LocalName).Index;
                _codeSegment.AppendLine($"    push     [rbp-{(localIndex + _parameters.Count + 1)*8}]");
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
                    if (_returnType is ConcreteReefTypeReference { Name: "Unit" })
                    {
                        // zero out return value for null return
                        _codeSegment.AppendLine("    xor     rax, rax");
                    }
                    else
                    {
                        _codeSegment.AppendLine("    pop     rax");
                    }

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
                
                // move rax into the local's dedicated stack space
                _codeSegment.AppendLine($"    mov     [rbp-{(localIndex + _parameters.Count + 1)*8}], rax");
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
                        _codeSegment.AppendLine("    pop     rax");
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