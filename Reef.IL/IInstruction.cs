namespace Reef.IL;

public interface IInstruction
{
    InstructionAddress Address { get; }
}

/// <summary>
/// Compares two integer values. If the first is greater than the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntGreaterThan(InstructionAddress Address) : IInstruction;

/// <summary>
/// Compares two integer values. If the first is less than the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntLessThan(InstructionAddress Address) : IInstruction;

/// <summary>
/// Compares two integer values. If the first is greater than or equal to the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntGreaterOrEqualTo(InstructionAddress Address) : IInstruction;

/// <summary>
/// Compares two integer values. If the first is less than or equal to the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntLessOrEqualTo(InstructionAddress Address) : IInstruction;

/// <summary>
/// Compares two integer values. If they are equal, true is pushed onto the stack,
/// otherwise false is pushed onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record CompareIntEqual(InstructionAddress Address) : IInstruction;

/// <summary>
/// Loads the local variable at specified index onto the evaluation stack
/// </summary>
/// <param name="LocalIndex"></param>
public record LoadLocal(InstructionAddress Address, uint LocalIndex) : IInstruction;

/// <summary>
/// Branch to the specified Instruction if the top of the evaluation stack is false
/// </summary>
public record BranchIfFalse(InstructionAddress Address, InstructionAddress BranchTo) : IInstruction;

/// <summary>
/// Copy the value from the top of the evaluation stack and pushes a copy
/// </summary>
/// <param name="Address"></param>
public record CopyStack(InstructionAddress Address) : IInstruction;

/// <summary>
/// Loads the argument at the specified argument index onto evaluation stack
/// </summary>
/// <param name="Address"></param>
public record LoadArgument(InstructionAddress Address, uint ArgumentIndex) : IInstruction;

/// <summary>
/// Pops the top of the evaluation stack and discards it
/// </summary>
/// <param name="Address"></param>
public record Drop(InstructionAddress Address) : IInstruction;

/// <summary>
/// Unconditionally branch to the specified Instruction
/// </summary>
/// <param name="Address"></param>
/// <param name="BranchTo"></param>
public record Branch(InstructionAddress Address, InstructionAddress BranchTo) : IInstruction;

/// <summary>
/// Returns from the current method, leaving the return value on the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record Return(InstructionAddress Address) : IInstruction;

/// <summary>
/// Pops the top method off the evaluation stack and calls it, providing arguments on the evaluation stack. Last argument is
/// on the top of the stack after the method. If the method is an instance method, a reference to the instance is passed
/// as the first argument
/// </summary>
/// <param name="Address"></param>
public record Call(InstructionAddress Address) : IInstruction;

/// <summary>
/// Loads a reference to the specified global function onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
/// <param name="FunctionDefinitionReference"></param>
public record LoadGlobalFunction(InstructionAddress Address, FunctionDefinitionReference FunctionDefinitionReference) : IInstruction;

/// <summary>
/// Loads a reference to the specified type function onto the evaluation stack
/// </summary>
public record LoadTypeFunction(InstructionAddress Address, ConcreteReefTypeReference ReefType, uint FunctionIndex, IReadOnlyList<IReefTypeReference> TypeArguments) : IInstruction;

/// <summary>
/// Pops the top of the evaluation stack and stores it in the specified local index
/// </summary>
/// <param name="Address"></param>
/// <param name="LocalIndex"></param>
public record StoreLocal(InstructionAddress Address, uint LocalIndex) : IInstruction;

/// <summary>
/// Creates an object of the specified type and pushes the resulting reference onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record CreateObject(InstructionAddress Address, ConcreteReefTypeReference ReefType) : IInstruction;

/// <summary>
/// Loads a reference to the specified type into the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record LoadType(InstructionAddress Address, IReefTypeReference ReefType) : IInstruction;

/// <summary>
/// Pops two values off the evaluation stack, storing the top most value
/// into the specified field of the bottom most value reference
/// </summary>
/// <param name="Address"></param>
public record StoreField(InstructionAddress Address, uint VariantIndex, uint FieldIndex) : IInstruction;

/// <summary>
/// Pops the top value off of the evaluation stack and loads the field of the reference onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
/// <param name="FieldIndex"></param>
public record LoadField(InstructionAddress Address, uint VariantIndex, uint FieldIndex) : IInstruction;

/// <summary>
/// Pops the top value off the evaluation stack and stores it in the specified static field
/// </summary>
/// <param name="Address"></param>
public record StoreStaticField(InstructionAddress Address, IReefTypeReference ReefType, uint StaticFieldIndex) : IInstruction;

/// <summary>
/// Loads the specified static field and pushes it onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record LoadStaticField(InstructionAddress Address, IReefTypeReference ReefType, uint VariantIndex, uint FieldIndex)
    : IInstruction;

/// <summary>
/// Loads a string constant onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
/// <param name="Value"></param>
public record LoadStringConstant(InstructionAddress Address, string Value) : IInstruction;

/// <summary>
/// Loads a constant int onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record LoadIntConstant(InstructionAddress Address, int Value) : IInstruction;

/// <summary>
/// Loads a constant bool onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
/// <param name="Value"></param>
public record LoadBoolConstant(InstructionAddress Address, bool Value) : IInstruction;

/// <summary>
/// Loads a unit onto the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record LoadUnitConstant(InstructionAddress Address) : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and puts the sum back on the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record IntPlus(InstructionAddress Address) : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and puts back the difference on the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record IntMinus(InstructionAddress Address) : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and puts back the product on the evaluation stack
/// </summary>
/// <param name="Address"></param>
public record IntMultiply(InstructionAddress Address) : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and divides the first by the second, putting the result on the stack
/// </summary>
/// <param name="Address"></param>
public record IntDivide(InstructionAddress Address) : IInstruction;

/// <summary>
/// Pulls the top boolean off the stack and inverts it, putting the result back on the stack
/// </summary>
/// <param name="Address"></param>
public record BoolNot(InstructionAddress Address) : IInstruction;