using Reef.Core.LoweredExpressions;

namespace Reef.Core;

public class TreeShaker(LoweredProgram program)
{
    private readonly HashSet<DefId> _usefulMethodDefIds = [];
    private readonly Dictionary<DefId, IMethod> _methods = program.Methods
        .ToDictionary(x => x.Id);

    public HashSet<DefId> Shake()
    {
        var mainMethod = program.Methods.OfType<LoweredMethod>().Single(x => x.Name == "_Main");

        ShakeMethod(mainMethod);

        return _usefulMethodDefIds;
    }

    private void ShakeMethod(IMethod method)
    {
        if (!_usefulMethodDefIds.Add(method.Id) || method is not LoweredMethod loweredMethod)
        {
            return;
        }

        foreach (var basicBlock in loweredMethod.BasicBlocks)
        {
            var operands = new List<IOperand>();
            foreach (var statement in basicBlock.Statements)
            {
                switch (statement)
                {
                    case Assign assign:
                        {
                            switch (assign.RValue)
                            {
                                case FillArray fillArray:
                                    {
                                        operands.Add(fillArray.Value);
                                        break;
                                    }
                                case BinaryOperation binaryOperation:
                                    {
                                        operands.Add(binaryOperation.LeftOperand);
                                        operands.Add(binaryOperation.RightOperand);
                                        break;
                                    }
                                case UnaryOperation unaryOperation:
                                    {
                                        operands.Add(unaryOperation.Operand);
                                        break;
                                    }
                                case Use use:
                                    {
                                        operands.Add(use.Operand);
                                        break;
                                    }
                                case CreateObject createObject:
                                case CreateArray:
                                    {
                                        // no operands
                                        break;
                                    }
                                default:
                                    {
                                        throw new NotImplementedException(assign.RValue.GetType().ToString());
                                    }
                            }
                            break;
                        }
                    default:
                        {
                            throw new NotImplementedException(statement.GetType().ToString());
                        }
                }
            }

            switch (basicBlock.Terminator)
            {
                case MethodCall { Function: LoweredFunctionReference { DefinitionId: var functionId } } methodCall:
                    {
                        ShakeMethod(_methods[functionId]);
                        operands.AddRange(methodCall.Arguments);
                        break;
                    }
                case MethodCall { Function: MethodPointerFunctionReference { MethodPointer: var methodPointer } }:
                    {
                        operands.Add(methodPointer);
                        break;
                    }
                case SwitchInt switchInt:
                    {
                        operands.Add(switchInt.Operand);
                        break;
                    }
                case Assert assert:
                    {
                        operands.Add(assert.Value);
                        break;
                    }
                case Return:
                case GoTo:
                case null:
                    break; // noop
                default:
                    throw new NotImplementedException(basicBlock.Terminator.GetType().ToString());
            }

            foreach (var operand in operands.OfType<FunctionPointerConstant>())
            {
                ShakeMethod(_methods[operand.Value.DefinitionId]);
            }
        }
    }
}
