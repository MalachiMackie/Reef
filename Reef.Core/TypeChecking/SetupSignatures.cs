using System.Diagnostics;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{

    private (List<(ProgramClass, ClassSignature)>, List<UnionSignature>) SetupSignatures()
    {
        var classes =
            new List<(ProgramClass, ClassSignature, List<FunctionSignature>, List<TypeField> fields)>();
        var unions = new List<(ProgramUnion, UnionSignature, List<FunctionSignature>, List<IUnionVariant>)>();

        // setup union and class signatures before setting up their functions/fields etc. so that functions and fields can reference other types
        foreach (var union in _program.Unions)
        {
            var variants = new List<IUnionVariant>();
            var functions = new List<FunctionSignature>();
            var typeParameters = new List<GenericPlaceholder>(union.TypeParameters.Count);
            var unionSignature = new UnionSignature
            {
                Id = new DefId(_program.ModuleId, $"{_program.ModuleId}.{union.Name.StringValue}"),
                Name = union.Name.StringValue,
                TypeParameters = typeParameters,
                Functions = functions,
                Variants = variants
            };

            union.Signature = unionSignature;

            if (union.TypeParameters.GroupBy(x => x.StringValue).Any(x => x.Count() > 1))
            {
                throw new InvalidOperationException("Duplicate type parameter");
            }

            typeParameters.AddRange(union.TypeParameters.Select(typeParameter => new GenericPlaceholder
            { GenericName = typeParameter.StringValue, OwnerType = unionSignature }));

            unions.Add((union, unionSignature, functions, variants));

            if (!_types.TryAdd(unionSignature.Name, unionSignature))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeName(union.Name));
            }
        }

        foreach (var klass in _program.Classes)
        {
            var name = klass.Name.StringValue;
            var functions = new List<FunctionSignature>(klass.Functions.Count);
            var fields = new List<TypeField>(klass.Fields.Count);
            var typeParameters = new List<GenericPlaceholder>(klass.TypeParameters.Count);
            var signature = new ClassSignature
            {
                Id = new DefId(_program.ModuleId, $"{_program.ModuleId}.{klass.Name.StringValue}"),
                Name = name,
                TypeParameters = typeParameters,
                Functions = functions,
                Fields = fields,
            };
            typeParameters.AddRange(klass.TypeParameters.Select(typeParameter => new GenericPlaceholder
            { GenericName = typeParameter.StringValue, OwnerType = signature }));

            klass.Signature = signature;

            var typeParametersLookup = klass.TypeParameters.ToLookup(x => x.StringValue);

            foreach (var grouping in typeParametersLookup)
            {
                foreach (var typeParameter in grouping.Skip(1))
                {
                    _errors.Add(TypeCheckerError.DuplicateTypeParameter(typeParameter));
                }
            }

            classes.Add((klass, signature, functions, fields));

            if (!_types.TryAdd(name, signature))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeName(klass.Name));
            }
        }

        foreach (var (union, unionSignature, functions, variants) in unions)
        {
            using var _ = PushScope(
                unionSignature,
                genericPlaceholders: unionSignature.TypeParameters,
                defId: unionSignature.Id);

            foreach (var function in union.Functions)
            {
                if (functions.Any(x => x.Name == function.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.ConflictingFunctionName(function.Name));
                }

                functions.Add(TypeCheckFunctionSignature(new DefId(unionSignature.Id.ModuleId, unionSignature.Id.FullName + "__" + function.Name), function, unionSignature));
            }

            foreach (var variant in union.Variants)
            {
                if (variants.Any(x => x.Name == variant.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.DuplicateVariantName(variant.Name));
                }

                variants.Add(variant switch
                {
                    Core.UnitUnionVariant => new UnitUnionVariant { Name = variant.Name.StringValue },
                    Core.TupleUnionVariant tupleVariant => TypeCheckTupleVariant(tupleVariant),
                    Core.ClassUnionVariant classVariant => TypeCheckUnionClassVariant(classVariant),
                    _ => throw new UnreachableException()
                });

                continue;

                TupleUnionVariant TypeCheckTupleVariant(Core.TupleUnionVariant tupleVariant)
                {
                    if (tupleVariant.TupleMembers.Count == 0)
                    {
                        _errors.Add(TypeCheckerError.EmptyUnionTupleVariant(
                                    unionSignature.Name, variant.Name));
                    }

                    var createFunctionParameters = new OrderedDictionary<string, FunctionSignatureParameter>();

                    var createFunction = new FunctionSignature(
                        new DefId(unionSignature.Id.ModuleId, unionSignature.Id.FullName + $"__Create__{variant.Name.StringValue}"),
                        Token.Identifier($"{unionSignature.Name}__Create__{variant.Name.StringValue}",
                            SourceSpan.Default),
                        [],
                        createFunctionParameters,
                        IsStatic: true,
                        IsMutable: false,
                        [])
                    {
                        OwnerType = unionSignature,
                        ReturnType = new InstantiatedUnion(
                            unionSignature,
                            [
                                .. unionSignature.TypeParameters.Select(x => new GenericTypeReference
                                {
                                    OwnerType = x.OwnerType,
                                    GenericName = x.GenericName,
                                })
                            ])
                    };

                    var tupleMemberTypes = new List<ITypeReference>(tupleVariant.TupleMembers.Count);

                    foreach (var (i, member) in tupleVariant.TupleMembers.Index())
                    {
                        var type = GetTypeReference(member);
                        tupleMemberTypes.Add(type);

                        var parameterName = $"Item{i}";
                        createFunctionParameters.Add(
                            parameterName,
                            new FunctionSignatureParameter(
                                createFunction,
                                Token.Identifier(parameterName, SourceSpan.Default),
                                type,
                                Mutable: false,
                                ParameterIndex: (uint)i));
                    }

                    return new TupleUnionVariant
                    {
                        Name = variant.Name.StringValue,
                        TupleMembers = tupleMemberTypes,
                        CreateFunction = createFunction
                    };
                }

                ClassUnionVariant TypeCheckUnionClassVariant(Core.ClassUnionVariant classVariant)
                {
                    var fields = new List<TypeField>();
                    foreach (var field in classVariant.Fields)
                    {
                        if (fields.Any(x => x.Name == field.Name.StringValue))
                        {
                            _errors.Add(TypeCheckerError.DuplicateFieldInUnionClassVariant(union.Name,
                                classVariant.Name, field.Name));
                        }

                        if (field.AccessModifier is not null)
                        {
                            throw new InvalidOperationException(
                                "Access modifier not allowed on class union variants. All fields are public");
                        }

                        if (field.StaticModifier is not null)
                        {
                            throw new InvalidOperationException("StaticModifier not allowed on class union variants");
                        }

                        var type = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type);

                        field.ResolvedType = type;

                        var typeField = new TypeField
                        {
                            Name = field.Name.StringValue,
                            Type = type,
                            IsMutable = field.MutabilityModifier is { Modifier.Type: TokenType.Mut },
                            IsStatic = false,
                            StaticInitializer = null,
                            IsPublic = true,
                        };
                        fields.Add(typeField);
                    }

                    return new ClassUnionVariant
                    {
                        Fields = fields,
                        Name = classVariant.Name.StringValue
                    };
                }
            }
        }

        foreach (var (klass, classSignature, functions, fields) in classes)
        {
            using var _ = PushScope(classSignature, genericPlaceholders: classSignature.TypeParameters, defId: classSignature.Id);

            foreach (var fn in klass.Functions)
            {
                if (functions.Any(x => x.Name == fn.Name.StringValue))
                {
                    _errors.Add(TypeCheckerError.ConflictingFunctionName(fn.Name));
                }

                // todo: function overloading
                functions.Add(TypeCheckFunctionSignature(
                    new DefId(classSignature.Id.ModuleId, classSignature.Id.FullName + $"__{fn.Name.StringValue}"),
                    fn,
                    classSignature));
            }

            foreach (var field in klass.Fields)
            {
                var type = field.Type is null ? UnknownType.Instance : GetTypeReference(field.Type);
                field.ResolvedType = type;
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = type,
                    IsMutable = field.MutabilityModifier is { Modifier.Type: TokenType.Mut },
                    IsPublic = field.AccessModifier is { Token.Type: TokenType.Pub },
                    IsStatic = field.StaticModifier is { Token.Type: TokenType.Static },
                    StaticInitializer = field.InitializerValue,
                };

                if (fields.Any(y => y.Name == typeField.Name))
                {
                    throw new InvalidOperationException($"Field with name {field.Name} already defined");
                }

                fields.Add(typeField);
            }
        }

        foreach (var fn in _program.Functions)
        {
            var name = fn.Name.StringValue;

            // todo: function overloading
            if (!ScopedFunctions.TryAdd(name, TypeCheckFunctionSignature(new DefId(_program.ModuleId, $"{_program.ModuleId}.{fn.Name.StringValue}"), fn, ownerType: null)))
            {
                _errors.Add(TypeCheckerError.ConflictingFunctionName(fn.Name));
            }
        }

        foreach (var typeParameter in _program.Classes.SelectMany(x => x.TypeParameters)
                     .Concat(_program.Unions.SelectMany(x => x.TypeParameters))
                     .Where(x => _types.ContainsKey(x.StringValue)))
        {
            _errors.Add(TypeCheckerError.TypeParameterConflictsWithType(typeParameter));
        }

        return (
            [.. classes.Select(x => (x.Item1, x.Item2))],
            [.. unions.Select(x => x.Item2)]
        );
    }
}
