using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Reef.Core.LoweredExpressions.New;

public class NewPrettyPrinter
{
    private readonly StringBuilder _stringBuilder = new();
    private uint _indentationLevel;

    public static string PrettyPrintLoweredProgram(
            NewLoweredProgram program,
            bool parensAroundExpressions = true,
            bool printValueUseful = true)
    {
        var printer = new NewPrettyPrinter();

        printer.PrettyPrintLoweredProgramInner(program);

        return printer._stringBuilder.ToString();
    }

    private void PrettyPrintLoweredProgramInner(NewLoweredProgram program)
    {
        foreach (var dataType in program.DataTypes)
        {
            PrettyPrintDataType(dataType);
        }

        foreach (var method in program.Methods)
        {
            PrettyPrintMethod(method);
        }
    }

    private void PrettyPrintDataType(NewDataType dataType)
    {
        Indent();
        _stringBuilder.Append($"datatype {dataType.Name}");
        if (dataType.TypeParameters.Count > 0)
        {
            _stringBuilder.Append('<');
            _stringBuilder.AppendJoin(", ", dataType.TypeParameters.Select(x => x.PlaceholderName));
            _stringBuilder.Append('>');
        }
        _stringBuilder.AppendLine(" {");
        _indentationLevel++;

        foreach (var variant in dataType.Variants)
        {
            PrettyPrintVariant(variant);
        }

        foreach (var staticField in dataType.StaticFields)
        {
            PrettyPrintStaticField(staticField);
        }

        _indentationLevel--;
        Indent();
        _stringBuilder.AppendLine("}");
    }

    private void PrettyPrintStaticField(NewStaticDataTypeField field)
    {
        Indent();
        _stringBuilder.Append($"static field {field.Name}: ");
        PrettyPrintTypeReference(field.Type);
        _stringBuilder.Append(" = ");
        PrettyPrintCodeBlock(field.InitializerBasicBlocks, field.InitializerLocals, field.ReturnValueLocal);
        _stringBuilder.AppendLine(",");
    }

    private void PrettyPrintField(NewDataTypeField field)
    {
        Indent();
        _stringBuilder.Append($"field {field.Name}: ");
        PrettyPrintTypeReference(field.Type);
        _stringBuilder.AppendLine(",");
    }

    

    private void PrettyPrintTypeReference(INewLoweredTypeReference typeReference)
    {
        switch (typeReference)
        {
            case NewLoweredGenericPlaceholder placeholder:
                {
                    _stringBuilder.Append(placeholder.PlaceholderName);
                    break;
                }
            case NewLoweredConcreteTypeReference concrete:
                {
                    _stringBuilder.Append(concrete.Name);
                    if (concrete.TypeArguments.Count > 0)
                    {
                        _stringBuilder.Append("::<");
                        for (var i = 0; i < concrete.TypeArguments.Count; i++)
                        {
                            if (i > 0)
                            {
                                _stringBuilder.Append(", ");
                            }
                            PrettyPrintTypeReference(concrete.TypeArguments[i]);
                        }
                        _stringBuilder.Append('>');
                    }
                    break;
                }
            default:
                throw new UnreachableException($"{typeReference}: {typeReference.GetType()}");
        }
    }

    private void PrettyPrintVariant(NewDataTypeVariant variant)
    {
        Indent();
        _stringBuilder.AppendLine($"{variant.Name} {{");
        _indentationLevel++;
        foreach (var field in variant.Fields)
        {
            PrettyPrintField(field);
        }
        _indentationLevel--;
        Indent();
        _stringBuilder.AppendLine("}");
    }

    private void PrettyPrintMethod(NewLoweredMethod method)
    {
        Indent();
        _stringBuilder.Append($"fn {method.Name}");

        if (method.TypeParameters.Count > 0)
        {
            _stringBuilder.Append('<');
            for (var i = 0; i < method.TypeParameters.Count; i++)
            {
                if (i > 0)
                {
                    _stringBuilder.Append(", ");
                }
                _stringBuilder.Append(method.TypeParameters[i].PlaceholderName);
            }
            _stringBuilder.Append('>');
        }
        _stringBuilder.Append('(');

        for (var i = 0; i < method.ParameterLocals.Count; i++)
        {
            if (i > 0)
            {
                _stringBuilder.Append(", ");
            }

            _stringBuilder.Append($"{method.ParameterLocals[i].CompilerGivenName}: ");
            PrettyPrintTypeReference(method.ParameterLocals[i].Type);
        }
        _stringBuilder.Append("): ");
        PrettyPrintTypeReference(method.ReturnValue.Type);
        _stringBuilder.AppendLine(" {");
        _indentationLevel++;

        PrettyPrintCodeBlock(method.BasicBlocks, method.Locals, method.ReturnValue);

        

        _indentationLevel--;
        Indent();
        _stringBuilder.AppendLine("}");
    }

    private void PrettyPrintCodeBlock(IReadOnlyList<BasicBlock> basicBlocks, IReadOnlyList<NewMethodLocal> methodLocals, NewMethodLocal returnLocal)
    {
        foreach (var local in methodLocals.Prepend(returnLocal))
        {
            Indent();
            _stringBuilder.Append($"let {local.CompilerGivenName}: ");
            PrettyPrintTypeReference(local.Type);
            _stringBuilder.AppendLine(";");
        }

        foreach (var basicBlock in basicBlocks)
        {
            Indent();
            _stringBuilder.AppendLine($"{basicBlock.Id.Id}: {{");
            _indentationLevel++;
            
            PrettyPrintJoin(
                basicBlock.Statements,
                statement =>
                {
                    Indent();
                    PrettyPrintStatement(statement);
                },
                "\n");

            Indent();
            PrettyPrintTerminator(basicBlock.Terminator);
            _stringBuilder.AppendLine();
            
            _indentationLevel--;
            Indent();
            _stringBuilder.AppendLine("}");
        }
    }

    private void PrettyPrintTerminator(ITerminator? terminator)
    {
        if (terminator is null)
        {
            _stringBuilder.Append("[No Terminator]");
            return;
        }

        switch (terminator)
        {
            case MethodCall methodCall:
            {
                _stringBuilder.Append($"{methodCall.LocalDestination} = ");
                PrettyPrintFunctionReference(methodCall.Function);
                _stringBuilder.Append('(');
                PrettyPrintJoin(methodCall.Arguments, PrettyPrintOperand, ", ");
                _stringBuilder.Append($") -> [return: {methodCall.GoToAfter.Id}];");

                break;
            }
            case Return:
            {
                _stringBuilder.AppendLine("return;");
                break;
            }
            case SwitchInt switchInt:
            {
                _stringBuilder.Append("switchInt(");
                PrettyPrintOperand(switchInt.Operand);
                _stringBuilder.Append(") -> [");
                PrettyPrintJoin(
                    switchInt.Cases.OrderBy(x => x.Key).ToArray(),
                    branch => _stringBuilder.Append($"{branch.Key}: {branch.Value.Id}"),
                    ", ");
                _stringBuilder.Append($", otherwise {switchInt.Otherwise.Id}];");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(terminator));
        }
    }

    private void PrettyPrintJoin<T>(IReadOnlyList<T> items, Action<T> printItem, string join)
    {
        for (var index = 0; index < items.Count; index++)
        {
            printItem(items[index]);
            if (index + 1 < items.Count)
            {
                _stringBuilder.Append(join);
            }
        }
    }
    
    private void PrettyPrintStatement(IStatement statement)
    {
        switch (statement)
        {
            case Assign assign:
            {
                _stringBuilder.Append($"{assign.Local} = ");
                PrettyPrintRValue(assign.RValue);
                _stringBuilder.Append(';');
                break;
            }
            case LocalAlive localAlive:
                throw new NotImplementedException();
            case LocalDead localDead:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void PrettyPrintRValue(IRValue rValue)
    {
        switch (rValue)
        {
            case BinaryOperation binaryOperation:
            {
                _stringBuilder.Append(binaryOperation.Kind switch
                {
                    BinaryOperationKind.Add => "Add",
                    BinaryOperationKind.Subtract => "Subtract",
                    BinaryOperationKind.Multiply => "Multiply",
                    BinaryOperationKind.Divide => "Divide",
                    BinaryOperationKind.LessThan => "LessThan",
                    BinaryOperationKind.LessThanOrEqual => "LessThanOrEqual",
                    BinaryOperationKind.GreaterThan => "GreaterThan",
                    BinaryOperationKind.GreaterThanOrEqual => "GreaterThanOrEqual",
                    BinaryOperationKind.Equal => "Equal",
                    BinaryOperationKind.NotEqual => "NotEqual",
                    _ => throw new ArgumentOutOfRangeException()
                });
                _stringBuilder.Append('(');
                PrettyPrintJoin([binaryOperation.LeftOperand, binaryOperation.RightOperand], PrettyPrintOperand, ", ");
                _stringBuilder.Append(')');
                break;
            }
            case FieldAccess fieldAccess:
                _stringBuilder.Append('(');
                PrettyPrintOperand(fieldAccess.FieldOwner);
                _stringBuilder.Append($"as variant: {fieldAccess.VariantName})");
                _stringBuilder.Append($".{fieldAccess.FieldName}");
                break;
            case UnaryOperation unaryOperation:
                _stringBuilder.Append(unaryOperation.Kind switch
                {
                    UnaryOperationKind.Not => '!',
                    UnaryOperationKind.Negate => '-',
                    _ => throw new ArgumentOutOfRangeException()
                });
                PrettyPrintOperand(unaryOperation.Operand);
                break;
            case Use use:
                PrettyPrintOperand(use.Operand);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rValue));
        }
    }

    private void PrettyPrintOperand(IOperand operand)
    {
        switch (operand)
        {
            case IntConstant intConstant:
                _stringBuilder.Append($"{intConstant.Value}_int{intConstant.ByteSize * 8}");
                break;
            case StringConstant stringConstant:
                _stringBuilder.Append($"\"{stringConstant}\"");
                break;
            case UIntConstant uIntConstant:
                _stringBuilder.Append($"{uIntConstant.Value}_uint{uIntConstant.ByteSize * 8}");
                break;
            case UnitConstant:
                _stringBuilder.Append("()");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operand));
        }
    }

    private void PrettyPrintFunctionReference(NewLoweredFunctionReference functionReference)
    {
        _stringBuilder.Append(functionReference.Name);
        if (functionReference.TypeArguments.Count > 0)
        {
            _stringBuilder.Append("::<");
            for (var i = 0; i < functionReference.TypeArguments.Count; i++)
            {
                if (i > 0)
                {
                    _stringBuilder.Append(", ");
                }
                PrettyPrintTypeReference(functionReference.TypeArguments[i]);
            }
            _stringBuilder.Append('>');
        }
    }

    private static readonly ConcurrentDictionary<uint, string> IndentationStrings = [];
    private void Indent()
    {
        if (!IndentationStrings.TryGetValue(_indentationLevel, out var str))
        {
            str = new string(' ', (int)_indentationLevel * 4);
            IndentationStrings[_indentationLevel] = str;
        }
        _stringBuilder.Append(str);
    }
}
