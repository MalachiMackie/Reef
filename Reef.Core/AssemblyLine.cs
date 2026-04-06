using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Reef.Core.LoweredExpressions;
using Reef.Core.TypeChecking;
using Index = Reef.Core.LoweredExpressions.Index;

namespace Reef.Core;

public record AssemblyDefId(DefId Id, IReadOnlyList<AssemblyDefId> TypeArguments);

#pragma warning disable CS9113 // Parameter is unread.
public partial class AssemblyLine(IReadOnlyList<LoweredModule> modules, HashSet<DefId> usefulMethodIds, ILogger logger)
#pragma warning restore CS9113 // Parameter is unread.
{
    private const string AsmHeader = """
                                     bits 64
                                     default rel

                                     """;

    private readonly StringBuilder _dataSegment = new("segment .data\n");
    private readonly StringBuilder _stringDataSubSegment = new();
    private readonly StringBuilder _typeInfoDataSubSegment = new();
    private readonly StringBuilder _methodInfoDataSubSegment = new();

    private readonly StringBuilder _codeSegment = new("""
                                                      segment .text
                                                      global main
                                                      extern ExitProcess
                                                      extern _CRT_INIT
                                                      extern init_runtime

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

    private ulong GetTypeId(ILoweredTypeReference type, bool currentlyWritingTypeInfo)
    {
        // todo: _currentTypeArguments I suspect is incorrect here
        var found = _typeIds.FirstOrDefault(x => AreTypeReferencesEqual(x.TypeReference, type, _currentTypeArguments));
        if (found == default)
        {
            var typeId = (ulong)_typeIds.Count;
            _typeIds.Add((type, typeId));
            if (currentlyWritingTypeInfo)
            {
                _typesToWriteBlobInfo.Enqueue((type, typeId));
            }
            else
            {
                WriteTypeInfoBlob(type, typeId);
            }
            return typeId;
        }

        return found.TypeId;

    }

    private readonly List<(LoweredFunctionReference FunctionReference, ulong MethodId)> _methodIds = [];

    private readonly List<(ILoweredTypeReference TypeReference, ulong TypeId)> _typeIds = [];
    private readonly Queue<(ILoweredTypeReference TypeReference, ulong TypeId)> _typesToWriteBlobInfo = [];

    private void WriteMethodInfoBlob(LoweredMethod method, ulong methodId)
    {
        var methodInfoReference = new LoweredConcreteTypeReference(
            TypeChecker.ClassSignature.MethodInfo.Value.Name,
            TypeChecker.ClassSignature.MethodInfo.Value.Id,
            []
        );

        var methodInfoSize = GetTypeSize(methodInfoReference, []);
        var bytesWritten = 0u;

        _methodInfoDataSubSegment.AppendLine($"        ; MethodInfo: {method.Name}");
        _methodInfoDataSubSegment.AppendLine($"        ; MethodInfo.Id");
        _methodInfoDataSubSegment.AppendLine($"        dd 0x{methodId:X}");
        bytesWritten += 4;

        _methodInfoDataSubSegment.AppendLine($"        ; MethodInfo.Name");
        PadAlignment(methodInfoSize.VariantFieldOffsets["_classVariant"]["Name"].Alignment);
        _methodInfoDataSubSegment.AppendLine($"        dq 0x{method.Name.Length:X}");
        bytesWritten += 8;
        _methodInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(method.Name)}");
        bytesWritten += 8;

        Debug.Assert(bytesWritten == methodInfoSize.Size);

        void PadAlignment(uint alignment)
        {
            _methodInfoDataSubSegment.AppendLine($"        ALIGN {alignment}, db 0");
            if (bytesWritten == 0)
            {
                // nothing will have been aligned, so nothing to increment
                _methodInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                return;
            }
            if (bytesWritten < alignment)
            {
                bytesWritten = alignment;
            }
            else
            {
                bytesWritten += bytesWritten % alignment;
            }

            _methodInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
        }
    }

    private void WriteTypeInfoBlob(ILoweredTypeReference type, ulong typeId)
    {
        if (type is LoweredGenericPlaceholder outerGenericPlaceholder)
        {
            WriteTypeInfoBlob(_currentTypeArguments[outerGenericPlaceholder.PlaceholderName], typeId);
            return;
        }

        var typeInfoDataType = _dataTypes[DefId.TypeInfo];
        var typeInfoTypeReference = new LoweredConcreteTypeReference(
                        TypeChecker.InstantiatedUnion.TypeInfo.Signature.Name,
                        TypeChecker.InstantiatedUnion.TypeInfo.Signature.Id,
                        []);
        var typeInfoSize = GetTypeSize(typeInfoTypeReference, []);

        var bytesWritten = 0u;
        PadAlignment(typeInfoSize.Alignment);

        _typeInfoDataSubSegment.AppendLine($"        ; TypeInfo: {type}");

        switch (type)
        {
            case LoweredConcreteTypeReference concrete:
                {
                    var dataType = _dataTypes[concrete.DefinitionId];

                    var fieldInfoDataType = _dataTypes[DefId.FieldInfo];
                    var variantInfoDataType = _dataTypes[DefId.VariantInfo];

                    var fieldInfoTypeReference = new LoweredConcreteTypeReference(
                                    TypeChecker.InstantiatedClass.FieldInfo.Signature.Name,
                                    TypeChecker.InstantiatedClass.FieldInfo.Signature.Id,
                                    []);
                    var variantInfoTypeReference = new LoweredConcreteTypeReference(
                                    TypeChecker.InstantiatedClass.VariantInfo.Signature.Name,
                                    TypeChecker.InstantiatedClass.VariantInfo.Signature.Id,
                                    []);

                    var fieldInfoSize = GetTypeSize(fieldInfoTypeReference, []);
                    var fieldFieldOffsets = fieldInfoSize.VariantFieldOffsets["_classVariant"];
                    var variantInfoSize = GetTypeSize(variantInfoTypeReference, []);
                    var variantFieldOffsets = variantInfoSize.VariantFieldOffsets["_classVariant"];

                    Debug.Assert(fieldInfoDataType.Variants.Count == 1);
                    Debug.Assert(variantInfoDataType.Variants.Count == 1);

                    var fieldInfoVariant = fieldInfoDataType.Variants[0];
                    var variantInfoVariant = variantInfoDataType.Variants[0];

                    if (dataType.Variants.Count == 1 && dataType.Variants[0].Name == "_classVariant")
                    {
                        var classVariantFieldOffsets = typeInfoSize.VariantFieldOffsets["Class"];

                        // class
                        var (classVariantIndex, typeInfoVariant) = typeInfoDataType.Variants.Index().First(x => x.Item.Name == "Class");
                        Debug.Assert(typeInfoVariant.Fields.Count == 5);
                        var (variantIdentifierIndex, variantIdenitfierName) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "_variantIdentifier");
                        var (nameIndex, nameField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "Name");
                        var (typeIdIndex, typeIdField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "TypeId");
                        var (staticFieldsIndex, staticFieldsField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "StaticFields");
                        var (fieldsIndex, fieldsField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "Fields");
                        Debug.Assert(variantIdentifierIndex == 0);
                        Debug.Assert(nameIndex == 1);
                        Debug.Assert(typeIdIndex == 2);
                        Debug.Assert(staticFieldsIndex == 3);
                        Debug.Assert(fieldsIndex == 4);

                        // variantIdentifier
                        Debug.Assert(classVariantFieldOffsets["_variantIdentifier"].Offset == 0);
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.VariantIdentifier");
                        _typeInfoDataSubSegment.AppendLine($"        dw 0x{classVariantIndex:X}");
                        bytesWritten += 2;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        // name
                        PadAlignment(classVariantFieldOffsets["Name"].Alignment);
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.Name.Length");
                        _typeInfoDataSubSegment.AppendLine($"        dq 0x{dataType.Name.Length:X}");
                        bytesWritten += 8;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.Name.Ref");
                        _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(dataType.Name)}");
                        bytesWritten += 8;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        // typeId
                        PadAlignment(classVariantFieldOffsets["TypeId"].Alignment);
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.Id.Value");
                        _typeInfoDataSubSegment.AppendLine($"        dd 0x{typeId:X}");
                        bytesWritten += 4;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        // static fields
                        PadAlignment(classVariantFieldOffsets["StaticFields"].Alignment);
                        var classStaticFieldIndex = 0;
                        foreach (var staticField in dataType.StaticFields)
                        {
                            PadAlignment(fieldInfoSize.Alignment);
                            var fieldType = staticField.Type;
                            if (fieldType is LoweredGenericPlaceholder genericPlaceholder
                                && genericPlaceholder.OwnerDefinitionId == concrete.DefinitionId)
                            {
                                var typeArgumentIndex = dataType.TypeParameters.Index()
                                    .First(x => x.Item.PlaceholderName == genericPlaceholder.PlaceholderName).Index;
                                fieldType = concrete.TypeArguments[typeArgumentIndex];
                            }
                            var staticFieldTypeId = GetTypeId(fieldType, currentlyWritingTypeInfo: true);

                            // name
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}].Name.Length");
                            _typeInfoDataSubSegment.AppendLine($"        dq 0x{staticField.Name.Length:X}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}].Name.Ref");
                            _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(staticField.Name)}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            // typeId
                            PadAlignment(fieldFieldOffsets["TypeId"].Alignment);
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}].TypeId.ModuleId.Length");
                            _typeInfoDataSubSegment.AppendLine($"        dd 0x{staticFieldTypeId:X}");
                            bytesWritten += 4;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            classStaticFieldIndex++;
                        }
                        for (; classStaticFieldIndex < 10; classStaticFieldIndex++)
                        {
                            PadAlignment(fieldInfoSize.Alignment);
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}]");
                            _typeInfoDataSubSegment.AppendLine($"        times {fieldInfoSize.Size} db 0");
                            bytesWritten += fieldInfoSize.Size;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                        }

                        // fields
                        PadAlignment(classVariantFieldOffsets["Fields"].Alignment);
                        var fieldIndex = 0;
                        foreach (var field in dataType.Variants[0].Fields)
                        {
                            PadAlignment(fieldInfoSize.Alignment);
                            // name
                            _typeInfoDataSubSegment.AppendLine($"        ; Fields[{fieldIndex}].Name.Length");
                            _typeInfoDataSubSegment.AppendLine($"        dq 0x{field.Name.Length:X}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                            _typeInfoDataSubSegment.AppendLine($"        ; Fields[{fieldIndex}].Name.Ref");
                            _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(field.Name)}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            // typeId
                            PadAlignment(fieldFieldOffsets["TypeId"].Alignment);
                            var fieldType = field.Type;
                            if (fieldType is LoweredGenericPlaceholder genericPlaceholder
                                && genericPlaceholder.OwnerDefinitionId == concrete.DefinitionId)
                            {
                                var typeArgumentIndex = dataType.TypeParameters.Index()
                                    .First(x => x.Item.PlaceholderName == genericPlaceholder.PlaceholderName).Index;
                                fieldType = concrete.TypeArguments[typeArgumentIndex];
                            }
                            _typeInfoDataSubSegment.AppendLine($"        ; Fields[{fieldIndex}].TypeId.Value");
                            _typeInfoDataSubSegment.AppendLine($"        dq 0x{GetTypeId(fieldType, currentlyWritingTypeInfo: true):X}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            fieldIndex++;
                        }
                        for (; fieldIndex < 10; fieldIndex++)
                        {
                            PadAlignment(fieldInfoSize.Alignment);
                            _typeInfoDataSubSegment.AppendLine($"        ; Fields[{fieldIndex}]");
                            _typeInfoDataSubSegment.AppendLine($"        times {fieldInfoSize.Size} db 0");
                            bytesWritten += fieldInfoSize.Size;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                        }
                    }
                    else
                    {
                        // union
                        var unionVariantFieldOffsets = typeInfoSize.VariantFieldOffsets["Union"];
                        var (unionVariantIndex, typeInfoVariant) = typeInfoDataType.Variants.Index().First(x => x.Item.Name == "Union");
                        Debug.Assert(typeInfoVariant.Fields.Count == 6);

                        var (variantIdentifierIndex, variantIdenitfierName) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "_variantIdentifier");
                        var (nameIndex, nameField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "Name");
                        var (typeIdIndex, typeIdField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "TypeId");
                        var (staticFieldsIndex, staticFieldsField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "StaticFields");
                        var (variantsIndex, variantsField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "Variants");
                        var (variantIdentifierGetterIndex, variantIdentifierGetterField) = typeInfoVariant.Fields.Index().First(x => x.Item.Name == "VariantIdentifierGetter");
                        Debug.Assert(variantIdentifierIndex == 0);
                        Debug.Assert(nameIndex == 1);
                        Debug.Assert(typeIdIndex == 2);
                        Debug.Assert(staticFieldsIndex == 3);
                        Debug.Assert(variantsIndex == 4);
                        Debug.Assert(variantIdentifierGetterIndex == 5);

                        // variantIdentifier
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.VariantIdentifier");
                        _typeInfoDataSubSegment.AppendLine($"        dw 0x{unionVariantIndex:X}");
                        bytesWritten += 2;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        // name
                        PadAlignment(unionVariantFieldOffsets["Name"].Alignment);
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.Name.Length");
                        _typeInfoDataSubSegment.AppendLine($"        dq 0x{dataType.Name.Length:X}");
                        bytesWritten += 8;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.Name.Ref");
                        _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(dataType.Name)}");
                        bytesWritten += 8;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        // typeId
                        PadAlignment(unionVariantFieldOffsets["TypeId"].Alignment);
                        _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.TypeId.Value");
                        _typeInfoDataSubSegment.AppendLine($"        dd 0x{typeId:X}");
                        bytesWritten += 4;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        // static fields
                        PadAlignment(unionVariantFieldOffsets["StaticFields"].Alignment);
                        var classStaticFieldIndex = 0;
                        foreach (var staticField in dataType.StaticFields)
                        {
                            var fieldType = staticField.Type;
                            if (fieldType is LoweredGenericPlaceholder genericPlaceholder
                                && genericPlaceholder.OwnerDefinitionId == concrete.DefinitionId)
                            {
                                var typeArgumentIndex = dataType.TypeParameters.Index()
                                    .First(x => x.Item.PlaceholderName == genericPlaceholder.PlaceholderName).Index;
                                fieldType = concrete.TypeArguments[typeArgumentIndex];
                            }
                            var staticFieldTypeId = GetTypeId(fieldType, currentlyWritingTypeInfo: true);
                            PadAlignment(fieldInfoSize.Alignment);

                            // name
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}].Name.Length");
                            _typeInfoDataSubSegment.AppendLine($"        dq 0x{staticField.Name.Length:X}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}].Name.Ref");
                            _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(staticField.Name)}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            // typeId
                            PadAlignment(fieldFieldOffsets["TypeId"].Alignment);
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}].TypeId.ModuleId.Length");
                            _typeInfoDataSubSegment.AppendLine($"        dd 0x{staticFieldTypeId:X}");
                            bytesWritten += 4;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            classStaticFieldIndex++;
                        }
                        for (; classStaticFieldIndex < 10; classStaticFieldIndex++)
                        {
                            PadAlignment(fieldInfoSize.Alignment);
                            _typeInfoDataSubSegment.AppendLine($"        ; StaticFields[{classStaticFieldIndex}]");
                            _typeInfoDataSubSegment.AppendLine($"        times {fieldInfoSize.Size} db 0");
                            bytesWritten += fieldInfoSize.Size;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                        }

                        PadAlignment(unionVariantFieldOffsets["Variants"].Alignment);
                        var currentVariantIndex = 0;
                        foreach (var variant in dataType.Variants)
                        {
                            PadAlignment(variantInfoSize.Alignment);

                            // name
                            _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}].Name.Length");
                            _typeInfoDataSubSegment.AppendLine($"        dq 0x{variant.Name.Length:X}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                            _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}].Name.Ref");
                            _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(variant.Name)}");
                            bytesWritten += 8;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                            // fields
                            PadAlignment(variantFieldOffsets["Fields"].Alignment);
                            var fieldIndex = 0;
                            foreach (var field in variant.Fields)
                            {
                                PadAlignment(fieldInfoSize.Alignment);

                                // name
                                _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}].Fields[{fieldIndex}].Name.Length");
                                _typeInfoDataSubSegment.AppendLine($"        dq 0x{field.Name.Length:X}");
                                bytesWritten += 8;
                                _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                                _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}].Fields[{fieldIndex}].Name.Ref");
                                _typeInfoDataSubSegment.AppendLine($"        dq {GetStringConstantLabel(field.Name)}");
                                bytesWritten += 8;
                                _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                                // typeId

                                var fieldType = field.Type;
                                if (fieldType is LoweredGenericPlaceholder genericPlaceholder
                                    && genericPlaceholder.OwnerDefinitionId == concrete.DefinitionId)
                                {
                                    var typeArgumentIndex = dataType.TypeParameters.Index()
                                        .First(x => x.Item.PlaceholderName == genericPlaceholder.PlaceholderName).Index;
                                    fieldType = concrete.TypeArguments[typeArgumentIndex];
                                }

                                PadAlignment(fieldFieldOffsets["TypeId"].Alignment);
                                _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}].Fields[{fieldIndex}].TypeId.Value");
                                _typeInfoDataSubSegment.AppendLine($"        dq 0x{GetTypeId(fieldType, currentlyWritingTypeInfo: true):X}");
                                bytesWritten += 8;
                                _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                                fieldIndex++;
                            }
                            for (; fieldIndex < 10; fieldIndex++)
                            {
                                PadAlignment(fieldInfoSize.Alignment);
                                _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}].Fields[{fieldIndex}]");
                                _typeInfoDataSubSegment.AppendLine($"        times {fieldInfoSize.Size} db 0");
                                bytesWritten += fieldInfoSize.Size;
                                _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                            }
                            currentVariantIndex++;
                        }
                        for (; currentVariantIndex < 10; currentVariantIndex++)
                        {
                            PadAlignment(variantInfoSize.Alignment);
                            _typeInfoDataSubSegment.AppendLine($"        ; Variants[{currentVariantIndex}]");
                            _typeInfoDataSubSegment.AppendLine($"        times {variantInfoSize.Size} db 0");
                            bytesWritten += variantInfoSize.Size;
                            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                        }

                        // variant identifier getter
                        PadAlignment(unionVariantFieldOffsets["VariantIdentifierGetter"].Alignment);
                        _typeInfoDataSubSegment.AppendLine($"        ; VariantIdentifierGetter.FunctionReference");
                        _typeInfoDataSubSegment.AppendLine("        dq __variant_identifier_field_getter");
                        bytesWritten += 8;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                        _typeInfoDataSubSegment.AppendLine($"        ; VariantIdentifierGetter.FunctionParameter");
                        _typeInfoDataSubSegment.AppendLine("        dq 0");
                        bytesWritten += 8;
                        _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                    }

                    break;
                }
            case LoweredPointer pointer:
                {
                    var pointerToVariantFieldOffsets = typeInfoSize.VariantFieldOffsets["Pointer"];
                    var (pointerToVariantIndex, typeInfoVariant) = typeInfoDataType.Variants.Index().First(x => x.Item.Name == "Pointer");

                    // variantIdentifier
                    _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.VariantIdentifier");
                    _typeInfoDataSubSegment.AppendLine($"        dw 0x{pointerToVariantIndex:X}");
                    bytesWritten += 2;
                    _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                    // pointerTo
                    PadAlignment(pointerToVariantFieldOffsets["PointerTo"].Alignment);
                    _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.PointerTo.Value");
                    _typeInfoDataSubSegment.AppendLine($"        dd 0x{GetTypeId(pointer.PointerTo, currentlyWritingTypeInfo: true):X}");
                    bytesWritten += 4;
                    _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                    break;
                }
            case LoweredArray array:
                {
                    var arrayVariantFieldOffsets = typeInfoSize.VariantFieldOffsets["Array"];
                    var (arrayVariantIndex, typeInfoVariant) = typeInfoDataType.Variants.Index().First(x => x.Item.Name == "Array");

                    // variantIdentifier
                    _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.VariantIdentifier");
                    _typeInfoDataSubSegment.AppendLine($"        dw 0x{arrayVariantIndex:X}");
                    bytesWritten += 2;
                    _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");

                    var elementType = array.ElementType;
                    PadAlignment(arrayVariantFieldOffsets["ElementType"].Alignment);
                    _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.ElementType.Value");
                    _typeInfoDataSubSegment.AppendLine($"        dd 0x{GetTypeId(elementType, currentlyWritingTypeInfo: true):X}");
                    bytesWritten += 4;

                    PadAlignment(arrayVariantFieldOffsets["Length"].Alignment);
                    _typeInfoDataSubSegment.AppendLine("        ; TypeInfo.Length");
                    _typeInfoDataSubSegment.AppendLine($"        dq 0x{array.Length:X}");
                    bytesWritten += 8;
                    break;
                }
            default:
                throw new NotImplementedException(type.GetType().ToString());
        }

        Debug.Assert(bytesWritten <= typeInfoSize.Size, $"BytesWritten: {bytesWritten}, size: {typeInfoSize.Size}, type: {type}");
        if (bytesWritten < typeInfoSize.Size)
        {
            var bytesToWrite = typeInfoSize.Size - bytesWritten;
            _typeInfoDataSubSegment.AppendLine($"        times {bytesToWrite} db 0");
            bytesWritten += bytesToWrite;
            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
        }

        Debug.Assert(bytesWritten == typeInfoSize.Size);

        void PadAlignment(uint alignment)
        {
            _typeInfoDataSubSegment.AppendLine($"        ALIGN {alignment}, db 0");
            if (bytesWritten == 0)
            {
                // nothing will have been aligned, so nothing to increment
                _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
                return;
            }
            if (bytesWritten < alignment)
            {
                bytesWritten = alignment;
            }
            else
            {
                bytesWritten += bytesWritten % alignment;
            }

            _typeInfoDataSubSegment.AppendLine($"        ; BytesWritten: {bytesWritten}");
        }
    }

    private string ProcessInner()
    {
        var mainModule = modules.Where(x => x.Methods.Any(y => y.Name == "_Main")).ToArray();

        if (mainModule.Length != 1)
        {
            throw new InvalidOperationException("Expected a single module with a main method");
        }

        TryEnqueueMethodForProcessing(new LoweredMethod(
            new DefId(DefId.CoreLibModuleId, "__variant_identifier_field_getter"),
            "__variant_identifier_field_getter",
            TypeParameters: [],
            BasicBlocks: [
                new BasicBlock(
                    new BasicBlockId("bb0"),
                    Statements: [
                        new Assign(
                            new Local("_returnValue"),
                            new Use(new Copy(new Deref(new Local("_param0")))))
                    ],
                    new Return()
                )
            ],
            ReturnValue: new MethodLocal(
                "_returnValue",
                null,
                new LoweredConcreteTypeReference(
                    TypeChecker.ClassSignature.UInt16.Value.Name,
                    TypeChecker.ClassSignature.UInt16.Value.Id,
                    [])),
            ParameterLocals: [
                new MethodLocal(
                    "_param0",
                    null,
                    // this relies on the fact that variantIdentifier field is always first
                    new LoweredPointer(
                        new LoweredConcreteTypeReference(
                            TypeChecker.ClassSignature.UInt16.Value.Name,
                            TypeChecker.ClassSignature.UInt16.Value.Id,
                            [])
                    )
                )
            ],
            Locals: []), []);

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

        while (_typesToWriteBlobInfo.TryDequeue(out var typeInfoToWrite))
        {
            WriteTypeInfoBlob(typeInfoToWrite.TypeReference, typeInfoToWrite.TypeId);
        }

        var typeInfoSize = GetTypeSize(
            new LoweredConcreteTypeReference("TypeInfo", DefId.TypeInfo, []),
            []);
        var variantInfoSize = GetTypeSize(
            new LoweredConcreteTypeReference("VariantInfo", DefId.VariantInfo, []),
            []);
        var fieldInfoSize = GetTypeSize(
            new LoweredConcreteTypeReference("FieldInfo", DefId.FieldInfo, []),
            []);
        var methodInfoSize = GetTypeSize(
            new LoweredConcreteTypeReference("MethodInfo", DefId.MethodInfo, []),
            []);

        return $"""
                {AsmHeader}
                global typeInfoSize
                global fieldInfoSize
                global variantInfoSize
                global typeInfoCount
                global typeInfoArray
                global methodInfoArray
                global methodInfoCount
                global methodInfoSize
                {_codeSegment}
                {_dataSegment}
                {_stringDataSubSegment}
                    ALIGN 8, db 0
                    typeInfoSize dq 0x{typeInfoSize.Size:X}
                    fieldInfoSize dq 0x{fieldInfoSize.Size:X}
                    variantInfoSize dq 0x{variantInfoSize.Size:X}
                    methodInfoSize dq 0x{methodInfoSize.Size:X}
                    methodInfoCount dq 0x{_methodCount:X}
                    typeInfoCount dq 0x{_typeIds.Count:X}
                    ALIGN 16, db 0
                    typeInfoArray:
                {_typeInfoDataSubSegment}
                    ALIGN 16, db 0
                    methodInfoArray:
                {_methodInfoDataSubSegment}
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

        _codeSegment.AppendLine($"    sub     rsp, {ShadowSpaceBytes}");
        _codeSegment.AppendLine($"    call    init_runtime");
        _codeSegment.AppendLine($"    add     rsp, {ShadowSpaceBytes}");

        // give main it's shadow space
        _codeSegment.AppendLine($"    sub     rsp, {ShadowSpaceBytes}");
        _codeSegment.AppendLine($"    call    {GetMethodLabel(mainMethod, [])}");

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
        var label = typeArguments.Count == 0
            ? method.Id.FullName
            : $"{method.Id.FullName}_{string.Join("_", typeArguments.Select(x => x switch
            {
                LoweredConcreteTypeReference concrete => concrete.DefinitionId.FullName,
                LoweredPointer(LoweredConcreteTypeReference pointerToConcrete) => $"Pointer_{pointerToConcrete.DefinitionId.FullName}_",
                LoweredPointer(var pointerTo) => throw new NotImplementedException(pointerTo.GetType().ToString()),
                _ => throw new NotImplementedException(x.GetType().ToString())
            }))}";

        return label.Replace(":::", ".");
    }

    private uint _methodCount;

    private readonly List<KeyValuePair<ILoweredTypeReference, TypeSizeInfo>> _typeSizes = [];

    private sealed record TypeSizeInfo(
        uint Size,
        uint Alignment,
        Dictionary<string, Dictionary<string, FieldSize>> VariantFieldOffsets);

    private sealed record FieldSize(uint Offset, uint Size, uint Alignment);

    private static bool AreTypeReferencesEqual(ILoweredTypeReference left, ILoweredTypeReference right, Dictionary<string, ILoweredTypeReference> typeArguments)
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
                                case LoweredArray { Length: not null } loweredArray:
                                    {
                                        var elementSize = GetTypeSize(loweredArray.ElementType, typeArguments);
                                        var paddingNeededPerElement = elementSize.Size % elementSize.Alignment;
                                        fieldSize = new TypeSizeInfo(
                                            (elementSize.Size + paddingNeededPerElement) * loweredArray.Length.Value,
                                            elementSize.Alignment,
                                            []);
                                        break;
                                    }
                                case LoweredArray { Length: null } loweredArray:
                                    {
                                        throw new NotImplementedException();
                                        // var elementSize = GetTypeSize(loweredArray.ElementType, typeArguments);
                                        // var paddingNeededPerElement = elementSize.Size % elementSize.Alignment;
                                        // fieldSize = new TypeSizeInfo(
                                        //     (elementSize.Size + paddingNeededPerElement) * loweredArray.Length.Value,
                                        //     elementSize.Alignment,
                                        //     []);
                                        // break;
                                    }
                                case LoweredPointer loweredPointer:
                                    {
                                        fieldSize = GetTypeSize(loweredPointer, typeArguments);
                                        break;
                                    }
                                case RawPointer rawPointer:
                                    {
                                        fieldSize = GetTypeSize(rawPointer, typeArguments);
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
            case RawPointer:
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
            case LoweredArray { Length: not null } array:
                {
                    var elementSize = GetTypeSize(array.ElementType, typeArguments);
                    if (elementSize.Size % elementSize.Alignment != 0)
                    {
                        throw new NotImplementedException();
                    }

                    var arrayTypeSize = new TypeSizeInfo(
                        elementSize.Size * array.Length.Value + 8, // + 8 for length
                        Math.Max(8, elementSize.Alignment),
                        []);

                    _typeSizes.Add(KeyValuePair.Create(typeReference, arrayTypeSize));

                    return arrayTypeSize;
                }
            case LoweredArray { Length: null }:
                {
                    throw new UnreachableException();
                }
            default:
                throw new NotImplementedException(typeReference.GetType().ToString());
        }

        var typeSize = new TypeSizeInfo(size, alignment, dataTypeFieldOffsets);

        _typeSizes.Add(KeyValuePair.Create(typeReference, typeSize));

        return typeSize;
    }

    private static byte AlignInt(ref uint value, uint alignBy)
    {
        if (alignBy == 0)
            return 0;

        var mod = value % alignBy;
        if (mod == 0)
            return 0;

        var increment = alignBy - mod;

        value += increment;
        return (byte)increment;
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

        WriteMethodInfoBlob(method, _methodCount);

        Debug.Assert(method.TypeParameters.Count == typeArguments.Count);
        _currentTypeArguments = typeArguments.Zip(method.TypeParameters).ToDictionary(x => x.Second.PlaceholderName, x => x.First);
        _currentMethod = method;
        _methodCount++;
        _locals = [];
        var parameterStackOffset = PointerSize * 2; // start with offset by PointerSize * 2 because return address and rbp are on the top of the stack

        var returnType = method.ReturnValue.Type;
        if (returnType is LoweredGenericPlaceholder { PlaceholderName: var placeholderName })
        {
            returnType = _currentTypeArguments[placeholderName];
        }
        _ = GetTypeId(returnType, currentlyWritingTypeInfo: false);

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
            if (parameterType is LoweredGenericPlaceholder { PlaceholderName: var parameterPlaceholderName })
            {
                parameterType = _currentTypeArguments[parameterPlaceholderName];
            }
            _ = GetTypeId(parameterType, currentlyWritingTypeInfo: false);
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

            var rawParameterPlace = new MemoryOffset(new PointerTo(Register.BasePointer), null, parameterOffset);
            IAsmPlace parameterPlace = rawParameterPlace;
            if (parameterSize.Size > MaxParameterSize)
            {
                parameterPlace = new PointerTo(parameterPlace);
            }

            _locals[parameterLocal.CompilerGivenName] = new LocalInfo(
                parameterPlace,
                parameterType);

            if (sourceRegister is not null)
            {
                MoveIntoPlace(rawParameterPlace, sourceRegister, size);
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
                new PointerTo(_locals[ReturnValueAddressLocal].Place),
                returnType);
        }
        else
        {
            locals = locals.Append(method.ReturnValue);
        }

        foreach (var local in locals)
        {
            var localType = local.Type;
            if (localType is LoweredGenericPlaceholder { PlaceholderName: var localPlaceholderName })
            {
                localType = _currentTypeArguments[localPlaceholderName];
            }
            _ = GetTypeId(localType, currentlyWritingTypeInfo: false);
            var typeSize = GetTypeSize(localType, _currentTypeArguments);

            // make sure stack offset is aligned to the size of the type
            AlignInt(ref localStackOffset, typeSize.Alignment);

            // increment the stack offset before we associate the position with the local so that
            // it points to the lowest memory address (top of the stack) as structures grow towards
            // higher memory addresses
            localStackOffset += typeSize.Size;

            var localPlace = new MemoryOffset(new PointerTo(Register.BasePointer), null, -(int)localStackOffset);
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
            Copy { Place: var place } => GetPlaceType(place) switch
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

                    if (ownerType is LoweredArray)
                    {
                        Debug.Assert(field.FieldName == "Length");
                        return new LoweredConcreteTypeReference(
                            TypeChecker.ClassSignature.UInt64.Value.Name,
                            TypeChecker.ClassSignature.UInt64.Value.Id,
                            []
                        );
                    }

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
                            _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType.GetType().Name)
                        };

                        concreteOwner = ownerType as LoweredConcreteTypeReference;
                    }

                    var dataType = _dataTypes[concreteOwner.DefinitionId];
                    var variant = dataType.Variants.First(x => x.Name == field.VariantName);

                    var type = variant.Fields.First(x => x.Name == field.FieldName).Type;

                    if (type is LoweredGenericPlaceholder genericPlaceholder
                        && genericPlaceholder.OwnerDefinitionId == concreteOwner.DefinitionId)
                    {
                        var typeArgumentIndex = dataType.TypeParameters.Index()
                            .First(x => x.Item.PlaceholderName == genericPlaceholder.PlaceholderName).Index;
                        type = concreteOwner.TypeArguments[typeArgumentIndex];
                    }

                    return type;
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
        var size = GetTypeSize(operandType, _currentTypeArguments).Size;

        Register leftOperandRegister;
        Register rightOperandRegister;

        switch (kind)
        {
            case BinaryOperationKind.Add:
                {
                    leftOperandRegister = AllocateRegister();
                    rightOperandRegister = AllocateRegister();

                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    _codeSegment.AppendLine($"    add     {leftOperandRegister.ToAsm(size)}, {rightOperandRegister.ToAsm(size)}");
                    MoveIntoPlace(destination, leftOperandRegister, size);
                    break;
                }
            case BinaryOperationKind.Subtract:
                {
                    leftOperandRegister = AllocateRegister();
                    rightOperandRegister = AllocateRegister();

                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    _codeSegment.AppendLine($"    sub     {leftOperandRegister.ToAsm(size)}, {rightOperandRegister.ToAsm(size)}");
                    MoveIntoPlace(destination, leftOperandRegister, size);
                    break;
                }
            case BinaryOperationKind.Multiply:
                {
                    // mul/imul needs the value to be in the a register
                    leftOperandRegister = Register.A;
                    AllocateRegister(leftOperandRegister);
                    rightOperandRegister = AllocateRegister();

                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    var intSigned = GetIntSigned(left).NotNull();

                    // imul with one operand implicitly goes into the a register (rax or al),
                    // and the destination is in the a register too
                    _codeSegment.AppendLine(intSigned == IntSigned.Signed
                        ? $"    imul    {rightOperandRegister.ToAsm(size)}"
                        : $"    mul     {rightOperandRegister.ToAsm(size)}");

                    MoveIntoPlace(destination, leftOperandRegister, size);
                    break;
                }
            case BinaryOperationKind.Divide:
                {
                    leftOperandRegister = Register.A;
                    AllocateRegister(leftOperandRegister);
                    if (size != 1)
                    {
                        AllocateRegister(Register.D);
                    }

                    if (GetIntSigned(left) == IntSigned.Signed)
                    {
                        rightOperandRegister = AllocateRegister();

                        MoveOperandToDestination(left, leftOperandRegister);
                        MoveOperandToDestination(right, rightOperandRegister);

                        // sign extend for signed division
                        _codeSegment.AppendLine(size switch
                        {
                            1 => "    cbw", // quotent in al, remainder in ah
                            2 => "    cwd", // quotent in ax, remainder in dx
                            4 => "    cdq", // quotent in eax, remainder in edx
                            8 => "    cqo", // quotent in rax, remainder in rdx
                            _ => throw new UnreachableException()
                        });
                        _codeSegment.AppendLine($"    idiv    {rightOperandRegister.ToAsm(size)}");
                    }
                    else
                    {
                        rightOperandRegister = AllocateRegister();
                        MoveOperandToDestination(left, leftOperandRegister);
                        MoveOperandToDestination(right, rightOperandRegister);

                        // zero extend for unsigned division
                        _codeSegment.AppendLine(size switch
                        {
                            1 => "    xor     ah, ah",
                            2 => "    xor     dx, dx",
                            4 => "    xor     edx, edx",
                            8 => "    xor     rdx, rdx",
                            _ => throw new UnreachableException()
                        });

                        _codeSegment.AppendLine($"    div     {rightOperandRegister.ToAsm(size)}");
                    }

                    if (size != 1)
                    {
                        FreeRegister(Register.D);
                    }

                    MoveIntoPlace(destination, leftOperandRegister, size);

                    break;
                }
            case BinaryOperationKind.LessThan:
                {
                    leftOperandRegister = AllocateRegister();
                    rightOperandRegister = AllocateRegister();

                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    _codeSegment.AppendLine($"    cmp     {leftOperandRegister.ToAsm(size)}, {rightOperandRegister.ToAsm(size)}");
                    _codeSegment.AppendLine("    pushf");
                    _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                    _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 10000000b"); // sign flag
                    _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 7");
                    MoveIntoPlace(destination, leftOperandRegister, 1);
                    break;
                }
            case BinaryOperationKind.LessThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.GreaterThan:
                {
                    leftOperandRegister = AllocateRegister();
                    rightOperandRegister = AllocateRegister();

                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    _codeSegment.AppendLine($"    cmp     {rightOperandRegister.ToAsm(size)}, {leftOperandRegister.ToAsm(size)}");
                    _codeSegment.AppendLine("    pushf");
                    _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                    _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 10000000b"); // sign flag
                    _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 7");
                    MoveIntoPlace(destination, leftOperandRegister, 1);
                    break;
                }
            case BinaryOperationKind.GreaterThanOrEqual:
                throw new NotImplementedException();
            case BinaryOperationKind.Equal:
                {
                    leftOperandRegister = AllocateRegister();
                    rightOperandRegister = AllocateRegister();
                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    _codeSegment.AppendLine($"    cmp     {leftOperandRegister.ToAsm(size)}, {rightOperandRegister.ToAsm(size)}");
                    _codeSegment.AppendLine("    pushf");
                    _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                    _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 1000000b"); // zero flag
                    _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 6");
                    MoveIntoPlace(destination, leftOperandRegister, 1);
                    break;
                }
            case BinaryOperationKind.NotEqual:
                {
                    leftOperandRegister = AllocateRegister();
                    rightOperandRegister = AllocateRegister();
                    MoveOperandToDestination(left, leftOperandRegister);
                    MoveOperandToDestination(right, rightOperandRegister);
                    _codeSegment.AppendLine($"    cmp     {leftOperandRegister.ToAsm(size)}, {rightOperandRegister.ToAsm(size)}");
                    _codeSegment.AppendLine("    pushf");
                    _codeSegment.AppendLine($"    pop     {leftOperandRegister.ToAsm(PointerSize)}");
                    _codeSegment.AppendLine($"    and     {leftOperandRegister.ToAsm(PointerSize)}, 1000000b"); // zero flag
                    _codeSegment.AppendLine($"    shr     {leftOperandRegister.ToAsm(PointerSize)}, 6");
                    _codeSegment.AppendLine($"    btc     {leftOperandRegister.ToAsm(PointerSize)}, 0");
                    MoveIntoPlace(destination, leftOperandRegister, 1);
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
            case FillArray fill:
                {
                    // todo: this is naive, do something smarter
                    var elementType = GetOperandType(fill.Value);
                    var elementSize = GetTypeSize(elementType, _currentTypeArguments);
                    Debug.Assert(place.IsMemoryPlace);
                    var (remainingOffset, addressRegister, freeRegister) = FollowOffsetPointerChain(place);
                    for (var i = 0; i < fill.Count; i++)
                    {
                        MoveOperandToDestination(
                            fill.Value,
                            new MemoryOffset(
                                new PointerTo(addressRegister),
                                null,
                                (i * (int)elementSize.Size) + remainingOffset + 8 // + 8 to skip past array length
                            )
                        );
                    }

                    if (freeRegister)
                    {
                        FreeRegister(addressRegister);
                    }

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
                    MoveIntoPlace(place, operandRegister, operandSize.Size);
                    FreeRegister(operandRegister);
                    break;
                }
            case UnaryOperationKind.Negate:
                {
                    var operandRegister = AllocateRegister();
                    MoveOperandToDestination(operand, operandRegister);

                    _codeSegment.AppendLine($"    neg     {operandRegister.ToAsm(operandSize.Size)}");
                    MoveIntoPlace(place, operandRegister, operandSize.Size);

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
                    if (returnType is LoweredGenericPlaceholder { PlaceholderName: var placeholderName })
                    {
                        returnType = _currentTypeArguments[placeholderName];
                    }
                    var returnSize = GetTypeSize(returnType, _currentTypeArguments);
                    var returnValuePlace = PlaceToAsmPlace(new Local(_currentMethod.NotNull().ReturnValue.CompilerGivenName));

                    AllocateRegister(Register.A);
                    if (returnSize.Size > MaxParameterSize)
                    {
                        // copy the pointer to the return value into the "a" register
                        MoveIntoPlace(
                            Register.A,
                            _locals[ReturnValueAddressLocal].Place,
                            PointerSize);
                    }
                    else
                    {
                        MoveIntoPlace(
                            Register.A,
                            returnValuePlace,
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
                MoveOperandToDestination(argument.NotNull(), new MemoryOffset(new PointerTo(Register.StackPointer), null, -(int)largeParametersSpace));
            }
        }

        parametersSpaceNeeded += largeParametersSpace;

        AlignInt(ref parametersSpaceNeeded, 16);

        _codeSegment.AppendLine($"; LargeParameterSpace: {largeParametersSpace} bytes, ArgumentStackSpace: {parametersSpaceNeeded - largeParametersSpace}");
        _codeSegment.AppendLine($"    sub     rsp, {parametersSpaceNeeded}");

        var registersToFree = new List<Register>();
        for (var i = arity - 1; i >= 0; i--)
        {
            IAsmPlace argumentDestination = i switch
            {
                0 => Register.C,
                1 => Register.D,
                2 => Register.R8,
                3 => Register.R9,
                _ => new MemoryOffset(new PointerTo(Register.StackPointer), null, i * (int)MaxParameterSize)
            };

            var (type, argument) = argumentTypes[i];

            var size = GetTypeSize(type, _currentTypeArguments);


            if (argumentDestination is Register register)
            {
                registersToFree.Add(register);
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

        foreach (var register in registersToFree)
        {
            FreeRegister(register);
        }

        // move rsp back to where it was before we called the function
        _codeSegment.AppendLine($"    add     rsp, {parametersSpaceNeeded}");

        AllocateRegister(Register.A);
        IAsmPlace returnSource = Register.A;

        if (returnSize.Size > MaxParameterSize)
        {
            returnSource = new PointerTo(returnSource);
        }

        var destination = PlaceToAsmPlace(methodCall.PlaceDestination);

        MoveIntoPlace(
            destination,
            returnSource,
            returnSize.Size);

        FreeRegister(Register.A);

        _codeSegment.AppendLine($"    jmp     {GetBasicBlockLabel(methodCall.GoToAfter)}");
    }

    private string GetBasicBlockLabel(BasicBlockId basicBlockId)
    {
        return $"{basicBlockId.Id}_{_methodCount}";
    }

    private interface IAsmPlace
    {
        public bool IsMemoryPlace { get; }
    }

    private sealed class Register : IAsmPlace
    {
        public static readonly Register A = new("a", false);
        public static readonly Register B = new("b", false);
        public static readonly Register C = new("c", false);
        public static readonly Register D = new("d", false);
        public static readonly Register Source = new("rsi", false);
        public static readonly Register Destination = new("rdi", false);

        public static readonly Register StackPointer = new("rsp", false, true);
        public static readonly Register BasePointer = new("rbp", false, true);

        public static readonly Register R8 = new("r8", true);
        public static readonly Register R9 = new("r9", true);
        public static readonly Register R10 = new("r10", true);
        public static readonly Register R11 = new("r11", true);
        public static readonly Register R12 = new("r12", true);
        public static readonly Register R13 = new("r13", true);
        public static readonly Register R14 = new("r14", true);
        public static readonly Register R15 = new("r15", true);

        public static readonly IReadOnlyList<Register> GeneralPurposeRegisters = [
            // A,
            B,
            // C,
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

        private Register(string name, bool isNumberRegister, bool isImmutable = false)
        {
            Name = name;
            IsNumberRegister = isNumberRegister;
            IsImmutable = isImmutable;
        }

        private string Name { get; }
        private bool IsNumberRegister { get; }
        public bool IsImmutable { get; }
        public bool IsMemoryPlace => false;

        public string ToAsm(uint size)
        {
            if (Name is "rsi" or "rdi" or "rbp" or "rsp")
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

    private class MemoryOffset : IAsmPlace
    {
        public MemoryOffset(IAsmPlace memory, Action<Register>? getOffsetInRegister, int? offset)
        {
            if (!memory.IsMemoryPlace)
            {
                throw new InvalidOperationException();
            }
            Memory = memory;
            GetOffsetInRegister = getOffsetInRegister;
            Offset = offset;
        }

        public void Deconstruct(out IAsmPlace memory, out Action<Register>? getOffsetInRegister, out int? offset)
        {
            memory = Memory;
            getOffsetInRegister = GetOffsetInRegister;
            offset = Offset;
        }

        public IAsmPlace Memory { get; }
        public Action<Register>? GetOffsetInRegister { get; }
        public int? Offset { get; }
        public bool IsMemoryPlace => true;
    }

    private record PointerTo(IAsmPlace PointerPlace) : IAsmPlace
    {
        public bool IsMemoryPlace => true;
    }

    private string GetStringConstantLabel(string constant)
    {
        if (_strings.TryGetValue(constant, out var stringName))
        {
            return stringName;
        }

        stringName = $"_str_{_strings.Count}";
        _strings[constant] = stringName;
        // todo: no null terminated strings
        var str = constant.AsSpan();
        _stringDataSubSegment.Append($"    {stringName} db ");

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
                    _stringDataSubSegment.Append(", ");
                }

                if (!inStringLiteral)
                {
                    _stringDataSubSegment.Append('"');
                }
                _stringDataSubSegment.Append(segment);
                started = true;
                inStringLiteral = true;
            }
            if (inStringLiteral)
            {
                _stringDataSubSegment.Append('"');
                inStringLiteral = false;
            }
            if (started)
            {
                _stringDataSubSegment.Append(", ");
            }
            _stringDataSubSegment.Append((int)str[whitespaceIndex]);
            str = str[(whitespaceIndex + 1)..];
            whitespaceIndex = str.IndexOfAnyInRange((char)9, (char)13);
            started = true;
        }

        if (str.Length > 0)
        {
            if (started)
            {
                _stringDataSubSegment.Append(", ");
            }
            if (!inStringLiteral)
            {
                _stringDataSubSegment.Append('"');
            }
            _stringDataSubSegment.Append(str);
            inStringLiteral = true;
            started = true;
        }

        if (inStringLiteral)
        {
            _stringDataSubSegment.Append('"');
        }
        if (started)
        {
            _stringDataSubSegment.Append(", ");
        }
        _stringDataSubSegment.AppendLine("0");

        return stringName;
    }

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
                    MoveIntoPlace(destination, PlaceToAsmPlace(copy.Place), size.Size);
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
                    var stringName = GetStringConstantLabel(stringConstant.Value);

                    MoveOperandToDestination(
                        new UIntConstant((ulong)stringConstant.Value.Length, (int)PointerSize),
                        destination);

                    if (!destination.IsMemoryPlace)
                    {
                        throw new InvalidOperationException("String cannot fit into a register");
                    }
                    var stringAddressDestination = new MemoryOffset(destination, null, (int)PointerSize);

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
            case TypeIdOf(var type):
                {
                    var typeId = GetTypeId(type, currentlyWritingTypeInfo: false);
                    MoveIntoPlace(destination, $"0x{typeId:X}", PointerSize);
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
            case MemoryOffset or PointerTo:
                {
                    var (remainingOffset, addressRegister, freeAddressRegister) = FollowOffsetPointerChain(source);

                    if (destination is Register destinationRegister)
                    {
                        _codeSegment.AppendLine($"    lea     {destinationRegister.ToAsm(PointerSize)}, [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}]");
                        if (freeAddressRegister)
                        {
                            FreeRegister(addressRegister);
                        }
                    }
                    else if (addressRegister.IsImmutable)
                    {
                        var newAddressRegister = AllocateRegister();
                        _codeSegment.AppendLine($"    lea     {newAddressRegister.ToAsm(PointerSize)}, [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}]");

                        if (freeAddressRegister)
                        {
                            FreeRegister(addressRegister);
                        }

                        MoveIntoPlace(destination, newAddressRegister, PointerSize);
                        FreeRegister(newAddressRegister);
                    }
                    else
                    {
                        _codeSegment.AppendLine($"    lea     {addressRegister.ToAsm(PointerSize)}, [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}]");
                        MoveIntoPlace(destination, addressRegister, PointerSize);
                        if (freeAddressRegister)
                        {
                            FreeRegister(addressRegister);
                        }
                    }

                    break;
                }
            case Register:
                throw new InvalidOperationException("Register has no address");
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private (int remainingOffset, Register addressRegister, bool freeRegister) FollowOffsetPointerChain(
        IAsmPlace place)
    {
        Debug.Assert(place is MemoryOffset or PointerTo);
        var offsetsAndPointers = new Stack<IAsmPlace>();

        var nextPlace = place;
        while (nextPlace is MemoryOffset or PointerTo)
        {
            offsetsAndPointers.Push(nextPlace);
            nextPlace = nextPlace switch
            {
                MemoryOffset x => x.Memory,
                PointerTo x => x.PointerPlace,
                _ => throw new UnreachableException()
            };
        }

        if (nextPlace is not Register addressRegister || offsetsAndPointers.Pop() is not PointerTo)
        {
            throw new UnreachableException();
        }

        var constOffset = 0;
        var getOffsetInRegisters = new List<Action<Register>>();
        var offsetRegister = AllocateRegister();
        var freeAddressRegister = false;

        while (offsetsAndPointers.TryPop(out var next))
        {
            switch (next)
            {
                case MemoryOffset(_, var getOffsetInRegister, var offset):
                    {
                        constOffset += offset ?? 0;
                        if (getOffsetInRegister is not null)
                            getOffsetInRegisters.Add(getOffsetInRegister);
                        break;
                    }
                case PointerTo:
                    {
                        if (getOffsetInRegisters.Count > 0)
                        {
                            getOffsetInRegisters[0](offsetRegister);
                        }
                        if (getOffsetInRegisters.Count > 1)
                        {
                            var tempRegister = AllocateRegister();

                            foreach (var getOffsetInRegister in getOffsetInRegisters.Skip(1))
                            {
                                getOffsetInRegister(tempRegister);
                                _codeSegment.AppendLine($"    add     {offsetRegister.ToAsm(PointerSize)}, {tempRegister.ToAsm(PointerSize)}");
                            }

                            FreeRegister(tempRegister);
                        }

                        if (addressRegister.IsImmutable)
                        {
                            var newAddressRegister = AllocateRegister();
                            MoveIntoPlace(newAddressRegister, addressRegister, PointerSize);
                            addressRegister = newAddressRegister;
                            freeAddressRegister = true;
                        }

                        if (getOffsetInRegisters.Count > 0)
                        {
                            _codeSegment.AppendLine($"    add     {addressRegister.ToAsm(PointerSize)}, {offsetRegister.ToAsm(PointerSize)}");
                        }

                        _codeSegment.AppendLine($"    mov     {addressRegister.ToAsm(PointerSize)}, [{addressRegister.ToAsm(PointerSize)}{FormatOffset(constOffset)}]");
                        constOffset = 0;
                        getOffsetInRegisters.Clear();

                        break;
                    }
            }
        }

        if (getOffsetInRegisters.Count > 0)
        {
            getOffsetInRegisters[0](offsetRegister);
            if (getOffsetInRegisters.Count > 1)
            {
                var tempRegister = AllocateRegister();

                foreach (var getOffsetInRegister in getOffsetInRegisters.Skip(1))
                {
                    getOffsetInRegister(tempRegister);
                    _codeSegment.AppendLine(
                        $"    add     {offsetRegister.ToAsm(PointerSize)}, {tempRegister.ToAsm(PointerSize)}");
                }

                FreeRegister(tempRegister);
            }

            if (addressRegister.IsImmutable)
            {
                var newAddressRegister = AllocateRegister();
                MoveIntoPlace(newAddressRegister, addressRegister, PointerSize);
                addressRegister = newAddressRegister;
                freeAddressRegister = true;
            }

            _codeSegment.AppendLine(
                $"    add     {addressRegister.ToAsm(PointerSize)}, {offsetRegister.ToAsm(PointerSize)}");
        }

        FreeRegister(offsetRegister);

        return (constOffset, addressRegister, freeAddressRegister);
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
        MoveIntoPlace(place, register, PointerSize);
        FreeRegister(register);
    }

    private void MoveIntoPlace(IAsmPlace destination, IAsmPlace source, uint size)
    {
        switch (source)
        {
            case PointerTo pointerTo:
                MoveIntoPlaceInner(destination, pointerTo, size);
                break;
            case MemoryOffset memoryOffset:
                MoveIntoPlaceInner(destination, memoryOffset, size);
                break;
            case Register register:
                MoveIntoPlaceInner(destination, register, size);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private void MoveIntoPlaceInner(IAsmPlace place, Register register, uint size)
    {
        switch (place)
        {
            case PointerTo or MemoryOffset:
                {
                    var (remainingOffset, addressRegister, freeRegister) = FollowOffsetPointerChain(place);
                    _codeSegment.AppendLine($"    mov     [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}], {register.ToAsm(size)}");
                    if (freeRegister)
                    {
                        FreeRegister(addressRegister);
                    }
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

    private void MoveIntoPlaceInner(IAsmPlace destination, MemoryOffset source, uint size)
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

        var (remainingOffset, addressRegister, freeAddressRegister) = FollowOffsetPointerChain(source);
        var valueRegister = AllocateRegister();

        _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}]");
        if (freeAddressRegister)
        {
            FreeRegister(addressRegister);
        }

        MoveIntoPlace(destination, valueRegister, size);

        FreeRegister(valueRegister);
    }

    private void MoveIntoPlaceInner(IAsmPlace destination, PointerTo source, uint size)
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

        var (remainingOffset, addressRegister, freeAddressRegister) = FollowOffsetPointerChain(source);
        var valueRegister = AllocateRegister();

        _codeSegment.AppendLine($"    mov     {valueRegister.ToAsm(size)}, [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}]");
        if (freeAddressRegister)
        {
            FreeRegister(addressRegister);
        }

        MoveIntoPlace(destination, valueRegister, size);

        FreeRegister(valueRegister);
    }

    private void FillMemory(IAsmPlace place, string constantValue, uint size)
    {
        if (size < PointerSize)
        {
            MoveIntoPlace(place, constantValue, size);
            return;
        }

        switch (place)
        {
            case PointerTo or MemoryOffset:
                {
                    var (remainingOffset, addressRegister, freeAddressRegister) = FollowOffsetPointerChain(place);

                    var i = 0;
                    while (i < size)
                    {
                        var chunkSize = SizeSpecifiers.Keys.Where(x => i + x <= size).Max();
                        var sizeSpecifier = SizeSpecifiers[chunkSize];
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset + i)}], {constantValue}");
                        i += (int)chunkSize;
                    }

                    if (freeAddressRegister)
                    {
                        FreeRegister(addressRegister);
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
        Debug.Assert(size <= SizeSpecifiers.Keys.Max());
        switch (place)
        {
            case PointerTo or MemoryOffset:
                {
                    var (remainingOffset, addressRegister, freeAddressRegister) = FollowOffsetPointerChain(place);
                    var i = 0;

                    while (i < size)
                    {
                        var chunkSize = SizeSpecifiers.Keys.Where(x => i + x <= size).Max();
                        var sizeSpecifier = SizeSpecifiers[chunkSize];
                        _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset + i)}], {constantValue}");
                        i += (int)chunkSize;
                    }

                    // _codeSegment.AppendLine($"    mov     {sizeSpecifier} [{addressRegister.ToAsm(PointerSize)}{FormatOffset(remainingOffset)}], {constantValue}");

                    if (freeAddressRegister)
                    {
                        FreeRegister(addressRegister);
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
            Deref deref => DerefToAsmPlace(deref),
            Index index => IndexToAsmPlace(index),
            _ => throw new ArgumentOutOfRangeException(nameof(place))
        };
    }

    private IAsmPlace DerefToAsmPlace(Deref deref)
    {
        var pointerPlace = PlaceToAsmPlace(deref.PointerPlace);

        return new PointerTo(pointerPlace);
    }

    private IAsmPlace IndexToAsmPlace(Index index)
    {
        var arrayPlace = PlaceToAsmPlace(index.ArrayPlace);
        var arrayType = GetPlaceType(index.ArrayPlace) switch
        {
            LoweredArray x => x,
            LoweredPointer(LoweredConcreteTypeReference x) => (x.TypeArguments[0] as LoweredArray).NotNull(),
            var x => throw new UnreachableException(x.GetType().ToString())
        };

        var elementSize = GetTypeSize(arrayType.ElementType, _currentTypeArguments);

        return new MemoryOffset(
            arrayPlace,
            GetOffsetInRegister,
            // offset by 8 to skip the length
            offset: 8);

        void GetOffsetInRegister(Register indexRegister)
        {
            MoveOperandToDestination(index.ArrayIndex, indexRegister);
            _codeSegment.AppendLine($"    imul    {indexRegister.ToAsm(PointerSize)}, 0x{elementSize.Size:x}");
        }
    }

    private IAsmPlace FieldToAsmPlace(Field field)
    {
        var ownerPlace = PlaceToAsmPlace(field.FieldOwner);
        var ownerType = GetPlaceType(field.FieldOwner);

        if (!ownerPlace.IsMemoryPlace)
        {
            throw new NotImplementedException();
        }

        if (ownerType is LoweredArray loweredArray)
        {
            if (field.FieldName != "Length")
            {
                throw new UnreachableException("Length is the only supported array field");
            }

            return ownerPlace;
        }

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

        return new MemoryOffset(ownerPlace, null, (int)fieldSize.Offset);
    }

}
