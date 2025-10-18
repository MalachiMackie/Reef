namespace Reef.Core.IL;

public interface IInstruction
{
}

public record InstructionLabel(string Name, uint ReferencesInstructionIndex);

public record InstructionList(List<IInstruction> Instructions, List<InstructionLabel> Labels)
{
    public void Add(IInstruction instruction)
    {
        Instructions.Add(instruction);
    }

    public void AddLabel(InstructionLabel label)
    {
        Labels.Add(label);
    }
}

/// <summary>
/// Compares two integer values. If the first is greater than the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntGreaterThan : IInstruction;

/// <summary>
/// Compares two integer values. If the first is less than the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntLessThan : IInstruction;

/// <summary>
/// Compares two integer values. If the first is greater than or equal to the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntGreaterOrEqualTo : IInstruction;

/// <summary>
/// Compares two integer values. If the first is less than or equal to the second, true is pushed
/// onto the evaluation stack, otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntLessOrEqualTo : IInstruction;

/// <summary>
/// Compares two integer values. If they are equal, true is pushed onto the stack,
/// otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntEqual : IInstruction;

/// <summary>
/// Compares two integer values. If they are not equal, true is pushed onto the stack,
/// otherwise false is pushed onto the evaluation stack
/// </summary>
public record CompareIntNotEqual : IInstruction;

/// <summary>
/// Loads the local variable at specified index onto the evaluation stack
/// </summary>
public record LoadLocal(string LocalName) : IInstruction;

/// <summary>
/// Branch to the specified Instruction if the top of the evaluation stack is false
/// </summary>
public record BranchIfFalse(string BranchToLabelName) : IInstruction;

/// <summary>
/// Branch to the specified Instruction if the top of the evaluation stack is true
/// </summary>
public record BranchIfTrue(string BranchToLabelName) : IInstruction;

/// <summary>
/// Copy the value from the top of the evaluation stack and pushes a copy
/// </summary>
public record CopyStack : IInstruction;

/// <summary>
/// Loads the argument at the specified argument index onto evaluation stack
/// </summary>
public record LoadArgument(uint ArgumentIndex) : IInstruction;

/// <summary>
/// Pops the top of the evaluation stack and discards it
/// </summary>
public record Drop : IInstruction;

/// <summary>
/// Unconditionally branch to the specified Instruction
/// </summary>
public record Branch(string BranchToLabelName) : IInstruction;

/// <summary>
/// Returns from the current method, leaving the return value on the evaluation stack
/// </summary>
public record Return : IInstruction;

/// <summary>
/// Pops the top method off the evaluation stack and calls it, providing arguments on the evaluation stack. Last argument is
/// on the top of the stack after the method. If the method is an instance method, a reference to the instance is passed
/// as the first argument
/// </summary>
public record Call(uint Arity, uint StackSize, bool ValueUseful) : IInstruction;

/// <summary>
/// Loads a reference to the specified global function onto the evaluation stack
/// </summary>
/// <param name="FunctionDefinitionReference"></param>
public record LoadFunction(FunctionDefinitionReference FunctionDefinitionReference) : IInstruction;

/// <summary>
/// Pops the top of the evaluation stack and stores it in the specified local index
/// </summary>
public record StoreLocal(string LocalName) : IInstruction;

/// <summary>
/// Creates an object of the specified type and pushes the resulting reference onto the evaluation stack
/// </summary>
public record CreateObject(ConcreteReefTypeReference ReefType) : IInstruction;

/// <summary>
/// Loads a reference to the specified type into the evaluation stack
/// </summary>
public record LoadType(IReefTypeReference ReefType) : IInstruction;

/// <summary>
/// Pops two values off the evaluation stack, storing the top most value
/// into the specified field of the bottom most value reference
/// </summary>
public record StoreField(uint VariantIndex, string FieldName) : IInstruction;

/// <summary>
/// Pops the top value off of the evaluation stack and loads the field of the reference onto the evaluation stack
/// </summary>
public record LoadField(uint VariantIndex, string FieldName) : IInstruction;

/// <summary>
/// Pops the top int off the stack and jumps to the relevant label given in the provided options
/// </summary>
public record SwitchInt(Dictionary<int, string> BranchLabels, string Otherwise) : IInstruction;

/// <summary>
/// Pops the top value off the evaluation stack and stores it in the specified static field
/// </summary>
public record StoreStaticField(IReefTypeReference ReefType, string StaticFieldName) : IInstruction;

/// <summary>
/// Loads the specified static field and pushes it onto the evaluation stack
/// </summary>
public record LoadStaticField(IReefTypeReference ReefType, string FieldName)
    : IInstruction;

/// <summary>
/// Loads a string constant onto the evaluation stack
/// </summary>
/// <param name="Value"></param>
public record LoadStringConstant(string Value) : IInstruction;

/// <summary>
/// Loads a constant int onto the evaluation stack
/// </summary>
public record LoadIntConstant(int Value) : IInstruction;

/// <summary>
/// Loads a constant bool onto the evaluation stack
/// </summary>
/// <param name="Value"></param>
public record LoadBoolConstant(bool Value) : IInstruction;

/// <summary>
/// pops a bool off the evaluation stack and pushes it back on as an int
/// </summary>
public record CastBoolToInt : IInstruction;

/// <summary>
/// Loads a unit onto the evaluation stack
/// </summary>
public record LoadUnitConstant : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and puts the sum back on the evaluation stack
/// </summary>
public record IntPlus : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and puts back the difference on the evaluation stack
/// </summary>
public record IntMinus : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and puts back the product on the evaluation stack
/// </summary>
public record IntMultiply : IInstruction;

/// <summary>
/// Pulls two ints off the evaluation stack and divides the first by the second, putting the result on the stack
/// </summary>
public record IntDivide : IInstruction;

/// <summary>
/// Pulls the top boolean off the stack and inverts it, putting the result back on the stack
/// </summary>
public record BoolNot : IInstruction;
