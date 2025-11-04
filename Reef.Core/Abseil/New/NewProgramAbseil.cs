using System.Diagnostics;
using Reef.Core.LoweredExpressions.New;

using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil.New;

public partial class NewProgramAbseil
{
    private const string ReturnValueLocalName = "_returnValue";
    private const string LocalsObjectLocalName = "_localsObject";
    private static string ParameterLocalName(uint parameterIndex) => $"_param{parameterIndex}";
    private static string UserDefinedLocalName(uint localIndex) => $"_local{localIndex}";
        
    private readonly Dictionary<
        NewLoweredMethod,
        (
            FunctionSignature fnSignature,
            List<BasicBlock> basicBlocks,
            List<NewMethodLocal> locals,
            IReadOnlyList<Expressions.IExpression> highLevelExpressions,
            NewLoweredConcreteTypeReference? ownerType,
            bool needsLowering
        )> _methods = [];
    private readonly Dictionary<DefId, NewDataType> _types = [];
    private readonly LangProgram _program;
    private NewLoweredConcreteTypeReference? _currentType;
    private (NewLoweredMethod LoweredMethod, FunctionSignature FunctionSignature)? _currentFunction;

    private readonly IReadOnlyList<NewLoweredProgram> _importedPrograms;
    private List<BasicBlock> _basicBlocks = [];
    private List<IStatement> _basicBlockStatements = [];
    private List<NewMethodLocal> _locals = [];

    public static NewLoweredProgram Lower(LangProgram program)
    {
        return new NewProgramAbseil(program).LowerInner();
    }

    private NewProgramAbseil(LangProgram program)
    {
        var importedDataTypes = new List<NewDataType>();
        var printf = FunctionSignature.Printf;
        var importedMethods = new List<NewLoweredMethod>()
        {
            new (
                printf.Id,
                printf.Name,
                [],
                [],
                ReturnValue: new NewMethodLocal(ReturnValueLocalName, null, GetTypeReference(InstantiatedClass.Unit)),
                ParameterLocals: [..printf.Parameters.Select((x, i) => new NewMethodLocal(
                    ParameterLocalName((uint)i), x.Key, GetTypeReference(x.Value.Type)))],
                Locals: [])
        };

        var rawPointerType = new NewLoweredConcreteTypeReference(
                ClassSignature.RawPointer.Name,
                ClassSignature.RawPointer.Id,
                []);

        for (var i = 0; i < 7; i++)
        {
            var fnClass = ClassSignature.Function(i);
            importedDataTypes.Add(
                new NewDataType(
                    fnClass.Id,
                    fnClass.Name,
                    [..fnClass.TypeParameters.Select(GetGenericPlaceholder)],
                    [
                        new NewDataTypeVariant(
                            "_classVariant",
                            [
                                new NewDataTypeField(
                                    "FunctionReference",
                                    new NewLoweredFunctionPointer(
                                        [..fnClass.TypeParameters.SkipLast(1).Select(GetGenericPlaceholder)],
                                        GetTypeReference(fnClass.TypeParameters[^1]))),
                                new NewDataTypeField(
                                    "FunctionParameter",
                                    rawPointerType)
                            ])
                    ],
                    []));

            /*
             * pub class Function`1<TReturn>
             * {
             *     pub field FunctionReference: FnType<TReturn>,
             *     pub field FunctionParameter: Option<Ptr>,
             *
             *     pub fn Call(): TReturn
             *     {
             *         if (FunctionParameter matches Option::Some(ptr)) {
             *           return ((FnType<Ptr, TReturn>)FunctionReference)(ptr);
             *         }
             *         return FunctionReference();
             *     }
             * }
             *
             */

            var call = fnClass.Functions[0];

            importedMethods.Add(
                new NewLoweredMethod(
                    call.Id,
                    $"{fnClass.Name}__{call.Name}",
                    [..fnClass.TypeParameters.Select(GetGenericPlaceholder)],
                    [],
                    ReturnValue: new NewMethodLocal(ReturnValueLocalName, null, GetTypeReference(call.ReturnType)),
                    ParameterLocals: [..call.Parameters.Values.Select((x, j) => new NewMethodLocal(ParameterLocalName((uint)j), x.Name.StringValue, GetTypeReference(x.Type)))],
                    []));
        }

        var errorGeneric = new NewLoweredGenericPlaceholder(
                                    UnionSignature.Result.Id,
                                    "TError");
        var valueGeneric = new NewLoweredGenericPlaceholder(
                                    UnionSignature.Result.Id,
                                    "TValue");
        var intRef = new NewLoweredConcreteTypeReference(
                                    ClassSignature.Int64.Name,
                                    ClassSignature.Int64.Id,
                                    []);
        
        var resultDataType = new NewDataType(
            UnionSignature.Result.Id,
            UnionSignature.Result.Name,
            [..UnionSignature.Result.TypeParameters.Select(GetGenericPlaceholder)],
            [
                new NewDataTypeVariant(
                    "Ok",
                    [
                        new NewDataTypeField(
                            "_variantIdentifier",
                            intRef),
                        new NewDataTypeField(
                            "Item0",
                            valueGeneric)
                    ]),
                new NewDataTypeVariant(
                    "Error",
                    [
                        new NewDataTypeField(
                            "_variantIdentifier",
                            intRef),
                        new NewDataTypeField(
                            "Item0",
                            errorGeneric)
                    ])
            ],
            []);
        importedDataTypes.Add(resultDataType);
        foreach (var variant in UnionSignature.Result.Variants.OfType<TypeChecking.TypeChecker.TupleUnionVariant>())
        {
            importedMethods.Add(
                new NewLoweredMethod(
                    variant.CreateFunction.Id,
                    variant.CreateFunction.Name,
                    resultDataType.TypeParameters,
                    [],
                    new NewMethodLocal(ReturnValueLocalName, null, GetTypeReference(variant.CreateFunction.ReturnType)),
                    [..variant.CreateFunction.Parameters.Values.Select((x, i) => new NewMethodLocal(ParameterLocalName((uint)i), x.Name.StringValue, GetTypeReference(x.Type)))],
                    []));
        }

        _importedPrograms = [
            new NewLoweredProgram()
            {
                DataTypes = importedDataTypes,
                Methods = importedMethods
            }
        ];
        _program = program;
        var a = _currentFunction;
    }

    private NewLoweredProgram LowerInner()
    {
        foreach (var dataType in _program.Unions.Select(x => LowerUnion(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        foreach (var dataType in _program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        var mainSignature = new FunctionSignature(
                DefId.Main(_program.ModuleId),
                Token.Identifier("_Main", SourceSpan.Default),
                [],
                [],
                IsStatic: true,
                IsMutable: false,
                _program.Expressions)
        {
            ReturnType = InstantiatedClass.Unit,
            OwnerType = null,
            LocalVariables = _program.TopLevelLocalVariables,
            LocalFunctions = _program.TopLevelLocalFunctions
        };
        foreach (var local in _program.TopLevelLocalVariables)
        {
            local.ContainingFunction = mainSignature;
        }

        var fnSignaturesToGenerate = _program.Functions.Select(x => x.Signature.NotNull());
        if (mainSignature.Expressions.Count > 0)
        {
            fnSignaturesToGenerate = fnSignaturesToGenerate.Prepend(mainSignature);
        }

        foreach (var fnSignature in fnSignaturesToGenerate)
        {
            var (method, basicBlocks, locals, expressions) = GenerateLoweredMethod(null, fnSignature, null, null);
            _methods.Add(method, (fnSignature, basicBlocks, locals, expressions, null, true));
        }

        foreach (var (method, (fnSignature, basicBlocks, locals, expressions, ownerTypeReference, _)) in _methods.Where(x => x.Value.needsLowering))
        {
            LowerMethod(method, fnSignature, basicBlocks, locals, expressions, ownerTypeReference);

        }

        return new NewLoweredProgram()
        {
            DataTypes = [.._types.Values],
            Methods = [.._methods.Keys]
        };
    }

    private void LowerMethod(
        NewLoweredMethod method,
        FunctionSignature fnSignature,
        List<BasicBlock> basicBlocks,
        List<NewMethodLocal> locals,
        IReadOnlyList<Expressions.IExpression> expressions,
        NewLoweredConcreteTypeReference? ownerTypeReference)
    {
        _currentType = ownerTypeReference;
        _currentFunction = (method, fnSignature);
        _basicBlocks = basicBlocks;
        _locals = locals;
        _basicBlockStatements = [];

        foreach (var expression in expressions)
        {
            NewLowerExpression(expression);
        }
        
        // loweredExpressions.AddRange(expressions.Select(LowerExpression));
        //
        // if (expressions.Count == 0 || !expressions[^1].Diverges)
        // {
        //     loweredExpressions.Add(new MethodReturnExpression(
        //                 new UnitConstantExpression(true)));
        // }
    }

    private NewDataType LowerClass(ClassSignature klass)
    {
        var typeParameters = klass.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var classTypeReference = new NewLoweredConcreteTypeReference(
                klass.Name,
                klass.Id,
                typeParameters);

        var staticFields = klass.Fields.Where(x => x.IsStatic)
            .Select<TypeField, NewStaticDataTypeField>(x =>
            {
                throw new NotImplementedException();
                // return new NewStaticDataTypeField(
                    // x.Name,
                    // GetTypeReference(x.Type),
                    // NewLowerExpression(x.StaticInitializer.NotNull()));
            });

        var fields = klass.Fields.Where(x => !x.IsStatic)
            .Select(x => new NewDataTypeField(x.Name, GetTypeReference(x.Type)));

        foreach (var method in klass.Functions)
        {
            var (loweredMethod, basicBlocks, locals, expressions) = GenerateLoweredMethod(klass.Name, method, classTypeReference, classTypeReference);
            _methods.Add(loweredMethod, (method, basicBlocks, locals, expressions, classTypeReference, true));
        }

        return new NewDataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [new NewDataTypeVariant("_classVariant", [.. fields])],
                [.. staticFields]);
    }

    private NewDataType LowerUnion(UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new NewLoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        foreach (var function in union.Functions)
        {
            var (loweredMethod, basicBlocks, locals, expressions) = GenerateLoweredMethod(union.Name, function, unionTypeReference, unionTypeReference);

            _methods.Add(loweredMethod, (function, basicBlocks, locals, expressions, unionTypeReference, true));
        }

        var variants = new List<NewDataTypeVariant>(union.Variants.Count);
        foreach (var variant in union.Variants)
        {
            var variantIdentifierField = new NewDataTypeField(
                    "_variantIdentifier",
                    GetTypeReference(InstantiatedClass.UInt16));
            var fields = new List<NewDataTypeField>() { variantIdentifierField };
            switch (variant)
            {
                case TypeChecking.TypeChecker.UnitUnionVariant u:
                    break;
                case TypeChecking.TypeChecker.ClassUnionVariant u:
                    {
                        fields.AddRange(u.Fields.Select(x => new NewDataTypeField(
                                        x.Name,
                                        GetTypeReference(x.Type))));
                        break;
                    }
                case TypeChecking.TypeChecker.TupleUnionVariant u:
                    {
                        var memberTypes = u.TupleMembers.NotNull().Select(GetTypeReference).ToArray();
                        fields.AddRange(memberTypes.Select((x, i) => new NewDataTypeField(
                                        $"Item{i}",
                                        x)));

                        throw new NotImplementedException();

                        // var createMethodFieldInitializations = fields.Skip(1).Index().ToDictionary(x => x.Item.Name, x => (ILoweredExpression)new LoadArgumentExpression((uint)x.Index, true, x.Item.Type));
                        // createMethodFieldInitializations["_variantIdentifier"] = new UInt16ConstantExpression(true, (ushort)variants.Count);

                        // List<ILoweredExpression> expressions = [
                        //                 new MethodReturnExpression(
                        //                     new CreateObjectExpression(
                        //                         unionTypeReference,
                        //                         variant.Name,
                        //                         true,
                        //                         createMethodFieldInitializations
                        //                         ))
                        //             ];
                        //
                        // var method = new LoweredMethod(
                        //             u.CreateFunction.Id,
                        //             u.CreateFunction.Name,
                        //             typeParameters,
                        //             memberTypes,
                        //             unionTypeReference,
                        //             expressions,
                        //             []);
                        //
                        // // add the tuple variant as a method
                        // _methods.Add(
                        //         method,
                        //         // pass null as the signature because it's never used as the current function
                        //         (null!, expressions, [], unionTypeReference, false));
                        // break;
                    }
                default:
                    throw new InvalidOperationException($"Invalid union variant {variant}");
            }
            variants.Add(new NewDataTypeVariant(
                        variant.Name,
                        fields));
        }

        return new NewDataType(
                union.NotNull().Id,
                union.Name,
                typeParameters,
                Variants: variants,
                StaticFields: []);
    }

    // instead of lowering the methods expressions right here, we return the list of expressions 
    // to add to later so that all the type and function references are available to be used
    private (NewLoweredMethod, List<BasicBlock>, List<NewMethodLocal>, IReadOnlyList<Expressions.IExpression>) GenerateLoweredMethod(
            string? ownerName,
            FunctionSignature fnSignature,
            NewLoweredConcreteTypeReference? ownerTypeReference,
            NewLoweredConcreteTypeReference? parentTypeReference)
    {
        var name = ownerName is null
            ? fnSignature.Name
            : $"{ownerName}__{fnSignature.Name}";

        var localsAccessedInClosure = fnSignature.LocalVariables.Where(x => x.ReferencedInClosure).ToArray();
        var parametersAccessedInClosure = fnSignature.Parameters.Values.Where(x => x.ReferencedInClosure).ToArray();
        NewDataType? localsType = null;
        if (localsAccessedInClosure.Length > 0 || parametersAccessedInClosure.Length > 0)
        {
            localsType = new NewDataType(
                new DefId(fnSignature.Id.ModuleId, fnSignature.Id.FullName + "__Locals"),
                $"{name}__Locals",
                [],
                [
                    new NewDataTypeVariant(
                        "_classVariant",
                        [
                            ..localsAccessedInClosure
                                .Concat(parametersAccessedInClosure.Cast<IVariable>())
                                .Select(
                                    x => new NewDataTypeField(
                                        x.Name.StringValue,
                                        GetTypeReference(x.Type))),
                        ])
                ],
                []);
            _types.Add(localsType.Id, localsType);

            fnSignature.LocalsTypeId = localsType.Id;
        }

        NewDataType? closureType = null;
        if (fnSignature.AccessedOuterVariables.Count > 0)
        {
            var fields = new Dictionary<DefId, NewDataTypeField>();

            foreach (var variable in fnSignature.AccessedOuterVariables)
            {
                switch (variable)
                {
                    case LocalVariable localVariable:
                        {
                            var containingFunction = localVariable.ContainingFunction.NotNull();
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull(expectedReason: "the containing function containing the referenced local should have already been lowered");
                            var localType = _types[localTypeId];
                            var localTypeReference = new NewLoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            // try to add because we might have already added this function's 
                            // local type if we're referencing multiple variables from
                            // the owner function
                            fields.TryAdd(
                                localTypeId,
                                new NewDataTypeField(localType.Name, localTypeReference)); 
                            break;
                        }
                    case FunctionSignatureParameter parameterVariable:
                        {
                            var containingFunction = parameterVariable.ContainingFunction.NotNull(); 
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull();
                            var localType = _types[localTypeId];
                            var localTypeReference = new NewLoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            // try to add because we might have already added this function's 
                            // local type if we're referencing multiple variables from
                            // the owner function
                            fields.TryAdd(
                                    localTypeId,
                                    new NewDataTypeField(localType.Name, localTypeReference));
                            break;
                        }
                    case FieldVariable fieldVariable:
                        {
                            var signature = fieldVariable.ContainingSignature;
                            var typeReference = new NewLoweredConcreteTypeReference(
                                    signature.Name,
                                    signature.Id,
                                    [..signature.TypeParameters
                                        .Select(GetTypeReference)]);

                            fields.TryAdd(
                                signature.Id,
                                new NewDataTypeField("this", typeReference));

                            break;
                        }
                    case ThisVariable thisVariable:
                        {
                            Debug.Assert(parentTypeReference is not null);
                            fields.TryAdd(
                                parentTypeReference.DefinitionId,
                                new NewDataTypeField("this", parentTypeReference));
                            break;
                        }
                }
            }

            closureType = new NewDataType(
                new DefId(fnSignature.Id.ModuleId, fnSignature.Id.FullName + "__Closure"),
                $"{name}__Closure",
                [],
                [
                    new NewDataTypeVariant(
                        "_classVariant",
                        [..fields.Values])
                ],
                []);

            fnSignature.ClosureTypeId = closureType.Id;

            _types.Add(closureType.Id, closureType);
        }

        foreach (var localSignature in fnSignature.LocalFunctions)
        {
            var (localMethod, localFnBasicBlocks, localFnLocals, localExpressions) = 
                GenerateLoweredMethod(ownerName: name, localSignature, null, parentTypeReference);

            _methods.Add(localMethod, (localSignature, localFnBasicBlocks, localFnLocals, localExpressions, parentTypeReference, true));
        }

        var locals = new List<NewMethodLocal>(
                fnSignature.LocalVariables.Count
                - localsAccessedInClosure.Length
                + (localsAccessedInClosure.Length > 0 ? 1 : 0));
        var basicBlocks = new List<BasicBlock>();

        if (localsType is not null)
        {
            var localsTypeReference = new NewLoweredConcreteTypeReference(
                            localsType.Name,
                            localsType.Id,
                            []);

            locals.Add(new NewMethodLocal(
                        LocalsObjectLocalName,
                        null,
                        localsTypeReference));
            var hasCompilerInsertedFirstParameter = 
                (!fnSignature.IsStatic && ownerTypeReference is not null)
                || closureType is not null;


            throw new NotImplementedException();
            // var fieldInitializers = new Dictionary<string, ILoweredExpression>();
            // foreach (var parameter in parametersAccessedInClosure)
            // {
            //     
            //     fieldInitializers.Add(
            //             parameter.Name.StringValue,
            //             new LoadArgumentExpression(
            //                 (uint)(parameter.ParameterIndex
            //                 + (hasCompilerInsertedFirstParameter ? 1 : 0)),
            //                 true,
            //                 GetTypeReference(parameter.Type)));
            // }
            //
            // expressions.Add(
            //     new VariableDeclarationAndAssignmentExpression(
            //         "__locals",
            //         new CreateObjectExpression(
            //             localsTypeReference,
            //             "_classVariant",
            //             true,
            //             fieldInitializers),
            //         false));

        }

        locals.AddRange(fnSignature.LocalVariables.Where(x => !x.ReferencedInClosure)
                .Select((x, i) => new NewMethodLocal(UserDefinedLocalName((uint)i), x.Name.StringValue, GetTypeReference(x.Type))));

        var parameters = fnSignature.Parameters.Select(y =>
            (userGivenName: (string?)y.Key, paramType: GetTypeReference(y.Value.Type)));

        if (!fnSignature.IsStatic && ownerTypeReference is not null)
        {
            parameters = parameters.Prepend((null, ownerTypeReference));
        }
        else if (closureType is not null)
        {
            parameters = parameters.Prepend((null, new NewLoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                [])));
        }

        return (
            new NewLoweredMethod(
                fnSignature.Id,
                name,
                [
                    .. fnSignature.TypeParameters.Select(GetGenericPlaceholder),
                    .. ownerTypeReference?.TypeArguments.Select(x => (x as NewLoweredGenericPlaceholder).NotNull()) ?? []
                ],
                basicBlocks,
                new NewMethodLocal(ReturnValueLocalName, null, GetTypeReference(fnSignature.ReturnType)),
                locals,
                [.. parameters.Select((x, i) => new NewMethodLocal(ParameterLocalName((uint)i), x.userGivenName, x.paramType))]),
            basicBlocks,
            locals,
            fnSignature.Expressions);
    }

    private NewLoweredGenericPlaceholder GetGenericPlaceholder(GenericPlaceholder placeholder)
    {
        return new NewLoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
    }

    private NewDataType GetDataType(
            DefId typeId,
            IReadOnlyList<INewLoweredTypeReference> typeArguments)
    {
        if (_types.TryGetValue(typeId, out var dataType))
            return dataType;

        return _importedPrograms.Select(x => x.DataTypes.FirstOrDefault(y => y.Id == typeId))
            .FirstOrDefault(x => x is not null)
            ?? throw new InvalidOperationException($"No data type with id {typeId} was found");
    }

    private NewLoweredFunctionReference GetFunctionReference(
            DefId functionId,
            IReadOnlyList<INewLoweredTypeReference> typeArguments,
            IReadOnlyList<INewLoweredTypeReference> ownerTypeArguments)
    {
        var loweredMethod = _methods.Keys.FirstOrDefault(x => x.Id == functionId)
            ?? _importedPrograms.SelectMany(x => x.Methods)
                .First(x => x.Id == functionId);

        IReadOnlyList<INewLoweredTypeReference> resultingTypeArguments = [..typeArguments, ..ownerTypeArguments];
        
        Debug.Assert(resultingTypeArguments.Count == loweredMethod.TypeParameters.Count);

        return new(
                loweredMethod.Name,
                functionId,
                resultingTypeArguments);
    }

    private INewLoweredTypeReference GetTypeReference(ITypeReference typeReference)
    {
        return typeReference switch
        {
            InstantiatedClass c => new NewLoweredConcreteTypeReference(
                c.Signature.Name,
                c.Signature.Id,
                [.. c.TypeArguments.Select(GetTypeReference)]),
            InstantiatedUnion u => new NewLoweredConcreteTypeReference(
                u.Signature.Name,
                u.Signature.Id,
                [.. u.TypeArguments.Select(GetTypeReference)]),
            GenericTypeReference {ResolvedType: {} resolvedType} => GetTypeReference(resolvedType),
            GenericTypeReference {ResolvedType: null} g => new NewLoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            GenericPlaceholder g => new NewLoweredGenericPlaceholder(
                g.OwnerType.Id,
                g.GenericName),
            UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            FunctionObject f => FunctionObjectCase(f),
            UnspecifiedSizedIntType i => GetTypeReference(i.ResolvedIntType.NotNull()),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };

        NewLoweredConcreteTypeReference FunctionObjectCase(FunctionObject f)
        {
            var type = _importedPrograms
                .SelectMany(x => x.DataTypes.Where(y => y.Name == $"Function`{f.Parameters.Count + 1}"))
                .First();
            
            return new NewLoweredConcreteTypeReference(
                type.Name,
                type.Id,
                [..f.Parameters.Select(x => GetTypeReference(x.Type))
                    .Append(GetTypeReference(f.ReturnType))]);
        }
    }

    private static bool EqualTypeReferences(INewLoweredTypeReference a, INewLoweredTypeReference b)
    {
        return (a, b) switch
        {
            (NewLoweredConcreteTypeReference concreteA, NewLoweredConcreteTypeReference concreteB)
                when concreteA.DefinitionId == concreteB.DefinitionId
                && concreteA.TypeArguments.Zip(concreteB.TypeArguments)
                    .All(x => EqualTypeReferences(x.First, x.Second)) => true,
            (NewLoweredGenericPlaceholder genericA, NewLoweredGenericPlaceholder genericB)
                when genericA.OwnerDefinitionId == genericB.OwnerDefinitionId => true,
            _ => false
        };
    }
}