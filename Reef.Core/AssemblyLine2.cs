using System.Diagnostics;
using System.Text;
using Reef.Core.LoweredExpressions.New;

namespace Reef.Core;

public class AssemblyLine2(IReadOnlyList<NewLoweredModule> modules, HashSet<DefId> usefulMethodIds)
{
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

    private readonly Queue<(NewLoweredMethod Method, IReadOnlyList<NewLoweredConcreteTypeReference> TypeArguments)>
        _methodProcessingQueue = [];

    private NewLoweredMethod? _currentMethod;
    private IReadOnlyList<NewLoweredConcreteTypeReference> _currentTypeArguments = [];

    private readonly HashSet<string> _queuedMethodLabels = [];

    private void TryEnqueueMethodForProcessing(NewLoweredMethod method, IReadOnlyList<NewLoweredConcreteTypeReference> typeArguments)
    {
        var label = GetMethodLabel(method, typeArguments);
        if (_queuedMethodLabels.Add(label))
        {
            _methodProcessingQueue.Enqueue((method, typeArguments));
        }
    }

    public static string Process(IReadOnlyList<NewLoweredModule> modules, HashSet<DefId> usefulMethodIds)
    {
        var assemblyLine = new AssemblyLine2(modules, usefulMethodIds);
        return assemblyLine.ProcessInner();
    }

    private string ProcessInner()
    {
        var mainModule = modules.Where(x => x.Methods.Any(y => y.Name == "_Main")).ToArray();

        if (mainModule.Length != 1)
        {
            throw new InvalidOperationException("Expected a single module with a main method");
        }

        var mainMethod = mainModule[0].Methods.OfType<NewLoweredMethod>().Single(x => x.Name == "_Main");

        foreach (var externMethod in modules.SelectMany(x => x.Methods)
                     .OfType<NewLoweredExternMethod>()
                     .Where(x => usefulMethodIds.Contains(x.Id)))
        {
            _codeSegment.AppendLine($"extern {externMethod.Name}");
        }

        CreateMain(mainMethod);

        // enqueue all non-generic methods. Generic methods get enqueued lazily based on what they're type arguments they're invoked with
        foreach (var module in modules)
        {
            foreach (var method in module.Methods.OfType<NewLoweredMethod>().Where(x =>
                         usefulMethodIds.Contains(x.Id)
                         && x.TypeParameters.Count == 0))
            {
                TryEnqueueMethodForProcessing(method, []);
            }
        }

        while (_methodProcessingQueue.TryDequeue(out var item))
        {
            var (method, typeArguments) = item;
            ProcessMethod(method, typeArguments);
            
            _codeSegment.AppendLine();
        }

        return $"""
                {AsmHeader}
                {_dataSegment}
                {_codeSegment}
                """;
    }

    private IMethod? GetMethod(DefId defId)
    {
        return modules.SelectMany(x => x.Methods)
            .FirstOrDefault(x => x.Id == defId);
    }

    private void CreateMain(NewLoweredMethod mainMethod)
    {
        _codeSegment.AppendLine("main:");

        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");
        // give CRT_INIT it's shadow space
        _codeSegment.AppendLine($"    sub     rsp, {ShadowSpaceBytes}");

        _codeSegment.AppendLine("    call    _CRT_INIT");
        // put rsp back
        _codeSegment.AppendLine($"    add     rsp, {ShadowSpaceBytes}");

        // give main it's shadow space
        _codeSegment.AppendLine($"    sub     rsp, {ShadowSpaceBytes}");
        _codeSegment.AppendLine($"    call    {mainMethod.Id.FullName}");

        _codeSegment.AppendLine($"    add     rsp, {ShadowSpaceBytes}");

        // zero out rax as return value
        _codeSegment.AppendLine("    xor     rax, rax");
        // move rax into rcx for exit process parameter
        _codeSegment.AppendLine("    mov     rcx, rax");
        _codeSegment.AppendLine("    call    ExitProcess");
        _codeSegment.AppendLine();
    }

    private Dictionary<string, int> _locals = null!;

    private static string GetMethodLabel(IMethod method, IReadOnlyList<NewLoweredConcreteTypeReference> typeArguments)
    {
        return typeArguments.Count == 0
            ? method.Id.FullName
            : $"{method.Id.FullName}_{string.Join("_", typeArguments.Select(x => x.DefinitionId.FullName))}";
    }

    private uint _methodCount;
    
    private void ProcessMethod(NewLoweredMethod method, IReadOnlyList<NewLoweredConcreteTypeReference> typeArguments)
    {
        _currentTypeArguments = typeArguments;
        _currentMethod = method;
        _methodCount++;
        _locals = [];
        var stackOffset = 32; // start with offset by 8 because return address is on the top of the stack 
        foreach (var local in method.Locals.Append(method.ReturnValue))
        {
            stackOffset += 8;
            _locals[local.CompilerGivenName] = -stackOffset;
        }
        
        _codeSegment.AppendLine($"{GetMethodLabel(method, typeArguments)}:");
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");
                
        // ensure stack space is 16 byte aligned
        stackOffset += stackOffset % 16;
        _codeSegment.AppendLine("; Allocate stack space for local variables and parameters");
        _codeSegment.AppendLine($"    sub     rsp, {stackOffset}");


        for (var index = 0; index < method.ParameterLocals.Count; index++)
        {
            var parameterLocal = method.ParameterLocals[index];

            var parameterOffset = (index + 2) * 8;
            
            var sourceRegister = index switch
            {
                0 => "rcx",
                1 => "rdx",
                2 => "r8",
                3 => "r9",
                _ => null
            };

            _locals[parameterLocal.CompilerGivenName] = parameterOffset;

            if (sourceRegister is not null)
            {
                _codeSegment.AppendLine($"    mov     QWORD [rbp{FormatOffset(parameterOffset)}], {sourceRegister}");
            }
        }


        foreach (var basicBlock in method.BasicBlocks)
        {
            _codeSegment.AppendLine($"{GetBasicBlockLabel(basicBlock.Id)}:");
            foreach (var statement in basicBlock.Statements)
            {
                ProcessStatement(statement);
            }

            ProcessTerminator(basicBlock.Terminator.NotNull());
        }

        _codeSegment.AppendLine();
    }


    private static string FormatOffset(int offset) => offset switch
    {
        0 => "",
        < 0 => $"-{-offset}",
        > 0 => $"+{offset}"
    };

    // private readonly Stack<FunctionDefinitionReference> _functionStack = [];
    private const uint ShadowSpaceBytes = 32;

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

    private void ProcessStatement(IStatement statement)
    {
        switch (statement)
        {
            case Assign assign:
            {
                var asmPlace = PlaceToAsmPlace(assign.Place);
                AssignRValue(asmPlace, assign.RValue);
                break;
            }
            case LocalAlive:
            case LocalDead:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }
    

    private enum IntSigned
    {
        Signed,
        Unsigned
    }
    
    private IntSigned? GetIntSigned(IOperand operand)
    {
        return operand switch
        {
            Copy{Place: var place} => GetPlaceType(place) switch
                {
                    NewLoweredConcreteTypeReference concrete when DefId.SignedInts.Contains(concrete.DefinitionId) => IntSigned.Signed,
                    NewLoweredConcreteTypeReference concrete when DefId.UnsignedInts.Contains(concrete.DefinitionId) => IntSigned.Unsigned,
                    _ => null
                },
            UIntConstant => IntSigned.Unsigned,
            IntConstant => IntSigned.Signed,
            BoolConstant or FunctionPointerConstant or StringConstant or UnitConstant => null,
            _ => throw new ArgumentOutOfRangeException(nameof(operand))
        };
    }
    

    private INewLoweredTypeReference GetPlaceType(IPlace place)
    {
        switch (place)
        {
            case Field field:
                throw new NotImplementedException();
            case Local local:
                return GetLocalType(local);
            case StaticField staticField:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(place));
        }
    }


    private INewLoweredTypeReference GetLocalType(Local local)
    {
        var currentMethod = _currentMethod.NotNull();
        IEnumerable<NewMethodLocal> locals =
            [..currentMethod.Locals, ..currentMethod.ParameterLocals, currentMethod.ReturnValue];
        var foundLocal = locals.First(x => x.CompilerGivenName == local.LocalName);
        return foundLocal.Type;
    }
    

    private void ProcessBinaryOperation(IAsmPlace destination, IOperand left, IOperand right, BinaryOperationKind kind)
    {
        switch (kind)
        {
            case BinaryOperationKind.Add:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                _codeSegment.AppendLine("    add     rax, rbx");
                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.Subtract:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                _codeSegment.AppendLine("    sub     rax, rbx");
                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.Multiply:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                var intSigned = GetIntSigned(left).NotNull();
                _codeSegment.AppendLine(intSigned == IntSigned.Signed
                    ? "    imul     rax, rbx"
                    : "    mul     rax, rbx");

                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.Divide:
                {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                var intSigned = GetIntSigned(left).NotNull();
                _codeSegment.AppendLine("    cqo");
                _codeSegment.AppendLine(intSigned == IntSigned.Signed
                    ? "    idiv     rbx"
                    : "    div     rbx");

                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.LessThan:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                _codeSegment.AppendLine("    cmp     rax, rbx");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine("    pop     rax");
                _codeSegment.AppendLine("    and     rax, 10000000b"); // sign flag
                _codeSegment.AppendLine("    shr     rax, 7");
                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.LessThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.GreaterThan:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                _codeSegment.AppendLine("    cmp     rbx, rax");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine("    pop     rax");
                _codeSegment.AppendLine("    and     rax, 10000000b"); // sign flag
                _codeSegment.AppendLine("    shr     rax, 7");
                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.GreaterThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.Equal:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                _codeSegment.AppendLine("    cmp     rax, rbx");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine("    pop     rax");
                _codeSegment.AppendLine("    and     rax, 1000000b"); // zero flag
                _codeSegment.AppendLine("    shr     rax, 6");
                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            case BinaryOperationKind.NotEqual:
            {
                MoveOperandToDestination(left, new Register("rax"));
                MoveOperandToDestination(right, new Register("rbx"));
                _codeSegment.AppendLine("    cmp     rax, rbx");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine("    pop     rax");
                _codeSegment.AppendLine("    and     rax, 1000000b"); // zero flag
                _codeSegment.AppendLine("    shr     rax, 6");
                _codeSegment.AppendLine("    btc     rax, 0");
                StoreAsmPlaceInPlace(new Register("rax"), destination);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void AssignRValue(IAsmPlace place, IRValue rValue)
    {
        switch (rValue)
        {
            case BinaryOperation binaryOperation:
            {
                ProcessBinaryOperation(place, binaryOperation.LeftOperand, binaryOperation.RightOperand, binaryOperation.Kind);
                break;
            }
            case CreateObject createObject:
                throw new NotImplementedException();
            case UnaryOperation unaryOperation:
            {
                ProcessUnaryOperation(place, unaryOperation.Operand, unaryOperation.Kind);
                break;
            }
            case Use use:
            {
                MoveOperandToDestination(use.Operand, place);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(rValue));
        }
    }

    private void ProcessUnaryOperation(IAsmPlace place, IOperand operand, UnaryOperationKind kind)
    {
        switch (kind)
        {
            case UnaryOperationKind.Not:
            {
                MoveOperandToDestination(operand, new Register("rax"));
                _codeSegment.AppendLine("    btc     rax, 0");
                StoreAsmPlaceInPlace(new Register("rax"), place);
                break;
            }
            case UnaryOperationKind.Negate:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private void ProcessTerminator(ITerminator terminator)
    {
        switch (terminator)
        {
            case GoTo goTo:
                _codeSegment.AppendLine($"    jmp     {GetBasicBlockLabel(goTo.BasicBlockId)}");
                break;
            case MethodCall methodCall:
                ProcessMethodCall(methodCall);
                break;
            case Return:
            {
                StoreAsmPlaceInPlace(PlaceToAsmPlace(new Local(_currentMethod.NotNull().ReturnValue.CompilerGivenName)), new Register("rax"));
                _codeSegment.AppendLine("    leave");
                _codeSegment.AppendLine("    ret");
                break;
            }
            case SwitchInt switchInt:
            {
                MoveOperandToDestination(switchInt.Operand, new Register("rax"));
                foreach (var (intCase, jumpTo) in switchInt.Cases)
                {
                    _codeSegment.AppendLine($"    cmp     rax, {intCase}");
                    _codeSegment.AppendLine($"    je      {GetBasicBlockLabel(jumpTo)}");
                }

                _codeSegment.AppendLine($"    jmp     {GetBasicBlockLabel(switchInt.Otherwise)}");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(terminator));
        }
    }

    private void ProcessMethodCall(MethodCall methodCall)
    {
        var arity = methodCall.Arguments.Count;
        
        _codeSegment.AppendLine($"; MethodCall({arity})");

        var calleeMethod = GetMethod(methodCall.Function.DefinitionId).NotNull();

        IReadOnlyList<NewLoweredConcreteTypeReference> calleeTypeArguments =
        [
            ..methodCall.Function.TypeArguments.Select(x =>
            {
                switch (x)
                {
                    case NewLoweredConcreteTypeReference concreteArgument:
                        return concreteArgument;
                    case NewLoweredGenericPlaceholder genericReefTypeReference:
                    {
                        Debug.Assert(genericReefTypeReference.OwnerDefinitionId == _currentMethod.NotNull().Id);
                        var typeArgumentIndex = _currentMethod.NotNull().TypeParameters.Index()
                            .First(y => y.Item.PlaceholderName == genericReefTypeReference.PlaceholderName).Index;
                        return _currentTypeArguments[typeArgumentIndex];
                    }
                    default:
                        throw new NotImplementedException();
                }
            })
        ];

        if (calleeMethod is NewLoweredMethod loweredMethod)
        {
            TryEnqueueMethodForProcessing(loweredMethod, calleeTypeArguments);
        }

        var functionLabel = GetMethodLabel(calleeMethod, calleeTypeArguments);
        
        // I think we can assume everything is already aligned now. We're not using the stack anymore other than local variables
        
        var parametersSpaceNeeded = Math.Max((methodCall.Arguments.Count - 4) * 8, 0) + 32;
        parametersSpaceNeeded += parametersSpaceNeeded % 16;
        
        // move first four arguments into registers as specified by win 64 calling convention, then
        // shift the remaining arguments up by four so that the 'top' 32 bytes are free and can act as
        // the callee's shadow space 

        _codeSegment.AppendLine($"    sub     rsp, {parametersSpaceNeeded}");
        
        for (var i = arity - 1; i >= 0; i--)
        {
            IAsmPlace destination = i switch
            {
                0 => new Register("rcx"),
                1 => new Register("rdx"),
                2 => new Register("r8"),
                3 => new Register("r9"),
                _ => new OffsetFromStackPointer(i * 8)
            };
            
            var argument = methodCall.Arguments[i];
            
            MoveOperandToDestination(argument, destination);
        }
        
        _codeSegment.AppendLine($"    call    {functionLabel}");

        // move rsp back to where it was before we called the function
        _codeSegment.AppendLine($"    add     rsp, {parametersSpaceNeeded}");

        StoreAsmPlaceInPlace(new Register("rax"), PlaceToAsmPlace(methodCall.PlaceDestination));

        _codeSegment.AppendLine($"    jmp     {GetBasicBlockLabel(methodCall.GoToAfter)}");
    }

    private string GetBasicBlockLabel(BasicBlockId basicBlockId)
    {
        return $"{basicBlockId.Id}_{_methodCount}";
    }

    private interface IAsmPlace;

    private record Register(string Name) : IAsmPlace;


    private record OffsetFromBasePointer(int Offset) : IAsmPlace;
    private record OffsetFromStackPointer(int Offset) : IAsmPlace;

    private void MoveOperandToDestination(IOperand operand, IAsmPlace destination)
    {
        switch (operand)
        {
            case BoolConstant boolConstant:
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, {(boolConstant.Value ? '1' : '0')}");
                break;
            case Copy copy:
            {
                StoreAsmPlaceInPlace(PlaceToAsmPlace(copy.Place), destination);
                break;
            }
            case FunctionPointerConstant functionPointerConstant:
                throw new NotImplementedException();
            case IntConstant intConstant:
            {
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, 0x{intConstant.Value:X}");
                break;
            }
            case StringConstant stringConstant:
            {
                if (!_strings.TryGetValue(stringConstant.Value, out var stringName))
                {
                    stringName = $"_str_{_strings.Count}";
                    _strings[stringConstant.Value] = stringName;
                    // todo: no null terminated strings
                    _dataSegment.AppendLine(
                        $"    {stringName} db \"{stringConstant.Value}\", 0");
                }

                LoadEffectiveAddress(destination, $"[{stringName}]");
                break;
            }
            case UIntConstant uIntConstant:
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, 0x{uIntConstant.Value:X}");
                break;
            case UnitConstant unitConstant:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(operand));
        }
    }

    private void LoadEffectiveAddress(IAsmPlace place, string operand)
    {
        if (place is Register { Name: var registerName })
        {
            _codeSegment.AppendLine($"    lea     {registerName}, {operand}");
            return;
        }

        _codeSegment.AppendLine($"    lea     rax, {operand}");
        StoreAsmPlaceInPlace(new Register("rax"), place);
    }
    
    private static string GetPlaceAsm(IAsmPlace place)
    {
        return place switch
        {
            OffsetFromStackPointer {Offset: var offset} => $"QWORD [rsp{FormatOffset(offset)}]",
            OffsetFromBasePointer {Offset: var offset} => $"QWORD [rbp{FormatOffset(offset)}]",
            Register {Name: var name} => name,
            _ => throw new ArgumentOutOfRangeException(nameof(place))
        };
    }

    private void StoreAsmPlaceInPlace(IAsmPlace source, IAsmPlace destination)
    {
        switch (source, destination)
        {
            case (Register sourceRegister, Register destinationRegister):
            {
                _codeSegment.AppendLine($"    mov     {destinationRegister.Name}, {sourceRegister.Name}");
                break;
            }
            case (Register sourceRegister, OffsetFromBasePointer or OffsetFromStackPointer):
            {
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, {sourceRegister.Name}");
                break;
            }
            case (OffsetFromBasePointer or OffsetFromStackPointer, Register destinationRegister):
            {
                _codeSegment.AppendLine($"    mov     {destinationRegister.Name}, {GetPlaceAsm(source)}");
                break;
            }
            case (OffsetFromBasePointer or OffsetFromStackPointer, OffsetFromBasePointer or OffsetFromStackPointer):
            {
                _codeSegment.AppendLine($"    mov     rax, {GetPlaceAsm(source)}");
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, rax");
                break;
            }
            default:
                throw new UnreachableException();
        }
    }

    private IAsmPlace PlaceToAsmPlace(IPlace place)
    {
        return place switch
        {
            Field field => throw new NotImplementedException(),
            Local local => new OffsetFromBasePointer(_locals[local.LocalName]),
            StaticField staticField => throw new NotImplementedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(place))
        };
    }
}
