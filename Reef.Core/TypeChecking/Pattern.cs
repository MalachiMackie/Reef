using System.Diagnostics;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private List<LocalVariable> TypeCheckPattern(ITypeReference valueTypeReference, IPattern pattern)
    {
        var patternVariables = new List<LocalVariable>();
        switch (pattern)
        {
            case DiscardPattern:
                // discard pattern always type checks
                break;
            case UnionVariantPattern variantPattern:
                {
                    var patternUnionType = GetTypeReference(variantPattern.Type);
                    variantPattern.TypeReference = patternUnionType;

                    if (patternUnionType is not InstantiatedUnion union)
                    {
                        throw new InvalidOperationException($"{patternUnionType} is not a union");
                    }

                    ExpectType(valueTypeReference, union, variantPattern.SourceRange);

                    if (variantPattern.VariantName is not null)
                    {
                        if (union.Variants.All(x => x.Name != variantPattern.VariantName.StringValue))
                        {
                            _errors.Add(TypeCheckerError.UnknownTypeMember(variantPattern.VariantName, union.Name));
                            break;
                        }
                    }

                    if (variantPattern.VariableName is { } variableName)
                    {
                        var variable = new LocalVariable(
                            CurrentFunctionSignature,
                            variableName,
                            patternUnionType,
                            false,
                            variantPattern.IsMutableVariable);
                        variantPattern.Variable = variable;
                        patternVariables.Add(variable);
                        if (!TryAddScopedVariable(variableName.StringValue, variable))
                        {
                            throw new InvalidOperationException($"Duplicate variable {variableName}");
                        }
                    }

                    break;
                }
            case ClassPattern classPattern:
                {
                    var patternType = GetTypeReference(classPattern.Type);
                    classPattern.TypeReference = patternType;

                    if (patternType is UnknownType)
                    {
                        break;
                    }

                    ExpectType(patternType, valueTypeReference, classPattern.SourceRange);

                    if (patternType is not InstantiatedClass classType)
                    {
                        _errors.Add(TypeCheckerError.NonClassUsedInClassPattern(classPattern.Type));
                        break;
                    }

                    if (classPattern.FieldPatterns.GroupBy(x => x.FieldName.StringValue).Any(x => x.Count() > 1))
                    {
                        throw new InvalidOperationException("Duplicate fields found");
                    }

                    var remainingFields = classType.Fields.Where(x => x.IsPublic)
                        .Select(x => x.Name)
                        .ToHashSet();

                    foreach (var fieldPattern in classPattern.FieldPatterns)
                    {
                        remainingFields.Remove(fieldPattern.FieldName.StringValue);
                        if (TryGetClassField(classType, fieldPattern.FieldName) is not { } field)
                        {
                            continue;
                        }

                        if (field.IsStatic)
                        {
                            _errors.Add(TypeCheckerError.StaticFieldInClassPattern(fieldPattern.FieldName));
                        }

                        var fieldType = field.Type;

                        if (fieldPattern.Pattern is null)
                        {
                            var variable = new LocalVariable(
                                CurrentFunctionSignature,
                                fieldPattern.FieldName,
                                fieldType,
                                false,
                                false);
                            patternVariables.Add(variable);
                            fieldPattern.Variable = variable;
                            if (!TryAddScopedVariable(fieldPattern.FieldName.StringValue, variable))
                            {
                                throw new InvalidOperationException($"Duplicate variable {fieldPattern.FieldName.StringValue}");
                            }
                        }
                        else
                        {
                            patternVariables.AddRange(TypeCheckPattern(fieldType, fieldPattern.Pattern));
                        }
                    }

                    if (classPattern.RemainingFieldsDiscarded)
                    {
                        remainingFields.Clear();
                    }

                    if (remainingFields.Count > 0)
                    {
                        _errors.Add(TypeCheckerError.MissingFieldsInClassPattern(remainingFields, classPattern.Type));
                    }

                    if (classPattern.VariableName is { } variableName)
                    {
                        var variable = new LocalVariable(
                            CurrentFunctionSignature,
                            variableName,
                            patternType,
                            false,
                            classPattern.IsMutableVariable);
                        classPattern.Variable = variable;
                        patternVariables.Add(variable);
                        if (!TryAddScopedVariable(variableName.StringValue, variable))
                        {
                            throw new InvalidOperationException($"Duplicate variable {variableName}");
                        }
                    }

                    break;
                }
            case UnionClassVariantPattern classVariantPattern:
                {
                    var patternType = GetTypeReference(classVariantPattern.Type);
                    classVariantPattern.TypeReference = patternType;

                    ExpectType(patternType, valueTypeReference, pattern.SourceRange);

                    if (patternType is not InstantiatedUnion union)
                    {
                        throw new InvalidOperationException($"{patternType} is not a union");
                    }

                    var variant = union.Variants.FirstOrDefault(x => x.Name == classVariantPattern.VariantName.StringValue)
                                  ?? throw new InvalidOperationException(
                                      $"No variant found named {classVariantPattern.VariantName.StringValue}");

                    if (variant is not ClassUnionVariant classVariant)
                    {
                        throw new InvalidOperationException($"Variant {variant.Name} is not a class variant");
                    }

                    if (classVariantPattern.FieldPatterns.GroupBy(x => x.FieldName.StringValue).Any(x => x.Count() > 1))
                    {
                        throw new InvalidOperationException("Duplicate fields found");
                    }

                    if (!classVariantPattern.RemainingFieldsDiscarded &&
                        classVariantPattern.FieldPatterns.Count != classVariant.Fields.Count)
                    {
                        _errors.Add(TypeCheckerError.MissingFieldsInUnionClassVariantPattern(
                            classVariantPattern,
                            classVariant.Fields.Select(x => x.Name).Except(classVariantPattern.FieldPatterns.Select(x => x.FieldName.StringValue))));
                    }

                    foreach (var fieldPattern in classVariantPattern.FieldPatterns)
                    {
                        var fieldType = classVariant.Fields.FirstOrDefault(x => x.Name == fieldPattern.FieldName.StringValue)?.Type
                            ?? throw new InvalidOperationException($"No field named {fieldPattern.FieldName}");

                        if (fieldPattern.Pattern is null)
                        {
                            var variable = new LocalVariable(
                                CurrentFunctionSignature,
                                fieldPattern.FieldName,
                                fieldType,
                                false,
                                false);
                            fieldPattern.Variable = variable;
                            patternVariables.Add(variable);
                            if (!TryAddScopedVariable(fieldPattern.FieldName.StringValue, variable))
                            {
                                throw new InvalidOperationException($"Duplicate variable {fieldPattern.FieldName.StringValue}");
                            }
                        }
                        else
                        {
                            patternVariables.AddRange(TypeCheckPattern(fieldType, fieldPattern.Pattern));
                        }
                    }

                    if (classVariantPattern.VariableName is { } variableName)
                    {
                        var variable = new LocalVariable(
                            CurrentFunctionSignature,
                            variableName,
                            patternType,
                            false,
                            classVariantPattern.IsMutableVariable);
                        classVariantPattern.Variable = variable;
                        patternVariables.Add(variable);
                        if (!TryAddScopedVariable(variableName.StringValue, variable))
                        {
                            throw new InvalidOperationException($"Duplicate variable {variableName}");
                        }
                    }

                    break;
                }
            case UnionTupleVariantPattern unionTupleVariantPattern:
                {
                    var patternType = GetTypeReference(unionTupleVariantPattern.Type);
                    unionTupleVariantPattern.TypeReference = patternType;

                    ExpectType(patternType, valueTypeReference, pattern.SourceRange);

                    if (patternType is not InstantiatedUnion unionType)
                    {
                        throw new InvalidOperationException($"{valueTypeReference} is not a union");
                    }

                    var variant = unionType.Variants.FirstOrDefault(x =>
                                      x.Name == unionTupleVariantPattern.VariantName.StringValue)
                                  ?? throw new InvalidOperationException(
                                      $"No union variant found with name {unionTupleVariantPattern.VariantName.StringValue}");

                    if (variant is not TupleUnionVariant tupleUnionVariant)
                    {
                        throw new InvalidOperationException("Expected union to be a tuple variant");
                    }

                    if (tupleUnionVariant.TupleMembers.Count != unionTupleVariantPattern.TupleParamPatterns.Count)
                    {
                        _errors.Add(TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(
                            unionTupleVariantPattern, tupleUnionVariant.TupleMembers.Count));
                    }

                    foreach (var (tupleMemberType, tupleMemberPattern) in tupleUnionVariant.TupleMembers.Zip(
                                 unionTupleVariantPattern.TupleParamPatterns))
                    {
                        patternVariables.AddRange(
                            TypeCheckPattern(tupleMemberType, tupleMemberPattern));
                    }

                    if (unionTupleVariantPattern.VariableName is { } variableName)
                    {
                        var variable = new LocalVariable(
                            CurrentFunctionSignature,
                            variableName,
                            patternType,
                            false,
                            unionTupleVariantPattern.IsMutableVariable);
                        unionTupleVariantPattern.Variable = variable;
                        patternVariables.Add(variable);
                        if (!TryAddScopedVariable(variableName.StringValue, variable))
                        {
                            throw new InvalidOperationException($"Duplicate variable {variableName}");
                        }
                    }

                    break;
                }
            case VariableDeclarationPattern { VariableName: var variableName, IsMut: var variableMutable } variableDeclarationPattern:
                {
                    var variable = new LocalVariable(
                        CurrentFunctionSignature,
                        variableName,
                        valueTypeReference,
                        false,
                        variableMutable);
                    variableDeclarationPattern.Variable = variable;
                    patternVariables.Add(variable);
                    if (!TryAddScopedVariable(variableName.StringValue, variable))
                    {
                        throw new InvalidOperationException($"Duplicate variable {variableName}");
                    }

                    break;
                }
            case TypePattern { Type: var typeIdentifier, VariableName: var variableName } typePattern:
                {
                    var type = GetTypeReference(typeIdentifier);
                    typePattern.TypeReference = type;

                    ExpectType(type, valueTypeReference, pattern.SourceRange);

                    if (variableName is not null)
                    {
                        var variable = new LocalVariable(CurrentFunctionSignature, variableName, type, Instantiated: false, Mutable: typePattern.IsVariableMutable);
                        typePattern.Variable = variable;
                        patternVariables.Add(variable);
                        if (!TryAddScopedVariable(variableName.StringValue, variable))
                        {
                            throw new InvalidOperationException($"Duplicate variable {variableName}");
                        }
                    }

                    break;
                }
            default:
                throw new UnreachableException(pattern.GetType().Name);
        }

        return patternVariables;
    }
}
