using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Reef.Core.LoweredExpressions;

public class PrettyPrinter(bool parensAroundExpressions, bool printValueUseful)
{
    private readonly StringBuilder _stringBuilder = new();
    private uint _indentationLevel;
    private IReadOnlyList<MethodLocal> _methodLocals = [];
    private IReadOnlyList<DataType> _dataTypes = [];

    public static string PrettyPrintLoweredProgram(
            LoweredProgram program,
            bool parensAroundExpressions = true,
            bool printValueUseful = true)
    {
        var printer = new PrettyPrinter(parensAroundExpressions, printValueUseful);

        printer.PrettyPrintLoweredProgramInner(program);

        return printer._stringBuilder.ToString();
    }

    private void PrettyPrintLoweredProgramInner(LoweredProgram program)
    {
        _dataTypes = program.DataTypes;
        foreach (var dataType in program.DataTypes)
        {
            PrettyPrintDataType(dataType);
        }

        foreach (var method in program.Methods)
        {
            PrettyPrintMethod(method);
        }
    }

    private void PrettyPrintDataType(DataType dataType)
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

    private void PrettyPrintStaticField(StaticDataTypeField field)
    {
        Indent();
        _stringBuilder.Append($"static field {field.Name}: ");
        PrettyPrintTypeReference(field.Type);
        _stringBuilder.Append(" = ");
        PrettyPrintExpression(field.StaticInitializer);
        _stringBuilder.AppendLine(",");
    }

    private void PrettyPrintField(DataTypeField field)
    {
        Indent();
        _stringBuilder.Append($"field {field.Name}: ");
        PrettyPrintTypeReference(field.Type);
        _stringBuilder.AppendLine(",");
    }

    

    private void PrettyPrintTypeReference(ILoweredTypeReference typeReference)
    {
        switch (typeReference)
        {
            case LoweredGenericPlaceholder placeholder:
                {
                    _stringBuilder.Append(placeholder.PlaceholderName);
                    break;
                }
            case LoweredConcreteTypeReference concrete:
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

    private void PrettyPrintVariant(DataTypeVariant variant)
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

    private void PrettyPrintMethod(LoweredMethod method)
    {
        _methodLocals = method.Locals;
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

        for (var i = 0; i < method.Parameters.Count; i++)
        {
            if (i > 0)
            {
                _stringBuilder.Append(", ");
            }
            PrettyPrintTypeReference(method.Parameters[i]);
        }
        _stringBuilder.Append("): ");
        PrettyPrintTypeReference(method.ReturnType);
        _stringBuilder.AppendLine(" {");
        _indentationLevel++;

        for (var i = 0; i < method.Expressions.Count; i++)
        {
            var expression = method.Expressions[i];
            Indent();
            var beforeLength = _stringBuilder.Length;
            PrettyPrintExpression(expression);

            // only print a semicolon if it's not a tail expression and we've actually printed something
            if ((i < method.Expressions.Count - 1 || !expression.ValueUseful) && beforeLength != _stringBuilder.Length)
            {
                _stringBuilder.Append(';');
            }
            _stringBuilder.AppendLine();
        }

        _indentationLevel--;
        Indent();
        _stringBuilder.AppendLine("}");
    }

    private void PrettyPrintExpression(ILoweredExpression expression)
    {
        if (parensAroundExpressions)
        {
            _stringBuilder.Append('(');
        }
        if (printValueUseful)
        {
            _stringBuilder.Append($"[ValueUseful: {expression.ValueUseful}] ");
        }
        switch (expression)
        {
            case VariableDeclarationExpression e:
            {
                var isInLocalsType = _methodLocals.Where(x => x.Name == "__locals")
                    .Any(x =>
                    {
                        var dataType = _dataTypes.First(y => y.Id == (x.Type as LoweredConcreteTypeReference)!.DefinitionId);
                        return dataType.Variants[0].Fields.Any(y => y.Name == e.LocalName);
                    });

                if (!isInLocalsType)
                {
                    _stringBuilder.Append($"var {e.LocalName}: ");
                    var local = _methodLocals.First(x => x.Name == e.LocalName);
                    PrettyPrintTypeReference(local.Type);
                }
                break;
            }
            case VariableDeclarationAndAssignmentExpression e:
            {
                var isInLocalsType = _methodLocals.Where(x => x.Name == "__locals")
                    .Any(x =>
                    {
                        var dataType = _dataTypes.First(y => y.Id == (x.Type as LoweredConcreteTypeReference)!.DefinitionId);
                        return dataType.Variants[0].Fields.Any(y => y.Name == e.LocalName);
                    });

                if (isInLocalsType)
                {
                    _stringBuilder.Append($"__locals.{e.LocalName}");
                }
                else
                {
                    _stringBuilder.Append($"var {e.LocalName}: ");
                    var local = _methodLocals.First(x => x.Name == e.LocalName);
                    PrettyPrintTypeReference(local.Type);
                }
                _stringBuilder.Append(" = ");
                PrettyPrintExpression(e.Value);
                break;
            }
            case IntEqualsExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" == ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case IntGreaterThanExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" > ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case IntLessThanExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" < ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case IntDivideExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" / ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case IntMultiplyExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" * ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case IntPlusExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" + ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case IntMinusExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" - ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case BoolAndExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" && ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case BoolOrExpression e:
            {
                PrettyPrintExpression(e.Left);
                _stringBuilder.Append(" || ");
                PrettyPrintExpression(e.Right);
                break;
            }
            case BoolNotExpression e:
            {
                _stringBuilder.Append('!');
                PrettyPrintExpression(e.Operand);
                break;
            }
            case MethodReturnExpression e:
            {
                _stringBuilder.Append("return ");
                PrettyPrintExpression(e.ReturnValue);
                break;
            }
            case CreateObjectExpression e:
            {
                _stringBuilder.Append("new ");
                PrettyPrintTypeReference(e.Type);
                _stringBuilder.Append($"::{e.Variant}");
                _stringBuilder.Append(" {");
                if (e.VariantFieldInitializers.Count > 0)
                {
                    _indentationLevel++;
                    for (var i = 0; i < e.VariantFieldInitializers.Count; i++)
                    {
                        _stringBuilder.AppendLine();
                        Indent();
                        var (fieldName, fieldValue) = e.VariantFieldInitializers.ElementAt(i);
                        _stringBuilder.Append($"{fieldName} = ");
                        PrettyPrintExpression(fieldValue);
                        _stringBuilder.Append(',');
                    }
                    _stringBuilder.AppendLine();
                    _indentationLevel--;
                    Indent();
                }
                _stringBuilder.Append('}');
                break;
            }
            case UnitConstantExpression:
            {
                _stringBuilder.Append("()");
                break;
            }
            case IntConstantExpression e:
            {
                _stringBuilder.Append(e.Value);
                break;
            }
            case StringConstantExpression e:
            {
                _stringBuilder.Append($"\"{e.Value}\"");
                break;
            }
            case BoolConstantExpression e:
            {
                _stringBuilder.Append(e.Value ? "true" : "false");
                break;
            }
            case LocalVariableAccessor e:
            {
                _stringBuilder.Append(e.LocalName);
                break;
            }
            case LoadArgumentExpression e:
            {
                _stringBuilder.Append($"LoadArgument({e.ArgumentIndex})");
                break;
            }
            case FunctionReferenceConstantExpression e:
            {
                _stringBuilder.Append('*');
                PrettyPrintFunctionReference(e.FunctionReference);
                break;
            }
            case FieldAssignmentExpression e:
            {
                PrettyPrintExpression(e.FieldOwnerExpression);
                _stringBuilder.Append($".{e.FieldName} = ");
                PrettyPrintExpression(e.FieldValue);
                break;
            }
            case FieldAccessExpression e:
            {
                PrettyPrintExpression(e.MemberOwner);
                _stringBuilder.Append($".{e.FieldName}");
                break;
            }
            case StaticFieldAccessExpression e:
            {
                PrettyPrintTypeReference(e.OwnerType);
                _stringBuilder.Append($"::{e.FieldName}");
                break;
            }
            case StaticFieldAssignmentExpression e:
            {
                PrettyPrintTypeReference(e.OwnerType);
                _stringBuilder.Append($"::{e.FieldName} = ");
                PrettyPrintExpression(e.FieldValue);
                break;
            }
            case LocalAssignmentExpression e:
            {
                _stringBuilder.Append($"{e.LocalName} = ");
                PrettyPrintExpression(e.Value);
                break;
            }
            case MethodCallExpression e:
            {
                PrettyPrintFunctionReference(e.FunctionReference);
                _stringBuilder.Append('(');
                for (var i = 0; i < e.Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        _stringBuilder.Append(", ");
                    }
                    PrettyPrintExpression(e.Arguments[i]);
                }
                _stringBuilder.Append(')');
                break;
            }
            case SwitchIntExpression e:
            {
                _stringBuilder.Append("int-switch (");
                PrettyPrintExpression(e.Check);
                _stringBuilder.AppendLine(") {");
                _indentationLevel++;
                foreach (var (i, result) in e.Results)
                {
                    Indent();
                    _stringBuilder.Append($"{i} => ");
                    PrettyPrintExpression(result);
                    _stringBuilder.AppendLine(",");
                }
                Indent();
                _stringBuilder.Append("otherwise => ");
                PrettyPrintExpression(e.Otherwise);
                _stringBuilder.AppendLine(",");
                _indentationLevel--;
                Indent();
                _stringBuilder.Append('}');
                break;
            }
            case CastBoolToIntExpression e:
            {
                _stringBuilder.Append("(int -> bool)");
                if (!parensAroundExpressions) {
                    // we always want to print parentheses around this expression
                    _stringBuilder.Append('(');
                }
                PrettyPrintExpression(e.BoolExpression);
                if (!parensAroundExpressions) {
                    _stringBuilder.Append(')');
                }
                break;
            }
            case BlockExpression e:
            {
                _stringBuilder.AppendLine("{");
                _indentationLevel++;
                for (var i = 0; i < e.Expressions.Count; i++)
                {
                    var blockExpression = e.Expressions[i];
                    Indent();
                    var beforeLength = _stringBuilder.Length;
                    PrettyPrintExpression(blockExpression);

                    // only print a semicolon if it's not a tail expression and we've actually printed something 
                    if ((i < e.Expressions.Count - 1 || !expression.ValueUseful) && beforeLength != _stringBuilder.Length)
                    {
                        _stringBuilder.Append(';');
                    }
                    _stringBuilder.AppendLine();
                }
                _indentationLevel--;
                Indent();
                _stringBuilder.Append('}');
                break;
            }
            case UnreachableExpression:
            {
                _stringBuilder.Append("Unreachable");
                break;
            }
            default:
                throw new NotImplementedException($"{expression.GetType()}");
        }

        if (parensAroundExpressions)
        {
            _stringBuilder.Append(')');
        }
    }

    private void PrettyPrintFunctionReference(LoweredFunctionReference functionReference)
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
