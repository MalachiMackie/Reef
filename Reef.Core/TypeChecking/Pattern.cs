using System.Diagnostics;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private List<LocalVariable> TypeCheckPattern(ITypeReference valueTypeReference, IPattern pattern)
    {
        var patternVariables = new List<LocalVariable>();
        switch (pattern)
        {
            case DiscardPattern discardPattern:
                discardPattern.TypeReference = Never();

                // discard pattern always type checks
                break;
            case UnionVariantPattern variantPattern:
                {
                    var patternUnionType = GetTypeReference(variantPattern.Type).ConcreteType();
                    variantPattern.TypeReference = patternUnionType;

                    if (patternUnionType is UnknownType)
                    {
                        break;
                    }

                    ExpectType(valueTypeReference, patternUnionType, variantPattern.SourceRange);

                    var unionSignature = patternUnionType switch
                    {
                        InstantiatedUnion x => x.Signature,
                        VariantOfType(var x) => x.Signature,
                        SelfTypeReference{ Signature: UnionSignature signature } => signature,
                        _ => throw new InvalidOperationException($"{patternUnionType} is not a union")
                    };

                    if (variantPattern.VariantName is not null)
                    {
                        if (unionSignature.Variants.All(x => x.Name != variantPattern.VariantName.StringValue))
                        {
                            AddError(TypeCheckerError.UnknownTypeMember(variantPattern.VariantName, unionSignature.Name));
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
                        AddError(TypeCheckerError.NonClassUsedInClassPattern(classPattern.Type));
                        break;
                    }

                    if (classPattern.FieldPatterns.GroupBy(x => x.FieldName.StringValue).Any(x => x.Count() > 1))
                    {
                        throw new InvalidOperationException("Duplicate fields found");
                    }

                    var remainingFields = classType.GetFields()
                        .Where(x => x.IsPublic || CanAccessPrivateMembers(classType.Signature))
                        .Select(x => x.Name)
                        .ToHashSet();

                    foreach (var fieldPattern in classPattern.FieldPatterns)
                    {
                        remainingFields.Remove(fieldPattern.FieldName.StringValue);
                        if (classType.GetFields().FirstOrDefault(x => x.Name == fieldPattern.FieldName.StringValue) is not { } field)
                        {
                            AddError(TypeCheckerError.UnknownTypeMember(fieldPattern.FieldName, classType.Signature.Name));
                            continue;
                        }

                        if (!field.IsPublic && !CanAccessPrivateMembers(classType.Signature))
                        {
                            AddError(TypeCheckerError.PrivateMemberReferenced(fieldPattern.FieldName));
                        }

                        if (field.IsStatic)
                        {
                            AddError(TypeCheckerError.StaticFieldInClassPattern(fieldPattern.FieldName));
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
                        AddError(TypeCheckerError.MissingFieldsInClassPattern(remainingFields, classPattern.Type));
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

                    var union = patternType switch
                    {
                        InstantiatedUnion x => x,
                        VariantOfType(var x) => x,
                        SelfTypeReference{ Signature: UnionSignature signature } => InstantiateUnion(signature, [], null, SourceRange.Default),
                        _ => throw new InvalidOperationException($"{patternType} is not a union")
                    };

                    var variant = GetUnionVariant(union, classVariantPattern.VariantName.StringValue, UInt64())
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
                        classVariantPattern.FieldPatterns.Count < classVariant.Fields.Count)
                    {
                        AddError(TypeCheckerError.MissingFieldsInUnionClassVariantPattern(
                            classVariantPattern,
                            classVariant.Fields.Select(x => x.Name).Except(classVariantPattern.FieldPatterns.Select(x => x.FieldName.StringValue))));
                    }

                    foreach (var fieldPattern in classVariantPattern.FieldPatterns)
                    {
                        var fieldType = classVariant.Fields.FirstOrDefault(x => x.Name == fieldPattern.FieldName.StringValue)?.Type;
                        if (fieldType is null)
                        {
                            AddError(TypeCheckerError.SymbolNotFound(fieldPattern.FieldName));
                            continue;
                        }

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

                    if (patternType is UnknownType)
                    {
                        break;
                    }

                    var unionType = patternType switch
                    {
                        InstantiatedUnion x => x,
                        VariantOfType(var x) => x,
                        SelfTypeReference { Signature: UnionSignature signature } => InstantiateUnion(signature, [], null, SourceRange.Default),
                        _ => throw new InvalidOperationException($"{patternType} is not a union")
                    };

                    var variant = GetUnionVariant(unionType, unionTupleVariantPattern.VariantName.StringValue, UInt64())
                        ?? throw new InvalidOperationException(
                                                          $"No union variant found with name {unionTupleVariantPattern.VariantName.StringValue}");

                    if (variant is not TupleUnionVariant tupleUnionVariant)
                    {
                        throw new InvalidOperationException("Expected union to be a tuple variant");
                    }

                    if (tupleUnionVariant.TupleMembers.Count != unionTupleVariantPattern.TupleParamPatterns.Count)
                    {
                        AddError(TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(
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
                    variableDeclarationPattern.TypeReference = Never();
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
