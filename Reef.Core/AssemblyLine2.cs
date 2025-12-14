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
        var stackOffset = 8; // start with offset by 8 because return address is on the top of the stack 
        foreach (var local in method.Locals.Append(method.ReturnValue))
        {
            _locals[local.CompilerGivenName] = -stackOffset;
            stackOffset += 8;
        }
        
        _codeSegment.AppendLine($"{GetMethodLabel(method, typeArguments)}:");

        for (var index = 0; index < method.ParameterLocals.Count; index++)
        {
            var parameterLocal = method.ParameterLocals[index];
            var parameterOffset = index * 8;
            
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
                _codeSegment.AppendLine($"    mov    [rbp+{parameterOffset}], {sourceRegister}");
            }
        }
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");

        // ensure stack space is 16 byte aligned
        stackOffset += stackOffset % 16;
        _codeSegment.AppendLine("; Allocate stack space for local variables and parameters");
        _codeSegment.AppendLine($"    sub     rsp, {stackOffset}");

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

        // var labels = method.Instructions.Labels.ToLookup(x => x.ReferencesInstructionIndex, x => x.Name);
        // for (var i = 0; i < method.Instructions.Instructions.Count; i++)
        // {
        //     foreach (var label in labels[(uint)i])
        //     {
        //         _codeSegment.AppendLine($"{label}:");
        //     }
        //     ProcessInstruction(method.Instructions.Instructions[i], method, typeArguments);
        // }
    }

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
                throw new NotImplementedException();
            case BinaryOperationKind.Multiply:
                throw new NotImplementedException();
            case BinaryOperationKind.Divide:
                throw new NotImplementedException();
            case BinaryOperationKind.LessThan:
                throw new NotImplementedException();
            case BinaryOperationKind.LessThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.GreaterThan:
                throw new NotImplementedException();
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
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, rax");
                break;
            }
            case BinaryOperationKind.NotEqual:
                throw new NotImplementedException();
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
                throw new NotImplementedException();
            case Use use:
            {
                MoveOperandToDestination(use.Operand, place);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(rValue));
        }
    }

    private void ProcessTerminator(ITerminator terminator)
    {
        switch (terminator)
        {
            case GoTo goTo:
                _codeSegment.AppendLine($"    jmp    {GetBasicBlockLabel(goTo.BasicBlockId)}");
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

                _codeSegment.AppendLine($"    jmp    {GetBasicBlockLabel(switchInt.Otherwise)}");
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
        
        // store the current rbp, and align rsp to 16 bytes
        // _codeSegment.AppendLine("; store previous rbp and rsp, then align stack to 16 bytes");
        // _codeSegment.AppendLine("    push    rbp");
        // _codeSegment.AppendLine("    mov     rbp, rsp");
        // _codeSegment.AppendLine("    and     rsp, 0xFFFFFFFFFFFFFFF0");
        
        var parametersSpaceNeeded = Math.Max(ShadowSpaceBytes, methodCall.Arguments.Count * 8);
        parametersSpaceNeeded += parametersSpaceNeeded % 16;
        _codeSegment.AppendLine($"    sub     rsp, {parametersSpaceNeeded}");
        
        // move first four arguments into registers as specified by win 64 calling convention, then
        // shift the remaining arguments up by four so that the 'top' 32 bytes are free and can act as
        // the callee's shadow space 
        for (var i = 0; i < arity; i++)
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
                throw new NotImplementedException();
            case Copy copy:
            {
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, {GetPlaceAsm(PlaceToAsmPlace(copy.Place))}");
                break;
            }
            case FunctionPointerConstant functionPointerConstant:
                throw new NotImplementedException();
            case IntConstant intConstant:
            {
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, {intConstant.Value:X}h");
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
                _codeSegment.AppendLine($"    mov     {GetPlaceAsm(destination)}, {uIntConstant.Value:X}h");
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
        _codeSegment.AppendLine($"    mov     {GetPlaceAsm(place)}, rax");
    }
    
    private string GetPlaceAsm(IAsmPlace place)
    {
        return place switch
        {
            OffsetFromStackPointer {Offset: < 0} sp => $"[rsp-{-sp.Offset}]",
            OffsetFromStackPointer {Offset: > 0} sp => $"[rsp+{sp.Offset}]",
            OffsetFromStackPointer {Offset: 0} => "[rsp]",
            OffsetFromBasePointer {Offset: 0} => "[rbp]",
            OffsetFromBasePointer {Offset: < 0} bp => $"[rbp-{-bp.Offset}]",
            OffsetFromBasePointer {Offset: > 0} bp => $"[rbp+{bp.Offset}]",
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
    
    // private void ProcessInstruction(IInstruction instruction, ReefMethod method, IReadOnlyList<ConcreteReefTypeReference> typeArguments)
    // {
    //     switch (instruction)
    //     {
    //         case BoolNot:
    //         {
    //             _codeSegment.AppendLine("; BOOL_NOT");
    //
    //             _codeSegment.AppendLine("    xor    [rsp], 1h");
    //             break;
    //         }
    //         case Branch branch:
    //         {
    //             _codeSegment.AppendLine($"; BRANCH({branch.BranchToLabelName})");
    //             _codeSegment.AppendLine($"    jmp     {branch.BranchToLabelName}");
    //             break;
    //         }
    //         case BranchIfFalse branchIfFalse:
    //         {
    //             _codeSegment.AppendLine($"; BRANCH_IF_FALSE({branchIfFalse.BranchToLabelName})");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    cmp     rax, 0");
    //             _codeSegment.AppendLine($"    je     {branchIfFalse.BranchToLabelName}");
    //             break;
    //         }
    //         case BranchIfTrue branchIfTrue:
    //         {
    //             _codeSegment.AppendLine($"; BRANCH_IF_TRUE({branchIfTrue.BranchToLabelName})");
    //             
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    cmp     rax, 1");
    //             _codeSegment.AppendLine($"    je     {branchIfTrue.BranchToLabelName}");
    //             break;
    //         }
    //         case Call call:
    //             {
    //                 _codeSegment.AppendLine($"; CALL({call.Arity})");
    //                 var functionDefinition = _functionStack.Pop();
    //                 var calleeMethod = GetMethod(functionDefinition.DefinitionId).NotNull();
    //                 IReadOnlyList<ConcreteReefTypeReference> calleeTypeArguments =
    //                     [..functionDefinition.TypeArguments.Select(x =>
    //                     {
    //                         switch (x)
    //                         {
    //                             case ConcreteReefTypeReference concreteArgument:
    //                                 return concreteArgument;
    //                             case GenericReefTypeReference genericReefTypeReference:
    //                             {
    //                                 Debug.Assert(genericReefTypeReference.DefinitionId == method.Id);
    //                                 var typeArgumentIndex = method.TypeParameters.Index()
    //                                     .First(y => y.Item == genericReefTypeReference.TypeParameterName).Index;
    //                                 return typeArguments[typeArgumentIndex];
    //                             }
    //                             default:
    //                                 throw new NotImplementedException();
    //                         }
    //                     })];
    //                 
    //                 TryEnqueueMethodForProcessing(calleeMethod, calleeTypeArguments);
    //                 
    //                 var functionLabel = GetMethodLabel(calleeMethod, calleeTypeArguments);
    //                 
    //                 // store the current rbp, and align rsp to 16 bytes
    //                 _codeSegment.AppendLine("; store previous rbp and rsp, then align stack to 16 bytes");
    //                 _codeSegment.AppendLine("    push    rbp");
    //                 _codeSegment.AppendLine("    mov     rbp, rsp");
    //                 _codeSegment.AppendLine("    and     rsp, 0xFFFFFFFFFFFFFFF0");
    //
    //                 var parametersSpaceNeeded = Math.Max(ShadowSpaceBytes, call.Arity * 8);
    //                 parametersSpaceNeeded += parametersSpaceNeeded % 16;
    //                 _codeSegment.AppendLine($"    sub     rsp, {parametersSpaceNeeded}");
    //                 
    //                 // current      target
    //                 // -----------------------
    //                 // 1            1
    //                 // 2            2
    //                 // 3            3
    //                 // 4            4
    //                 // 5            5
    //                 // 6            6
    //                 // 7            7
    //                 // <-- rsp      previous rbp <-- rbp
    //                 //              [maybe space]
    //                 //              7
    //                 //              6
    //                 //              5
    //                 //              [empty]
    //                 //              [empty]
    //                 //              [empty]
    //                 //              [empty]
    //                 //              <-- rsp
    //
    //                 // move first four arguments into registers as specified by win 64 calling convention, then
    //                 // shift the remaining arguments up by four so that the 'top' 32 bytes are free and can act as
    //                 // the callee's shadow space 
    //                 for (var i = 0; i < call.Arity; i++)
    //                 {
    //                     var source = $"[rbp+{(call.Arity - i) * 8}]";
    //                     var destination = i switch
    //                     {
    //                         0 => "rcx",
    //                         1 => "rdx",
    //                         2 => "r8",
    //                         3 => "r9",
    //                         _ => null
    //                     };
    //
    //                     if (destination is null)
    //                     {
    //                         _codeSegment.AppendLine($"    mov     rax, {source}");
    //                         _codeSegment.AppendLine($"    mov     [rsp+{i * 8}], rax");
    //                     }
    //                     else
    //                     {
    //                         _codeSegment.AppendLine($"    mov     {destination}, {source}");
    //                     }
    //                 }
    //
    //                 // give the function we're calling its shadow space
    //                 _codeSegment.AppendLine($"    call    {functionLabel}");
    //
    //                 // move rsp back to where it was before we called the function
    //                 _codeSegment.AppendLine("    mov     rsp, rbp");
    //                 _codeSegment.AppendLine("    pop     rbp");
    //                 if (call.Arity > 0)
    //                 {
    //                     _codeSegment.AppendLine($"    add     rsp, {(call.Arity * 8):X}");
    //                 }
    //
    //                 if (call.ValueUseful && (calleeMethod.ReturnType is not ConcreteReefTypeReference concrete || concrete.DefinitionId != DefId.Unit))
    //                 {
    //                     _codeSegment.AppendLine("    push    rax");
    //                 }
    //
    //                 break;
    //             }
    //         case CastBoolToInt:
    //             {
    //                 _codeSegment.AppendLine("; CAST_BOOL_TO_INT");
    //                 // noop
    //                 break;
    //             }
    //         case CompareInt64Equal:
    //         case CompareInt32Equal:
    //         case CompareInt16Equal:
    //         case CompareInt8Equal:
    //         case CompareUInt64Equal:
    //         case CompareUInt32Equal:
    //         case CompareUInt16Equal:
    //         case CompareUInt8Equal:
    //         {
    //             _codeSegment.AppendLine("; COMPARE_INT_EQUAL");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    cmp     rax, [rsp]");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    pushf");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    and     rax, 1000000b"); // zero flag
    //             _codeSegment.AppendLine("    shr     rax, 6");
    //             _codeSegment.AppendLine("    push    rax");
    //
    //             break;
    //         }
    //         case CompareInt64NotEqual:
    //         case CompareInt32NotEqual:
    //         case CompareInt16NotEqual:
    //         case CompareInt8NotEqual:
    //         case CompareUInt64NotEqual:
    //         case CompareUInt32NotEqual:
    //         case CompareUInt16NotEqual:
    //         case CompareUInt8NotEqual:
    //         {
    //             _codeSegment.AppendLine("; COMPARE_INT_NOT_EQUAL");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    cmp     rax, [rsp]");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    pushf");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    and     rax, 1000000b"); // zero flag
    //             _codeSegment.AppendLine("    shr     rax, 6");
    //             _codeSegment.AppendLine("    xor     rax, 1b");
    //             _codeSegment.AppendLine("    push    rax");
    //             break;
    //         }
    //         case CompareInt64GreaterOrEqualTo:
    //         case CompareInt32GreaterOrEqualTo:
    //         case CompareInt16GreaterOrEqualTo:
    //         case CompareInt8GreaterOrEqualTo:
    //         case CompareUInt64GreaterOrEqualTo:
    //         case CompareUInt32GreaterOrEqualTo:
    //         case CompareUInt16GreaterOrEqualTo:
    //         case CompareUInt8GreaterOrEqualTo:
    //             _codeSegment.AppendLine("; COMPARE_INT_GREATER_OR_EQUAL");
    //             throw new NotImplementedException();
    //         case CompareUInt64GreaterThan:
    //         case CompareUInt32GreaterThan:
    //         case CompareUInt16GreaterThan:
    //         case CompareUInt8GreaterThan:
    //         case CompareInt64GreaterThan:
    //         case CompareInt32GreaterThan:
    //         case CompareInt16GreaterThan:
    //         case CompareInt8GreaterThan:
    //         {
    //             _codeSegment.AppendLine("; COMPARE_INT_GREATER");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    cmp     rax, [rsp]");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    pushf");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    and     rax, 10000000b"); // sign flag
    //             _codeSegment.AppendLine("    shr     rax, 7");
    //             _codeSegment.AppendLine("    push    rax");
    //             break;
    //         }
    //         case CompareInt64LessOrEqualTo:
    //         case CompareInt32LessOrEqualTo:
    //         case CompareInt16LessOrEqualTo:
    //         case CompareInt8LessOrEqualTo:
    //         case CompareUInt64LessOrEqualTo:
    //         case CompareUInt32LessOrEqualTo:
    //         case CompareUInt16LessOrEqualTo:
    //         case CompareUInt8LessOrEqualTo:
    //             throw new NotImplementedException();
    //         case CompareInt64LessThan:
    //         case CompareInt32LessThan:
    //         case CompareInt16LessThan:
    //         case CompareInt8LessThan:
    //         case CompareUInt64LessThan:
    //         case CompareUInt32LessThan:
    //         case CompareUInt16LessThan:
    //         case CompareUInt8LessThan:
    //         {
    //             _codeSegment.AppendLine("; COMPARE_INT_LESS");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    pop     rdx");
    //             _codeSegment.AppendLine("    cmp     rdx, rax");
    //             _codeSegment.AppendLine("    pushf");
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    and     rax, 10000000b"); // sign flag
    //             _codeSegment.AppendLine("    shr     rax, 7");
    //             _codeSegment.AppendLine("    push    rax");
    //             break;
    //         }
    //         case CopyStack:
    //             _codeSegment.AppendLine("; COPY_STACK");
    //             throw new NotImplementedException();
    //         case CreateObject createObject:
    //             _codeSegment.AppendLine($"; CREATE_OBJECT({createObject.ReefType.Name}:<{string.Join(",", createObject.ReefType.TypeArguments)}>)");
    //             throw new NotImplementedException();
    //         case Drop:
    //             _codeSegment.AppendLine("; DROP");
    //             throw new NotImplementedException();
    //         case Int64Divide:
    //         case Int32Divide:
    //         case Int16Divide:
    //         case Int8Divide:
    //         {
    //             _codeSegment.AppendLine("; INT_DIVIDE");
    //
    //             _codeSegment.AppendLine("    pop     rcx");
    //             _codeSegment.AppendLine("    pop     rax");
    //
    //             // extend rax to double quad word for divide operation
    //             _codeSegment.AppendLine("    cqo");
    //             _codeSegment.AppendLine("    idiv    rcx");
    //             _codeSegment.AppendLine("    push    rax");
    //
    //             // todo: panic/throw when dividing by zero
    //             break;
    //         }
    //         case UInt64Divide:
    //         case UInt32Divide:
    //         case UInt16Divide:
    //         case UInt8Divide:
    //         {
    //             _codeSegment.AppendLine("; INT_DIVIDE");
    //
    //             _codeSegment.AppendLine("    pop     rcx");
    //             _codeSegment.AppendLine("    pop     rax");
    //
    //             // extend rax to double quad word for divide operation
    //             _codeSegment.AppendLine("    cqo");
    //             _codeSegment.AppendLine("    div     rcx");
    //             _codeSegment.AppendLine("    push    rax");
    //
    //             // todo: panic/throw when dividing by zero
    //             break;
    //         }
    //         case Int64Minus:
    //         case Int32Minus:
    //         case Int16Minus:
    //         case Int8Minus:
    //         case UInt64Minus:
    //         case UInt32Minus:
    //         case UInt16Minus:
    //         case UInt8Minus:
    //         {
    //             _codeSegment.AppendLine("; INT_MINUS");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    sub     [rsp], rax");
    //             break;
    //         }
    //         case Int64Multiply:
    //         case Int32Multiply:
    //         case Int16Multiply:
    //         case Int8Multiply:
    //         {
    //             _codeSegment.AppendLine("; INT_MULTIPLY");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    pop     rbx");
    //             // imul requires both arguments to be in registers (I think)
    //             _codeSegment.AppendLine("    imul    rax, rbx");
    //             _codeSegment.AppendLine("    push    rax");
    //             // todo: panic/throw on overflow
    //             break;
    //         }
    //         case UInt64Multiply:
    //         case UInt32Multiply:
    //         case UInt16Multiply:
    //         case UInt8Multiply:
    //         {
    //             _codeSegment.AppendLine("; INT_MULTIPLY");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    pop     rbx");
    //             // mul requires both arguments to be in registers (I think)
    //             _codeSegment.AppendLine("    mul     rax, rbx");
    //             _codeSegment.AppendLine("    push    rax");
    //             // todo: panic/throw on overflow
    //             break;
    //         }
    //         case Int64Plus:
    //         case Int32Plus:
    //         case Int16Plus:
    //         case Int8Plus:
    //         case UInt64Plus:
    //         case UInt32Plus:
    //         case UInt16Plus:
    //         case UInt8Plus:
    //         {
    //             _codeSegment.AppendLine("; INT_PLUS");
    //
    //             _codeSegment.AppendLine("    pop     rax");
    //             _codeSegment.AppendLine("    add    [rsp], rax");
    //             break;
    //         }
    //         case LoadArgument loadArgument:
    //             {
    //                 _codeSegment.AppendLine($"; LOAD_ARGUMENT({loadArgument.ArgumentIndex})");
    //
    //                 var stackOffset = _parameters[loadArgument.ArgumentIndex];
    //
    //                 _codeSegment.AppendLine($"    push    [rbp+{stackOffset + 8}]");
    //
    //                 break;
    //             }
    //         case LoadBoolConstant loadBoolConstant:
    //             {
    //                 _codeSegment.AppendLine($"; LOAD_BOOL_CONSTANT({loadBoolConstant.Value})");
    //
    //                 _codeSegment.AppendLine($"    push    {(loadBoolConstant.Value ? 1 : 0)}h");
    //
    //                 break;
    //             }
    //         case LoadField loadField:
    //             _codeSegment.AppendLine($"; LOAD_FIELD({loadField.VariantIndex}:{loadField.FieldName})");
    //             throw new NotImplementedException();
    //         case LoadFunction loadFunction:
    //             {
    //                 _codeSegment.AppendLine($"; LOAD_FUNCTION({loadFunction.FunctionDefinitionReference.Name})");
    //                 _functionStack.Push(loadFunction.FunctionDefinitionReference);
    //                 break;
    //             }
    //         case LoadInt64Constant loadIntConstant:
    //             {
    //                 _codeSegment.AppendLine($"; LOAD_INT64_CONSTANT({loadIntConstant.Value})");
    //                 if (loadIntConstant.Value < 0) throw new NotImplementedException();
    //                 _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //                 break;
    //             }
    //         case LoadInt32Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_INT32_CONSTANT({loadIntConstant.Value})");
    //             if (loadIntConstant.Value < 0) throw new NotImplementedException();
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadInt16Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_INT16_CONSTANT({loadIntConstant.Value})");
    //             if (loadIntConstant.Value < 0) throw new NotImplementedException();
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadInt8Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_INT8_CONSTANT({loadIntConstant.Value})");
    //             if (loadIntConstant.Value < 0) throw new NotImplementedException();
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadUInt64Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_UINT64_CONSTANT({loadIntConstant.Value})");
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadUInt32Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_UINT32_CONSTANT({loadIntConstant.Value})");
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadUInt16Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_UINT16_CONSTANT({loadIntConstant.Value})");
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadUInt8Constant loadIntConstant:
    //         {
    //             _codeSegment.AppendLine($"; LOAD_UINT8_CONSTANT({loadIntConstant.Value})");
    //             _codeSegment.AppendLine($"    push    {loadIntConstant.Value:X}h");
    //             break;
    //         }
    //         case LoadLocal loadLocal:
    //             {
    //                 _codeSegment.AppendLine($"; LOAD_LOCAL({loadLocal.LocalName})");
    //                 var stackOffset = _locals[loadLocal.LocalName];
    //                 _codeSegment.AppendLine($"    push    [rbp-{stackOffset + 8 }]");
    //                 break;
    //             }
    //         case LoadStaticField loadStaticField:
    //             _codeSegment.AppendLine($"; LOAD_STATIC_FIELD({loadStaticField})");
    //             throw new NotImplementedException();
    //         case LoadStringConstant loadStringConstant:
    //             {
    //                 _codeSegment.AppendLine($"; LOAD_STRING_CONSTANT(\"{loadStringConstant.Value}\")");
    //                 if (!_strings.TryGetValue(loadStringConstant.Value, out var stringName))
    //                 {
    //                     stringName = $"_str_{_strings.Count}";
    //                     _strings[loadStringConstant.Value] = stringName;
    //                     // todo: no null terminated strings
    //                     _dataSegment.AppendLine(
    //                         $"    {stringName} db \"{loadStringConstant.Value}\", 0");
    //                 }
    //
    //                 _codeSegment.AppendLine($"    lea     rax, [{stringName}]");
    //                 _codeSegment.AppendLine("    push    rax");
    //                 break;
    //             }
    //         case LoadType loadType:
    //             _codeSegment.AppendLine($"; LOAD_TYPE({loadType.ReefType})");
    //             throw new NotImplementedException();
    //         case LoadUnitConstant:
    //             _codeSegment.AppendLine("; LOAD_UNIT_CONSTANT");
    //             // noop
    //             break;
    //         case Return:
    //             {
    //                 _codeSegment.AppendLine("; RETURN");
    //                 // zero out return value for null return
    //                 _codeSegment.AppendLine(method.ReturnType is ConcreteReefTypeReference reference && reference.DefinitionId == DefId.Unit
    //                     ? "    xor     rax, rax"
    //                     : "    pop     rax");
    //
    //                 _codeSegment.AppendLine("    leave");
    //                 _codeSegment.AppendLine("    ret");
    //                 break;
    //             }
    //         case StoreField storeField:
    //             _codeSegment.AppendLine($"; STORE_FIELD({storeField.VariantIndex}:{storeField.FieldName})");
    //             throw new NotImplementedException();
    //         case StoreLocal storeLocal:
    //             {
    //                 _codeSegment.AppendLine($"; STORE_LOCAL({storeLocal.LocalName})");
    //                 var stackOffset = _locals[storeLocal.LocalName];
    //                 // pop the value on the stack into rax
    //                 _codeSegment.AppendLine("    pop     rax");
    //
    //                 // move rax into the local's dedicated stack space
    //                 _codeSegment.AppendLine($"    mov     [rbp-{stackOffset + 8}], rax");
    //                 break;
    //             }
    //         case StoreStaticField storeStaticField:
    //             _codeSegment.AppendLine($"; STORE_STATIC_FIELD({storeStaticField.StaticFieldName})");
    //             throw new NotImplementedException();
    //         case SwitchInt switchInt:
    //             {
    //                 _codeSegment.AppendLine("; SWITCH_INT");
    //                 foreach (var branch in switchInt.BranchLabels)
    //                 {
    //                     _codeSegment.AppendLine("    pop     rax");
    //                     _codeSegment.AppendLine($"    cmp     rax, {branch.Key:x}");
    //                     _codeSegment.AppendLine($"    je      {branch.Value}");
    //                 }
    //                 _codeSegment.AppendLine($"    jmp     {switchInt.Otherwise}");
    //                 break;
    //             }
    //         default:
    //             throw new ArgumentOutOfRangeException(nameof(instruction));
    //     }
    // }
}
