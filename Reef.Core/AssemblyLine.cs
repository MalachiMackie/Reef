using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;
using Index = Reef.Core.LoweredExpressions.Index;

namespace Reef.Core;

#pragma warning disable CS9113 // Parameter is unread.
public class AssemblyLine(IReadOnlyList<LoweredModule> modules, HashSet<DefId> usefulMethodIds, ILogger logger)
#pragma warning restore CS9113 // Parameter is unread.
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

    private const uint PointerSize = 8;
    private const uint MaxParameterSize = 8;
    private const string ReturnValueAddressLocal = "_returnValueAddress";

    /// <summary>
    /// Dictionary of string constants to their data segment labels
    /// </summary>
    private readonly Dictionary<string, string> _strings = [];

    private readonly Queue<(LoweredMethod Method, IReadOnlyList<ILoweredTypeReference> TypeArguments)>
        _methodProcessingQueue = [];

    private readonly HashSet<Register> _registersInUse = [];

    private LoweredMethod? _currentMethod;
    private Dictionary<string, ILoweredTypeReference> _currentTypeArguments = [];
    private readonly Dictionary<DefId, DataType> _dataTypes = modules.SelectMany(x => x.DataTypes).ToDictionary(
        x => x.Id);

    private readonly HashSet<string> _queuedMethodLabels = [];

    private void TryEnqueueMethodForProcessing(LoweredMethod method, IReadOnlyList<ILoweredTypeReference> typeArguments)
    {
        var label = GetMethodLabel(method, typeArguments);
        if (_queuedMethodLabels.Add(label))
        {
            _methodProcessingQueue.Enqueue((method, typeArguments));
        }
    }

    public static string Process(IReadOnlyList<LoweredModule> modules, HashSet<DefId> usefulMethodIds, ILogger logger)
    {
        var assemblyLine = new AssemblyLine(modules, usefulMethodIds, logger);
        return assemblyLine.ProcessInner();
    }

    private string ProcessInner()
    {
        var mainModule = modules.Where(x => x.Methods.Any(y => y.Name == "_Main")).ToArray();

        if (mainModule.Length != 1)
        {
            throw new InvalidOperationException("Expected a single module with a main method");
        }

        var mainMethod = mainModule[0].Methods.OfType<LoweredMethod>().Single(x => x.Name == "_Main");

        foreach (var externMethod in modules.SelectMany(x => x.Methods)
                     .OfType<LoweredExternMethod>()
                     .Where(x => usefulMethodIds.Contains(x.Id)))
        {
            _codeSegment.AppendLine($"extern {externMethod.Name}");
        }

        CreateMain(mainMethod);

        // enqueue all non-generic methods. Generic methods get enqueued lazily based on what they're type arguments they're invoked with
        foreach (var module in modules)
        {
            foreach (var method in module.Methods.OfType<LoweredMethod>().Where(x =>
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

    private void CreateMain(LoweredMethod mainMethod)
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
        _codeSegment.AppendLine($"    xor     {Register.A.ToAsm(PointerSize)}, {Register.A.ToAsm(PointerSize)}");
        // move rax into rcx for exit process parameter
        _codeSegment.AppendLine($"    mov     {Register.C.ToAsm(PointerSize)}, {Register.A.ToAsm(PointerSize)}");
        _codeSegment.AppendLine("    call    ExitProcess");
        _codeSegment.AppendLine();
    }

    private Dictionary<string, LocalInfo> _locals = null!;

    private sealed record LocalInfo(IAsmPlace Place, ILoweredTypeReference Type);

    private static string GetMethodLabel(IMethod method, IReadOnlyList<ILoweredTypeReference> typeArguments)
    {
        return typeArguments.Count == 0
            ? method.Id.FullName
            : $"{method.Id.FullName}_{string.Join("_", typeArguments.Select(x => x switch {
                    LoweredConcreteTypeReference concrete => concrete.DefinitionId.FullName,
                    LoweredPointer(LoweredConcreteTypeReference pointerToConcrete) => $"Pointer_{pointerToConcrete.DefinitionId.FullName}_",
                    LoweredPointer(var pointerTo) => throw new NotImplementedException(pointerTo.GetType().ToString()),
                    _ => throw new NotImplementedException(x.GetType().ToString())
                }))}";
    }

    private uint _methodCount;

    private readonly List<KeyValuePair<ILoweredTypeReference, TypeSizeInfo>> _typeSizes = [];

    private sealed record TypeSizeInfo(
        uint Size,
        uint Alignment,
        Dictionary<string, Dictionary<string, FieldSize>> VariantFieldOffsets);

    private sealed record FieldSize(uint Offset, uint Size, uint Alignment);

    private bool AreTypeReferencesEqual(ILoweredTypeReference left, ILoweredTypeReference right, Dictionary<string, ILoweredTypeReference> typeArguments)
    {
        switch (left, right)
        {
            case (LoweredConcreteTypeReference leftConcrete, LoweredConcreteTypeReference rightConcrete):
            {
                return leftConcrete.DefinitionId == rightConcrete.DefinitionId
                       && leftConcrete.TypeArguments.Count == rightConcrete.TypeArguments.Count
                       && leftConcrete.TypeArguments.Zip(rightConcrete.TypeArguments).All(x => AreTypeReferencesEqual(x.First, x.Second, typeArguments));
            }
            case (LoweredGenericPlaceholder leftGeneric, LoweredGenericPlaceholder rightGeneric):
            {
                var leftConcrete = typeArguments[leftGeneric.PlaceholderName];
                var rightConcrete = typeArguments[rightGeneric.PlaceholderName];
                return AreTypeReferencesEqual(leftConcrete, rightConcrete, typeArguments);
            }
            case (LoweredPointer leftPointer, LoweredPointer rightPointer):
            {
                return AreTypeReferencesEqual(leftPointer.PointerTo, rightPointer.PointerTo, typeArguments);
            }
            case (LoweredArray leftArray, LoweredArray rightArray):
            {
                return AreTypeReferencesEqual(leftArray.ElementType, rightArray.ElementType, typeArguments)
                       && leftArray.Length == rightArray.Length;
            }
        }

        if (left.GetType() == right.GetType())
        {
            throw new NotImplementedException($"{left.GetType()}, {right.GetType()}");
        }
        
        return false;
    }
    
    private TypeSizeInfo GetTypeSize(ILoweredTypeReference typeReference, Dictionary<string, ILoweredTypeReference> typeArguments)
    {
        var foundTypeReference = _typeSizes.FirstOrDefault(x => AreTypeReferencesEqual(x.Key, typeReference, typeArguments));
        if (foundTypeReference.Key is not null)
        {
            return foundTypeReference.Value;
        }

        var size = 0u;
        var alignment = 1u;
        // var size = new SizeAndAlignment(0, 1);
        var dataTypeFieldOffsets = new Dictionary<string, Dictionary<string, FieldSize>>();
        
        switch (typeReference)
        {
            case LoweredConcreteTypeReference concreteTypeReference:
            {
                if (concreteTypeReference.DefinitionId == DefId.Int64
                    || concreteTypeReference.DefinitionId == DefId.UInt64)
                {
                    size = 8;
                    alignment = 8;
                    break;
                }

                if (concreteTypeReference.DefinitionId == DefId.RawPointer)
                {
                    size = PointerSize;
                    alignment = PointerSize;
                    break;
                }

                if (concreteTypeReference.DefinitionId == DefId.Int32 || concreteTypeReference.DefinitionId == DefId.UInt32)
                {
                    size = 4;
                    alignment = 4;
                    break;
                }
                
                if (concreteTypeReference.DefinitionId == DefId.Int16 || concreteTypeReference.DefinitionId == DefId.UInt16)
                {
                    size = 2;
                    alignment = 2;
                    break;
                }
                
                if (concreteTypeReference.DefinitionId == DefId.Int8
                    || concreteTypeReference.DefinitionId == DefId.UInt8
                    || concreteTypeReference.DefinitionId == DefId.Boolean
                    || concreteTypeReference.DefinitionId == DefId.Unit)
                {
                    size = 1;
                    alignment = 1;
                    break;
                }
                
                var dataType = _dataTypes[concreteTypeReference.DefinitionId];
                
                foreach (var variant in dataType.Variants)
                {
                    var variantSize = 0u;
                    var variantAlignment = 1u;
                    var variantFieldOffsets = new Dictionary<string, FieldSize>();
                    
                    foreach (var field in variant.Fields)
                    {
                        TypeSizeInfo fieldSize;
                        switch (field.Type)
                        {
                            case LoweredConcreteTypeReference concreteFieldType:
                            {
                                fieldSize = GetTypeSize(concreteFieldType, typeArguments);
                                break;
                            }
                            case LoweredGenericPlaceholder genericPlaceholder:
                            {
                                var index = dataType.TypeParameters.Index()
                                    .First(x => x.Item.PlaceholderName == genericPlaceholder.PlaceholderName).Index;
                                var typeArgument = concreteTypeReference.TypeArguments[index];
                                fieldSize = GetTypeSize(typeArgument, typeArguments);
                                break;
                            }
                            default:
                                throw new NotImplementedException(field.Type.GetType().ToString());
                        }

                        AlignInt(ref variantSize, fieldSize.Alignment);

                        variantFieldOffsets[field.Name] =
                            new FieldSize(variantSize, fieldSize.Size, fieldSize.Alignment);

                        variantSize += fieldSize.Size;
                        variantAlignment = Math.Max(variantAlignment, fieldSize.Alignment);
                    }

                    size = Math.Max(size, variantSize);
                    alignment = Math.Max(alignment, variantAlignment);

                    dataTypeFieldOffsets[variant.Name] = variantFieldOffsets;
                }
                
                break;
            }
            case LoweredPointer:
            {
                size = PointerSize;
                alignment = PointerSize;
                break;
            }
            case LoweredGenericPlaceholder placeholder:
            {
                var innerTypeSize = GetTypeSize(typeArguments[placeholder.PlaceholderName], typeArguments);
                _typeSizes.Add(KeyValuePair.Create(typeReference, innerTypeSize));
                return innerTypeSize;
            }
            case LoweredArray array:
            {
                var elementSize = GetTypeSize(array.ElementType, typeArguments);
                if (elementSize.Size % elementSize.Alignment != 0)
                {
                    throw new NotImplementedException();
                }

                var arrayTypeSize = new TypeSizeInfo(
                    elementSize.Size * array.Length,
                    array.Length == 0 ? 1 : elementSize.Alignment,
                    new Dictionary<string, Dictionary<string, FieldSize>>());
                
                _typeSizes.Add(KeyValuePair.Create(typeReference, arrayTypeSize));

                return arrayTypeSize;
            }
            default:
                throw new NotImplementedException(typeReference.GetType().ToString());
        }
        
        var typeSize = new TypeSizeInfo(size, alignment, dataTypeFieldOffsets);

        _typeSizes.Add(KeyValuePair.Create(typeReference, typeSize));

        return typeSize;
    }

    private void AlignInt(ref uint value, uint alignBy)
    {
        if (alignBy == 0)
            return;
        
        var mod = value % alignBy;
        if (mod == 0)
            return;

        value += alignBy - mod;
    }

    private void AllocateRegister(Register register)
    {
        if (!_registersInUse.Add(register))
        {
            throw new InvalidOperationException($"Register {register.ToAsm(PointerSize)} is already in use");
        }
    }
    
    private void FreeRegister(Register register)
    {
        _registersInUse.Remove(register);
    }

    private Register AllocateRegister()
    {
        var register = Register.GeneralPurposeRegisters.First(_registersInUse.Add);
        return register;
    }
    
    private void ProcessMethod(LoweredMethod method, IReadOnlyList<ILoweredTypeReference> typeArguments)
    {
        _codeSegment.AppendLine($"{GetMethodLabel(method, typeArguments)}:");
        
        _codeSegment.AppendLine("    push    rbp");
        _codeSegment.AppendLine("    mov     rbp, rsp");
        
        _currentTypeArguments = typeArguments.Zip(method.TypeParameters).ToDictionary(x => x.Second.PlaceholderName, x => x.First);
        _currentMethod = method;
        _methodCount++;
        _locals = [];
        var parameterStackOffset = PointerSize * 2; // start with offset by PointerSize * 2 because return address and rbp are on the top of the stack 

        var returnType = method.ReturnValue.Type;
        if (returnType is LoweredGenericPlaceholder{PlaceholderName: var placeholderName})
        {
            returnType = _currentTypeArguments[placeholderName];
        }
        
        var returnSize = GetTypeSize(returnType, _currentTypeArguments);

        var parameters = method.ParameterLocals.ToArray();
        
        if (returnSize.Size > MaxParameterSize)
        {
            parameters = parameters.Prepend(new MethodLocal(
                ReturnValueAddressLocal,
                null,
                new LoweredPointer(returnType))).ToArray();
        }

        for (var i = 0; i < Math.Min(parameters.Length, 4); i++)
        {
            var sourceRegister = i switch
            {
                0 => Register.C,
                1 => Register.D,
                2 => Register.R8,
                3 => Register.R9,
                _ => throw new UnreachableException(),
            };
            
            AllocateRegister(sourceRegister);
        }
        
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameterLocal = parameters[index];
            var parameterType = parameterLocal.Type;
            if (parameterType is LoweredGenericPlaceholder{PlaceholderName: var parameterPlaceholderName})
            {
                parameterType = _currentTypeArguments[parameterPlaceholderName];
            }
            var parameterSize = GetTypeSize(parameterType, _currentTypeArguments);
            var size = Math.Min(parameterSize.Size, PointerSize);
            
            AlignInt(ref parameterStackOffset, parameterSize.Alignment);

            var parameterOffset = (int)parameterStackOffset;

            var sourceRegister = index switch
            {
                0 => Register.C,
                1 => Register.D,
                2 => Register.R8,
                3 => Register.R9,
                _ => null
            };

            var rawParameterPlace = new OffsetFromBasePointer(parameterOffset);
            IAsmPlace parameterPlace = rawParameterPlace;
            if (parameterSize.Size > MaxParameterSize)
            {
                parameterPlace = new PointerTo(parameterPlace, 0);
            }
            
            _locals[parameterLocal.CompilerGivenName] = new LocalInfo(
                parameterPlace,
                parameterType);

            if (sourceRegister is not null)
            {
                StoreAsmPlaceInPlace(sourceRegister, rawParameterPlace, size);
                FreeRegister(sourceRegister);
            }
            
            // parameter offset is incremented after we store the parameter place (and maybe move out of register)
            // because parameters are at higher memory addresses on the stack rather than lower memory addresses
            parameterStackOffset += size;
        }

        var localStackOffset = 0u;

        IEnumerable<MethodLocal> locals = method.Locals;
        if (returnSize.Size > MaxParameterSize)
        {
            _locals[method.ReturnValue.CompilerGivenName] = new LocalInfo(
                new PointerTo(_locals[ReturnValueAddressLocal].Place, 0),
                returnType);
        }
        else
        {
            locals = locals.Append(method.ReturnValue);
        }
        
        foreach (var local in locals)
        {
            var localType = local.Type;
            if (localType is LoweredGenericPlaceholder{PlaceholderName: var localPlaceholderName})
            {
                localType = _currentTypeArguments[localPlaceholderName];
            }
            var typeSize = GetTypeSize(localType, _currentTypeArguments);

            // make sure stack offset is aligned to the size of the type 
            AlignInt(ref localStackOffset, typeSize.Alignment);
            
            // increment the stack offset before we associate the position with the local so that 
            // it points to the lowest memory address (top of the stack) as structures grow towards
            // higher memory addresses
            localStackOffset += typeSize.Size;

            var localPlace = new OffsetFromBasePointer(-(int)localStackOffset);
            _locals[local.CompilerGivenName] = new LocalInfo(localPlace, localType);

            _codeSegment.AppendLine(
                $"; {local.CompilerGivenName} ({typeSize.Size} byte{(typeSize.Size > 1 ? "s" : "")}" +
                $", alignment {typeSize.Alignment} byte{(typeSize.Alignment > 1 ? "s" : "")})" +
                $": rbp[-{localStackOffset}]");
        }
        
        // ensure stack space is 16 byte aligned
        AlignInt(ref localStackOffset, 16);
        _codeSegment.AppendLine("; Allocate stack space for local variables and parameters");
        _codeSegment.AppendLine($"    sub     rsp, {localStackOffset}");


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
                LoweredConcreteTypeReference concrete when DefId.SignedInts.Contains(concrete.DefinitionId) => IntSigned.Signed,
                LoweredConcreteTypeReference concrete when DefId.UnsignedInts.Contains(concrete.DefinitionId) => IntSigned.Unsigned,
                _ => null
            },
            UIntConstant => IntSigned.Unsigned,
            IntConstant => IntSigned.Signed,
            BoolConstant or FunctionPointerConstant or StringConstant or UnitConstant => null,
            _ => throw new ArgumentOutOfRangeException(nameof(operand))
        };
    }
    

    private ILoweredTypeReference GetPlaceType(IPlace place)
    {
        switch (place)
        {
            case Field field:
            {
                var ownerType = GetPlaceType(field.FieldOwner);
                var concreteOwner = ownerType as LoweredConcreteTypeReference;

                while (concreteOwner is null)
                {
                    ownerType = ownerType switch
                    {
                        LoweredConcreteTypeReference => ownerType,
                        LoweredFunctionReference => throw new InvalidOperationException("FunctionReference has no fields"),
                        LoweredGenericPlaceholder(_, var placeholderName) => _currentTypeArguments[placeholderName],
                        LoweredPointer(var pointerTo) => pointerTo,
                        RawPointer => throw new InvalidOperationException("Raw Pointer has no fields"),
                        _ => throw new ArgumentOutOfRangeException(nameof(ownerType))
                    };
                    
                    concreteOwner = ownerType as LoweredConcreteTypeReference;
                }
                
                var dataType = _dataTypes[concreteOwner.DefinitionId];
                var variant = dataType.Variants.First(x => x.Name == field.VariantName);

                return variant.Fields.First(x => x.Name == field.FieldName).Type;
            }
            case Local local:
                return _locals[local.LocalName].Type;
            case StaticField staticField:
                throw new NotImplementedException();
            case Deref deref:
            {
                var pointerType = (GetPlaceType(deref.PointerPlace) as LoweredPointer).NotNull();
                return pointerType.PointerTo;
            }
            case Index index:
            {
                var arrayType = (GetPlaceType(index.ArrayPlace) as LoweredArray).NotNull();
                return arrayType.ElementType;
            }
            default:
                throw new ArgumentOutOfRangeException(place.GetType().ToString());
        }
    }


    private void ProcessBinaryOperation(IAsmPlace destination, IOperand left, IOperand right, BinaryOperationKind kind)
    {
        var operandType = GetOperandType(left);
        var operandSize = GetTypeSize(operandType, _currentTypeArguments);
        
        var leftOperandRegister = AllocateRegister();
        var rightOperandRegister = AllocateRegister();
        
        var leftOperandRegisterAsm = leftOperandRegister.ToAsm(operandSize.Size);
        var rightOperandRegisterAsm = rightOperandRegister.ToAsm(operandSize.Size);
        
        switch (kind)
        {
            case BinaryOperationKind.Add:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                _codeSegment.AppendLine($"    add     {leftOperandRegisterAsm}, {rightOperandRegisterAsm}");
                StoreAsmPlaceInPlace(Register.A, destination, operandSize.Size);
                break;
            }
            case BinaryOperationKind.Subtract:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                _codeSegment.AppendLine($"    sub     {leftOperandRegisterAsm}, {rightOperandRegisterAsm}");
                StoreAsmPlaceInPlace(leftOperandRegister, destination, operandSize.Size);
                break;
            }
            case BinaryOperationKind.Multiply:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                var intSigned = GetIntSigned(left).NotNull();
                
                // imul with one operand implicitly goes into the a register (rax or al),
                // and the destination is in the a register too
                _codeSegment.AppendLine(intSigned == IntSigned.Signed
                    ? $"    imul    {rightOperandRegisterAsm}"
                    : $"    mul     {rightOperandRegisterAsm}");

                StoreAsmPlaceInPlace(leftOperandRegister, destination, operandSize.Size);
                break;
            }
            case BinaryOperationKind.Divide:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                var intSigned = GetIntSigned(left).NotNull();
                _codeSegment.AppendLine("    cqo");
                _codeSegment.AppendLine(intSigned == IntSigned.Signed
                    ? $"    idiv     {rightOperandRegisterAsm}"
                    : $"    div     {rightOperandRegisterAsm}");

                StoreAsmPlaceInPlace(leftOperandRegister, destination, operandSize.Size);
                break;
            }
            case BinaryOperationKind.LessThan:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                _codeSegment.AppendLine($"    cmp     {leftOperandRegisterAsm}, {rightOperandRegisterAsm}");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 10000000b"); // sign flag
                _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 7");
                StoreAsmPlaceInPlace(leftOperandRegister, destination, 1);
                break;
            }
            case BinaryOperationKind.LessThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.GreaterThan:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                _codeSegment.AppendLine($"    cmp     {rightOperandRegisterAsm}, {leftOperandRegisterAsm}");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 10000000b"); // sign flag
                _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 7");
                StoreAsmPlaceInPlace(leftOperandRegister, destination, 1);
                break;
            }
            case BinaryOperationKind.GreaterThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.Equal:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                _codeSegment.AppendLine($"    cmp     {leftOperandRegisterAsm}, {rightOperandRegisterAsm}");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 1000000b"); // zero flag
                _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 6");
                StoreAsmPlaceInPlace(leftOperandRegister, destination, 1);
                break;
            }
            case BinaryOperationKind.NotEqual:
            {
                MoveOperandToDestination(left, leftOperandRegister);
                MoveOperandToDestination(right, rightOperandRegister);
                _codeSegment.AppendLine($"    cmp     {leftOperandRegisterAsm}, {rightOperandRegisterAsm}");
                _codeSegment.AppendLine("    pushf");
                _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 1000000b"); // zero flag
                _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 6");
                _codeSegment.AppendLine($"    btc     {leftOperandRegister.ToAsm(PointerSize)}, 0");
                StoreAsmPlaceInPlace(leftOperandRegister, destination, 1);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        FreeRegister(leftOperandRegister);
        FreeRegister(rightOperandRegister);
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
            {
                var size = GetTypeSize(createObject.Type, _currentTypeArguments);
                FillMemory(place, "0x0", size.Size);
                
                break;
            }
            case CreateArray createArray:
            {
                var size = GetTypeSize(createArray.Array, _currentTypeArguments);
                if (size.Size > 0)
                {
                    FillMemory(place, "0x0", size.Size);
                }

                break;
            }
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
                throw new ArgumentOutOfRangeException(rValue.GetType().ToString());
        }
    }

    private void ProcessUnaryOperation(IAsmPlace place, IOperand operand, UnaryOperationKind kind)
    {
        var operandType = GetOperandType(operand);
        var operandSize = GetTypeSize(operandType, _currentTypeArguments);
        
        switch (kind)
        {
            case UnaryOperationKind.Not:
            {
                var operandRegister = AllocateRegister();
                MoveOperandToDestination(operand, operandRegister);
                
                // btc instruction must be performed on 16 bit registers or greater
                var registerSize = operandRegister.ToAsm(Math.Max(operandSize.Size, 2));
                _codeSegment.AppendLine($"    btc     {registerSize}, 0");
                StoreAsmPlaceInPlace(operandRegister, place, operandSize.Size);
                FreeRegister(operandRegister);
                break;
            }
            case UnaryOperationKind.Negate:
            {
                var operandRegister = AllocateRegister();
                MoveOperandToDestination(operand, operandRegister);
                
                _codeSegment.AppendLine($"    neg     {operandRegister.ToAsm(operandSize.Size)}");
                StoreAsmPlaceInPlace(operandRegister, place, operandSize.Size);

                FreeRegister(operandRegister);
                break;
            }
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
                var returnType = _currentMethod.NotNull().ReturnValue.Type;
                if (returnType is LoweredGenericPlaceholder{PlaceholderName: var placeholderName })
                {
                    returnType = _currentTypeArguments[placeholderName];
                }
                var returnSize = GetTypeSize(returnType, _currentTypeArguments);
                var returnValuePlace = PlaceToAsmPlace(new Local(_currentMethod.NotNull().ReturnValue.CompilerGivenName));
                
                AllocateRegister(Register.A);
                if (returnSize.Size > MaxParameterSize)
                {
                    // copy the pointer to the return value into the "a" register
                    StoreAsmPlaceInPlace(
                        _locals[ReturnValueAddressLocal].Place,
                        Register.A,
                        PointerSize);
                }
                else
                {
                    StoreAsmPlaceInPlace(
                        returnValuePlace, 
                        Register.A,
                        returnSize.Size);
                }
                
                _codeSegment.AppendLine("    leave");
                _codeSegment.AppendLine("    ret");
                FreeRegister(Register.A);
                break;
            }
            case SwitchInt switchInt:
            {
                var register = AllocateRegister();
                MoveOperandToDestination(switchInt.Operand, register);
                var operandType = GetOperandType(switchInt.Operand);
                var operandSize = GetTypeSize(operandType, _currentTypeArguments);
                foreach (var (intCase, jumpTo) in switchInt.Cases)
                {
                    _codeSegment.AppendLine($"    cmp     {register.ToAsm(operandSize.Size)}, {intCase}");
                    _codeSegment.AppendLine($"    je      {GetBasicBlockLabel(jumpTo)}");
                }

                _codeSegment.AppendLine($"    jmp     {GetBasicBlockLabel(switchInt.Otherwise)}");
                FreeRegister(register);
                break;
            }
            case Assert assert:
            {
                // TODO: actually assert
                ProcessTerminator(new GoTo(assert.GoTo));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(terminator));
        }
    }

    private ILoweredTypeReference GetOperandType(IOperand operand)
    {
        switch (operand)
        {
            case BoolConstant boolConstant:
            {
                return new LoweredConcreteTypeReference(
                    TypeChecker.InstantiatedClass.Boolean.Signature.Name,
                    DefId.Boolean,
                    []);
            }
            case Copy copy:
                return GetPlaceType(copy.Place);
            case FunctionPointerConstant functionPointerConstant:
                return new RawPointer();
            case IntConstant intConstant:
            {
                return intConstant.ByteSize switch
                {
                    1 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.Int8.Signature.Name, DefId.Int8, []),
                    2 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.Int16.Signature.Name, DefId.Int16, []),
                    4 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.Int32.Signature.Name, DefId.Int32, []),
                    8 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.Int64.Signature.Name, DefId.Int64, []),
                    _ => throw new UnreachableException()
                };
            }
            case UIntConstant intConstant:
            {
                return intConstant.ByteSize switch
                {
                    1 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.UInt8.Signature.Name, DefId.UInt8, []),
                    2 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.UInt16.Signature.Name, DefId.UInt16, []),
                    4 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.UInt32.Signature.Name, DefId.UInt32, []),
                    8 => new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.UInt64.Signature.Name, DefId.UInt64, []),
                    _ => throw new UnreachableException()
                };
            }
            case SizeOf sizeOf:
                return new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.UInt64.Signature.Name, DefId.UInt64, []);
            case StringConstant stringConstant:
                return new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.String.Signature.Name, DefId.String, []);
            case UnitConstant unitConstant:
                return new LoweredConcreteTypeReference(TypeChecker.InstantiatedClass.Unit.Signature.Name, DefId.Unit, []);
            default:
                throw new ArgumentOutOfRangeException(nameof(operand));
        }
    }

    private void ProcessMethodCall(MethodCall methodCall)
    {
        var arity = methodCall.Arguments.Count;
        
        _codeSegment.AppendLine($"; MethodCall({arity})");

        var calleeMethod = GetMethod(methodCall.Function.DefinitionId).NotNull();
        
        IReadOnlyList<ILoweredTypeReference> calleeTypeArguments =
        [
            ..methodCall.Function.TypeArguments.Select(ILoweredTypeReference (x) =>
            {
                switch (x)
                {
                    case LoweredConcreteTypeReference concreteArgument:
                        return concreteArgument;
                    case LoweredGenericPlaceholder genericReefTypeReference:
                    {
                        Debug.Assert(genericReefTypeReference.OwnerDefinitionId == _currentMethod.NotNull().Id);
                        return _currentTypeArguments[genericReefTypeReference.PlaceholderName];
                    }
                    case LoweredPointer loweredPointer:
                    {
                        return loweredPointer;
                    }
                    default:
                        throw new NotImplementedException(x.GetType().ToString());
                }
            })
        ];
        
        var calleeTypeArgumentsDictionary = calleeMethod.TypeParameters.Index()
            .ToDictionary(x => x.Item.PlaceholderName, x => calleeTypeArguments[x.Index]);
        
        if (calleeMethod is LoweredMethod loweredMethod)
        {
            TryEnqueueMethodForProcessing(loweredMethod, calleeTypeArguments);
        }

        var functionLabel = GetMethodLabel(calleeMethod, calleeTypeArguments);

        var returnType = calleeMethod.ReturnValue.Type;
        if (returnType is LoweredGenericPlaceholder genericReturnType)
        {
            returnType = calleeTypeArgumentsDictionary[genericReturnType.PlaceholderName];
        }
        var returnSize = GetTypeSize(returnType, calleeTypeArgumentsDictionary);
        
        var argumentTypesEnumerable = methodCall.Arguments.Select(x => (GetOperandType(x), x));
        
        var destination = PlaceToAsmPlace(methodCall.PlaceDestination);

        if (returnSize.Size > MaxParameterSize)
        {
            argumentTypesEnumerable = 
                argumentTypesEnumerable.Prepend((new LoweredPointer(returnType), new AddressOf(methodCall.PlaceDestination)));
            arity += 1;
        }
        
        var argumentTypes = argumentTypesEnumerable.ToArray();
        
        
        var parametersSpaceNeeded = (uint)Math.Max((argumentTypes.Length - 4) * MaxParameterSize, 0) + 32;
        
        // move first four arguments into registers as specified by win 64 calling convention, then
        // shift the remaining arguments up by four so that the 'top' 32 bytes are free and can act as
        // the callee's shadow space 

        var largeParametersSpace = 0u;

        var parameterPointers = new Dictionary<int, int>();
        
        for (var i = arity - 1; i >= 0; i--)
        {
            var (argumentType, argument) = argumentTypes[i];

            var size = GetTypeSize(argumentType, _currentTypeArguments);

            if (size.Size > MaxParameterSize)
            {
                AlignInt(ref largeParametersSpace, size.Alignment);
                largeParametersSpace += size.Size;
                parameterPointers[i] = -(int)largeParametersSpace;
                _codeSegment.AppendLine($"; Parameter {i} ({size} bytes) at offset {-largeParametersSpace}");
                MoveOperandToDestination(argument.NotNull(), new OffsetFromStackPointer(-(int)largeParametersSpace));
            }
        }

        parametersSpaceNeeded += largeParametersSpace;
        
        AlignInt(ref parametersSpaceNeeded, 16);

        _codeSegment.AppendLine($"; LargeParameterSpace: {largeParametersSpace} bytes, ArgumentStackSpace: {parametersSpaceNeeded - largeParametersSpace}");
        _codeSegment.AppendLine($"    sub     rsp, {parametersSpaceNeeded}");
        
        for (var i = arity - 1; i >= 0; i--)
        {
            IAsmPlace argumentDestination = i switch
            {
                0 => Register.C,
                1 => Register.D,
                2 => Register.R8,
                3 => Register.R9,
                _ => new OffsetFromStackPointer(i * (int)MaxParameterSize)
            };

            var (type, argument) = argumentTypes[i];
            
            var size = GetTypeSize(type, _currentTypeArguments);


            if (argumentDestination is Register register)
            {
                AllocateRegister(register);
            }

            if (size.Size <= MaxParameterSize)
            {
                MoveOperandToDestination(argument, argumentDestination);
            }
            else
            {
                var offsetFromStackPointer = parameterPointers[i] + (int)parametersSpaceNeeded;
                
                StorePlaceAddress(argumentDestination, $"[rsp{FormatOffset(offsetFromStackPointer)}]");
            }
        }
        
        _codeSegment.AppendLine($"    call    {functionLabel}");
        
        FreeRegister(Register.C);
        FreeRegister(Register.D);
        FreeRegister(Register.R8);
        FreeRegister(Register.R9);

        // move rsp back to where it was before we called the function
        _codeSegment.AppendLine($"    add     rsp, {parametersSpaceNeeded}");

        AllocateRegister(Register.A);
        IAsmPlace returnSource = Register.A;
        
        if (returnSize.Size > MaxParameterSize)
        {
            returnSource = new PointerTo(returnSource, 0);
        }

        StoreAsmPlaceInPlace(
            returnSource,
            destination,
            returnSize.Size);
        
        FreeRegister(Register.A);

        _codeSegment.AppendLine($"    jmp     {GetBasicBlockLabel(methodCall.GoToAfter)}");
    }

    private string GetBasicBlockLabel(BasicBlockId basicBlockId)
    {
        return $"{basicBlockId.Id}_{_methodCount}";
    }

    private interface IAsmPlace;

    private sealed class Register : IAsmPlace
    {
        public static readonly Register A = new ("a", false);
        public static readonly Register B = new ("b", false);
        public static readonly Register C = new ("c", false);
        public static readonly Register D = new ("d", false);
        public static readonly Register Source = new ("rsi", false);
        public static readonly Register Destination = new ("rdi", false);
        
        public static readonly Register R8 = new ("r8", true);
        public static readonly Register R9 = new ("r9", true);
        public static readonly Register R10 = new ("r10", true);
        public static readonly Register R11 = new ("r11", true);
        public static readonly Register R12 = new ("r12", true);
        public static readonly Register R13 = new ("r13", true);
        public static readonly Register R14 = new ("r14", true);
        public static readonly Register R15 = new ("r15", true);

        public static readonly IReadOnlyList<Register> GeneralPurposeRegisters = [
            A,
            B,
            C,
            D,
            R8,
            R9,
            R10,
            R11,
            R12,
            R13,
            R14,
            R15,
        ];
        
        private Register(string name, bool isNumberRegister)
        {
            Name = name;
            IsNumberRegister = isNumberRegister;
        } 
        
        private string Name { get; }
        private bool IsNumberRegister { get; }

        public string ToAsm(uint size)
        {
            if (Name is "rsi" or "rdi")
            {
                return Name;
            }
            
            return (size, IsNumberRegister) switch
            {
                (1, false) => $"{Name}l",
                (2, false) => $"{Name}x",
                (4, false) => $"e{Name}x",
                (8, false) => $"r{Name}x",
                (1, true) => $"{Name}b",
                (2, true) => $"{Name}w",
                (4, true) => $"{Name}d",
                (8, true) => Name,
                _ => throw new InvalidOperationException(size.ToString())
            };
        }
    } 
    
    private record PointerTo(IAsmPlace PointerPlace, int Offset) : IAsmPlace;
    private record OffsetFromBasePointer(int Offset) : IAsmPlace;
    private record OffsetFromStackPointer(int Offset) : IAsmPlace;

    private void MoveOperandToDestination(IOperand operand, IAsmPlace destination)
    {
        switch (operand)
        {
            case BoolConstant boolConstant:
                MoveIntoPlace(destination, boolConstant.Value ? "1" : "0", 1);
                break;
            case Copy copy:
            {
                var operandType = GetOperandType(operand);
                var size = GetTypeSize(operandType, _currentTypeArguments);
                StoreAsmPlaceInPlace(PlaceToAsmPlace(copy.Place), destination, size.Size);
                break;
            }
            case FunctionPointerConstant functionPointerConstant:
                throw new NotImplementedException();
            case IntConstant intConstant:
            {
                MoveIntoPlace(destination, $"0x{intConstant.Value:X}", intConstant.ByteSize);
                break;
            }
            case StringConstant stringConstant:
            {
                if (!_strings.TryGetValue(stringConstant.Value, out var stringName))
                {
                    stringName = $"_str_{_strings.Count}";
                    _strings[stringConstant.Value] = stringName;
                    // todo: no null terminated strings
                    var str = stringConstant.Value.AsSpan();
                    _dataSegment.Append($"    {stringName} db ");
                    
                    // https://www.ascii-code.com/characters/white-space-characters
                    var whitespaceIndex = str.IndexOfAnyInRange((char)9, (char)13);

                    var started = false;
                    var inStringLiteral = false;
                    while (whitespaceIndex >= 0)
                    {
                        var segment = str[..whitespaceIndex];
                        if (segment.Length > 0)
                        {
                            if (started)
                            {
                                _dataSegment.Append(", ");
                            }
                            if (!inStringLiteral)
                            {
                                _dataSegment.Append("\"");
                            }
                            _dataSegment.Append(segment);
                            started = true;
                            inStringLiteral = true;
                        }
                        if (inStringLiteral)
                        {
                            _dataSegment.Append("\"");
                            inStringLiteral = false;
                        }
                        if (started)
                        {
                            _dataSegment.Append(", ");
                        }
                        _dataSegment.Append((int)str[whitespaceIndex]);
                        str = str[(whitespaceIndex + 1)..];
                        whitespaceIndex = str.IndexOfAnyInRange((char)9, (char)13);
                        started = true;
                    }

                    if (str.Length > 0)
                    {
                        if (started)
                        {
                            _dataSegment.Append(", ");
                        }
                        if (!inStringLiteral)
                        {
                            _dataSegment.Append("\"");
                        }
                        _dataSegment.Append(str);
                        inStringLiteral = true;
                        started = true;
                    }
                    
                    if (inStringLiteral)
                    {
                        _dataSegment.Append("\"");
                    }
                    if (started)
                    {
                        _dataSegment.Append(", ");
                    }
                    _dataSegment.AppendLine("0");
                }

                MoveOperandToDestination(
                    new UIntConstant((ulong)stringConstant.Value.Length, (int)PointerSize),
                    destination);
                
                IAsmPlace stringAddressDestination = destination switch
                {
                    OffsetFromBasePointer offsetFromBasePointer => new OffsetFromBasePointer(
                        offsetFromBasePointer.Offset + (int)PointerSize),
                    OffsetFromStackPointer offsetFromStackPointer => new OffsetFromStackPointer(
                        offsetFromStackPointer.Offset + (int)PointerSize),
                    PointerTo pointerTo => pointerTo with {Offset = pointerTo.Offset + (int)PointerSize},
                    _ => throw new InvalidOperationException($"string must be on the stack for now: {destination.GetType()}")
                };

                StorePlaceAddress(stringAddressDestination, $"[{stringName}]");
                break;
            }
            case UIntConstant uIntConstant:
                MoveIntoPlace(destination, $"0x{uIntConstant.Value:X}", uIntConstant.ByteSize);
                break;
            case UnitConstant unitConstant:
                throw new NotImplementedException();
            case AddressOf(var place):
            {
                var asmPlace = PlaceToAsmPlace(place);

                StorePlaceAddress(destination, asmPlace);
                break;
            }
            case SizeOf(var sizeOfType):
            {
                var size = GetTypeSize(sizeOfType, _currentTypeArguments);
                MoveIntoPlace(destination, $"0x{size.Size:X}", PointerSize);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(operand), operand.GetType().ToString());
        }
    }

    private void StorePlaceAddress(IAsmPlace destination, IAsmPlace source)
    {
        switch (source)
        {
            case OffsetFromBasePointer(var offset):
                StorePlaceAddress(destination, $"[rbp{FormatOffset(offset)}]");
                break;
            case OffsetFromStackPointer(var offset):
                StorePlaceAddress(destination, $"[rsp{FormatOffset(offset)}]");
                break;
            case PointerTo pointerTo:
                switch (pointerTo.PointerPlace)
                {
                    case OffsetFromBasePointer offsetFromBasePointer:
                    {
                        if (pointerTo.Offset != 0)
                        {
                            var register = AllocateRegister();
                            MoveIntoPlace(register, offsetFromBasePointer, PointerSize);
                            _codeSegment.AppendLine($"    add     {register.ToAsm(PointerSize)}, 0x{pointerTo.Offset:x}");
                            MoveIntoPlace(destination, register, PointerSize);
                            FreeRegister(register);
                        }
                        else
                        {
                            MoveIntoPlace(destination, offsetFromBasePointer, PointerSize);
                        }
                        break;
                    }
                    case OffsetFromStackPointer offsetFromStackPointer:
                    {
                        if (pointerTo.Offset != 0)
                        {
                            var register = AllocateRegister();
                            MoveIntoPlace(register, offsetFromStackPointer, PointerSize);
                            _codeSegment.AppendLine($"    add     {register.ToAsm(PointerSize)}, 0x{pointerTo.Offset:x}");
                            MoveIntoPlace(destination, register, PointerSize);
                            FreeRegister(register);
                        }
                        else
                        {
                            MoveIntoPlace(destination, offsetFromStackPointer, PointerSize);
                        }
                        break;
                    }
                    case PointerTo:
                        throw new InvalidOperationException();
                    case Register register:
                    {
                        if (pointerTo.Offset != 0)
                        {
                            _codeSegment.AppendLine($"    add     {register.ToAsm(PointerSize)}, 0x{pointerTo.Offset:x}");
                        }
                        MoveIntoPlace(destination, register, PointerSize);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                break;
            case Register:
                throw new InvalidOperationException("Register has no address");
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private void StorePlaceAddress(IAsmPlace place, string operand)
    {
        if (place is Register register)
        {
            _codeSegment.AppendLine($"    lea     {register.ToAsm(PointerSize)}, {operand}");
            return;
        }

        register = AllocateRegister();
        _codeSegment.AppendLine($"    lea     {register.ToAsm(PointerSize)}, {operand}");
        StoreAsmPlaceInPlace(register, place, PointerSize);
        FreeRegister(register);
    }

    private void MoveIntoPlace(IAsmPlace place, Register register, uint size)
    {
        switch (place)
        {
            case OffsetFromBasePointer(var offset):
            {
                _codeSegment.AppendLine($"    mov     [rbp{FormatOffset(offset)}], {register.ToAsm(size)}");
                break;
            }
            case OffsetFromStackPointer(var offset):
                _codeSegment.AppendLine($"    mov     [rsp{FormatOffset(offset)}], {register.ToAsm(size)}");
                break;
            case PointerTo pointerTo:
            {
                var destinationRegister = AllocateRegister();
                StorePlaceAddress(destinationRegister, pointerTo);
                _codeSegment.AppendLine($"    mov     [{destinationRegister.ToAsm(PointerSize)}], {register.ToAsm(size)}");
                FreeRegister(destinationRegister);
                break;
            }
            case Register destinationRegister when destinationRegister == register:
                throw new InvalidOperationException();
            case Register destinationRegister:
                _codeSegment.AppendLine($"    mov     {destinationRegister.ToAsm(size)}, {register.ToAsm(size)}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(place));
        }

    }

    private void MoveIntoPlace(IAsmPlace place, OffsetFromBasePointer offsetFromBasePointer, uint size)
    {
        if (size > PointerSize)
        {
            AllocateRegister(Register.C);
            AllocateRegister(Register.Source);
            AllocateRegister(Register.Destination);
            
            MoveIntoPlace(Register.C, $"0x{size:x}", PointerSize);
            StorePlaceAddress(Register.Source, offsetFromBasePointer);
            StorePlaceAddress(Register.Destination, place);
            _codeSegment.AppendLine("    rep movsb");
            
            FreeRegister(Register.C);
            FreeRegister(Register.Source);
            FreeRegister(Register.Destination);
            
            return;
        }
        var sizeSpecifier = SizeSpecifiers[size];
        switch (place)
        {
            case OffsetFromBasePointer(var offset):
            {
                var register = AllocateRegister();
                MoveIntoPlace(register, offsetFromBasePointer, size);
                _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rbp{FormatOffset(offset)}], {register.ToAsm(size)}");
                FreeRegister(register);
                break;
            }
            case OffsetFromStackPointer(var offset):
            {
                var register = AllocateRegister();
                MoveIntoPlace(register, offsetFromBasePointer, size);
                _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rbp{FormatOffset(offset)}], {register.ToAsm(size)}");
                FreeRegister(register);
                break;
            }
            case PointerTo(var pointerPlace, var offset):
            {
                switch (pointerPlace)
                {
                    case OffsetFromBasePointer offsetFromBasePointer1:
                    {
                        var valueRegister = AllocateRegister();
                        var pointerRegister = AllocateRegister();
                        MoveIntoPlace(valueRegister, offsetFromBasePointer, size);
                        MoveIntoPlace(pointerRegister, offsetFromBasePointer1, PointerSize);
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{pointerRegister.ToAsm(PointerSize)}{FormatOffset(offset)}], {valueRegister.ToAsm(size)}");
                        FreeRegister(valueRegister);
                        FreeRegister(pointerRegister);
                        break;
                    }
                    case OffsetFromStackPointer offsetFromStackPointer:
                    {
                        var valueRegister = AllocateRegister();
                        var pointerRegister = AllocateRegister();
                        MoveIntoPlace(valueRegister, offsetFromBasePointer, size);
                        MoveIntoPlace(pointerRegister, offsetFromStackPointer, size);
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{pointerRegister.ToAsm(PointerSize)}{FormatOffset(offset)}], {valueRegister.ToAsm(size)}");
                        FreeRegister(valueRegister);
                        FreeRegister(pointerRegister);
                        break;
                    }
                    case PointerTo:
                        throw new InvalidOperationException();
                    case Register pointerRegister:
                    {
                        var valueRegister = AllocateRegister();
                        MoveIntoPlace(valueRegister, offsetFromBasePointer, size);
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{pointerRegister.ToAsm(PointerSize)}{FormatOffset(offset)}], {valueRegister.ToAsm(size)}");
                        FreeRegister(valueRegister);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(pointerPlace));
                }
                break;
            }
            case Register register:
            {
                _codeSegment.AppendLine($"    mov     {register.ToAsm(size)}, [rbp{FormatOffset(offsetFromBasePointer.Offset)}]");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(place));
        }

    }

    private void MoveIntoPlace(IAsmPlace destination, PointerTo source, uint size)
    {
        if (size > PointerSize)
        {
            AllocateRegister(Register.C);
            AllocateRegister(Register.Source);
            AllocateRegister(Register.Destination);
            
            MoveIntoPlace(Register.C, $"0x{size:x}", PointerSize);
            StorePlaceAddress(Register.Source, source);
            StorePlaceAddress(Register.Destination, destination);
            _codeSegment.AppendLine("    rep movsb");
            
            FreeRegister(Register.C);
            FreeRegister(Register.Source);
            FreeRegister(Register.Destination);
            
            return;
        }

        switch (source.PointerPlace)
        {
            case OffsetFromBasePointer offsetFromBasePointer:
            {
                var sourcePointerRegister = AllocateRegister();
                var valueRegister = AllocateRegister();
                MoveIntoPlace(sourcePointerRegister, offsetFromBasePointer, PointerSize);
                _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{sourcePointerRegister.ToAsm(PointerSize)}{FormatOffset(source.Offset)}]");
                MoveIntoPlace(destination, valueRegister, size);
                
                FreeRegister(sourcePointerRegister);
                FreeRegister(valueRegister);
                break;
            }
            case OffsetFromStackPointer offsetFromStackPointer:
            {
                var sourcePointerRegister = AllocateRegister();
                var valueRegister = AllocateRegister();
                MoveIntoPlace(sourcePointerRegister, offsetFromStackPointer, PointerSize);
                _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{sourcePointerRegister.ToAsm(PointerSize)}{FormatOffset(source.Offset)}]");
                MoveIntoPlace(destination, valueRegister, size);
                
                FreeRegister(sourcePointerRegister);
                FreeRegister(valueRegister);
                break;
            }
            case PointerTo:
                throw new InvalidOperationException();
            case Register register:
            {
                var valueRegister = AllocateRegister();
                _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{register.ToAsm(PointerSize)}{FormatOffset(source.Offset)}]");
                MoveIntoPlace(destination, valueRegister, size);
                FreeRegister(valueRegister);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void MoveIntoPlace(IAsmPlace destination, OffsetFromStackPointer offsetFromStackPointer, uint size)
    {
        if (size > PointerSize)
        {
            AllocateRegister(Register.C);
            AllocateRegister(Register.Source);
            AllocateRegister(Register.Destination);
            
            MoveIntoPlace(Register.C, $"0x{size:x}", PointerSize);
            StorePlaceAddress(Register.Source, offsetFromStackPointer);
            StorePlaceAddress(Register.Destination, destination);
            _codeSegment.AppendLine("    rep movsb");
            
            FreeRegister(Register.C);
            FreeRegister(Register.Source);
            FreeRegister(Register.Destination);
            return;
        }
        var sizeSpecifier = SizeSpecifiers[size];
        switch (destination)
        {
            case OffsetFromBasePointer(var offset):
            {
                var valueRegister = AllocateRegister();
                
                MoveIntoPlace(valueRegister, offsetFromStackPointer, size);
                _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rsp{FormatOffset(offset)}], {valueRegister.ToAsm(size)}");
                FreeRegister(valueRegister);
                break;
            }
            case OffsetFromStackPointer(var offset):
            {
                var valueRegister = AllocateRegister();
                MoveIntoPlace(valueRegister, offsetFromStackPointer, size);
                _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rsp{FormatOffset(offset)}], {valueRegister.ToAsm(size)}");
                FreeRegister(valueRegister);
                break;
            }
            case PointerTo(var pointerPlace, var offset):
            {
                switch (pointerPlace)
                {
                    case OffsetFromBasePointer offsetFromBasePointer:
                    {
                        var valueRegister = AllocateRegister();
                        var sourcePointerRegister = AllocateRegister();
                        
                        MoveIntoPlace(sourcePointerRegister, offsetFromBasePointer, PointerSize);
                        _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{sourcePointerRegister.ToAsm(PointerSize)}{FormatOffset(offset)}]");
                        MoveIntoPlace(destination, valueRegister, size);
                        
                        FreeRegister(valueRegister);
                        FreeRegister(sourcePointerRegister);
                        break;
                    }
                    case OffsetFromStackPointer offsetFromStackPointer1:
                    {
                        var valueRegister = AllocateRegister();
                        var sourcePointerRegister = AllocateRegister();
                        
                        MoveIntoPlace(sourcePointerRegister, offsetFromStackPointer1, PointerSize);
                        _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{sourcePointerRegister.ToAsm(PointerSize)}{FormatOffset(offset)}]");
                        MoveIntoPlace(destination, valueRegister, size);

                        FreeRegister(valueRegister);
                        FreeRegister(sourcePointerRegister);
                        break;
                    }
                    case PointerTo:
                        throw new InvalidOperationException();
                    case Register register:
                    {
                        var valueRegister = AllocateRegister();
                        
                        _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{register.ToAsm(PointerSize)}{FormatOffset(offset)}]");
                        MoveIntoPlace(destination, valueRegister, size);
                        
                        FreeRegister(valueRegister);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(pointerPlace));
                }
                break;
            }
            case Register register:
            {
                _codeSegment.AppendLine($"    mov     {register.ToAsm(size)}, [rsp{FormatOffset(offsetFromStackPointer.Offset)}]");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(destination));
        }

    }
    
    private void FillMemory(IAsmPlace place, string constantValue, uint size)
    {
        if (size < PointerSize)
        {
            MoveIntoPlace(place, constantValue, size);
            return;
        }
        
        if (size % PointerSize != 0)
        {
            throw new NotImplementedException();
        }
        
        var sizeSpecifier = SizeSpecifiers[PointerSize];
        
        switch (place)
        {
            case OffsetFromBasePointer(var offset):
            {
                for (var i = 0; i < size; i += (int)PointerSize)
                {
                    _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rbp{FormatOffset(offset + i)}], {constantValue}");
                }
                break;
            }
            case OffsetFromStackPointer(var offset):
            {
                for (var i = 0; i < size; i += (int)PointerSize)
                {
                    _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rsp{FormatOffset(offset + i)}], {constantValue}");
                }
                break;
            }
            case PointerTo(var pointerPlace, var offset):
            {
                switch (pointerPlace)
                {
                    case OffsetFromBasePointer offsetFromBasePointer:
                    {
                        var register = AllocateRegister();

                        _codeSegment.AppendLine($"    mov     {register.ToAsm(PointerSize)}, [rbp{FormatOffset(offsetFromBasePointer.Offset)}]");
                        for (var i = 0; i < size; i += (int)PointerSize)
                        {
                            _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{register.ToAsm(PointerSize)}{FormatOffset(offset + i)}], {constantValue}");
                        }
                        
                        FreeRegister(register);
                        break;
                    }
                    case OffsetFromStackPointer offsetFromStackPointer:
                    {
                        var register = AllocateRegister();
                        
                        _codeSegment.AppendLine($"    mov     {register.ToAsm(PointerSize)}, [rsp{FormatOffset(offsetFromStackPointer.Offset)}]");

                        for (var i = 0; i < size; i += (int)PointerSize)
                        {
                            _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{register.ToAsm(PointerSize)}{FormatOffset(offset + i)}], {constantValue}");
                        }

                        FreeRegister(register);
                        break;
                    }
                    case PointerTo:
                        throw new InvalidOperationException();
                    case Register register:
                        for (var i = 0; i < size; i += (int)PointerSize)
                        {
                            _codeSegment.AppendLine($"    mov     [{register.ToAsm(PointerSize)}{FormatOffset(offset + i)}], {constantValue}");
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(pointerPlace));
                }
                break;
            }
            case Register:
            {
                throw new InvalidOperationException("Cannot move more than 8 bytes into a register");
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(place));
        }
    }

    private void MoveIntoPlace(IAsmPlace place, string constantValue, uint size)
    {
        var sizeSpecifier = SizeSpecifiers[size];
        switch (place)
        {
            case OffsetFromBasePointer(var offset):
                _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rbp{FormatOffset(offset)}], {constantValue}");
                break;
            case OffsetFromStackPointer(var offset):
                _codeSegment.AppendLine($"    mov     {sizeSpecifier} [rsp{FormatOffset(offset)}], {constantValue}");
                break;
            case PointerTo(var pointerPlace, var offset):
            {
                switch (pointerPlace)
                {
                    case OffsetFromBasePointer offsetFromBasePointer:
                    {
                        var register = AllocateRegister();
                        
                        _codeSegment.AppendLine($"    mov     {register.ToAsm(PointerSize)}, [rbp{FormatOffset(offsetFromBasePointer.Offset)}]");
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{register.ToAsm(PointerSize)}{FormatOffset(offset)}], {constantValue}");
                        FreeRegister(register);
                        break;
                    }
                    case OffsetFromStackPointer offsetFromStackPointer:
                    {
                        var register = AllocateRegister();
                        
                        _codeSegment.AppendLine($"    mov     {register.ToAsm(PointerSize)}, [rsp{FormatOffset(offsetFromStackPointer.Offset)}]");
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{register.ToAsm(PointerSize)}{FormatOffset(offset)}], {constantValue}");

                        FreeRegister(register);
                        break;
                    }
                    case PointerTo:
                        throw new InvalidOperationException();
                    case Register register:
                        _codeSegment.AppendLine($"    mov     [{register.ToAsm(PointerSize)}{FormatOffset(offset)}], {constantValue}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(pointerPlace));
                }
                break;
            }
            case Register register:
            {
                _codeSegment.AppendLine($"    mov     {register.ToAsm(size)}, {constantValue}");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(place));
        }
    }

    private void StoreAsmPlaceInPlace(IAsmPlace source, IAsmPlace destination, uint size)
    {
        switch (source)
        {
            case OffsetFromBasePointer offsetFromBasePointer:
                MoveIntoPlace(destination, offsetFromBasePointer, size);
                break;
            case OffsetFromStackPointer offsetFromStackPointer:
                MoveIntoPlace(destination, offsetFromStackPointer, size);
                break;
            case PointerTo pointerTo:
                MoveIntoPlace(destination, pointerTo, size);
                break;
            case Register register:
                MoveIntoPlace(destination, register, size);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static readonly Dictionary<uint, string> SizeSpecifiers = new()
    {
        { 1, "BYTE" },
        { 2, "WORD" },
        { 4, "DWORD" },
        { 8, "QWORD" },
    };

    private IAsmPlace PlaceToAsmPlace(IPlace place)
    {
        return place switch
        {
            Field field => FieldToAsmPlace(field),
            Local local => _locals[local.LocalName].Place,
            StaticField staticField => throw new NotImplementedException(),
            Deref deref => new PointerTo(PlaceToAsmPlace(deref.PointerPlace), 0),
            _ => throw new ArgumentOutOfRangeException(nameof(place))
        };
    }

    private IAsmPlace FieldToAsmPlace(Field field)
    {
        var ownerPlace = PlaceToAsmPlace(field.FieldOwner);
        var ownerType = GetPlaceType(field.FieldOwner);
        var concreteOwner = ownerType as LoweredConcreteTypeReference;

        while (concreteOwner is null)
        {
            ownerType = ownerType switch
            {
                LoweredConcreteTypeReference => ownerType,
                LoweredFunctionReference => throw new InvalidOperationException("FunctionReference has no fields"),
                LoweredGenericPlaceholder(_, var placeholderName) => _currentTypeArguments[placeholderName],
                LoweredPointer(var pointerTo) => pointerTo,
                RawPointer => throw new InvalidOperationException("Raw Pointer has no fields"),
                _ => throw new ArgumentOutOfRangeException(nameof(ownerType))
            };
            
            concreteOwner = ownerType as LoweredConcreteTypeReference;
        }

        var typeSize = GetTypeSize(ownerType, _currentTypeArguments);
        var fieldSize = typeSize.VariantFieldOffsets[field.VariantName][field.FieldName];

        return ownerPlace switch
        {
            OffsetFromStackPointer(var offset) => new OffsetFromStackPointer(offset + (int)fieldSize.Offset),
            OffsetFromBasePointer(var offset) => new OffsetFromBasePointer(offset + (int)fieldSize.Offset),
            PointerTo pointerTo => pointerTo with {Offset = pointerTo.Offset + (int)fieldSize.Offset},
            Register register => throw new NotImplementedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerPlace))
        };
    }
    
}