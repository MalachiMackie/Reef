namespace Reef.Core;

public record DefId(ModuleId ModuleId, string FullName)
{
    public static ModuleId CoreLibModuleId { get; } = new("Reef:::Core");
    public static ModuleId DiagnosticsModuleId { get; } = new("Reef:::Core:::Diagnostics");
    public static ModuleId ReflectionModuleId { get; } = new("Reef:::Core:::Reflection");

    public static DefId GetMemoryUsageBytes { get; } = new(DiagnosticsModuleId, $"{DiagnosticsModuleId}:::get_memory_usage_bytes");
    public static DefId TriggerGC { get; } = new(DiagnosticsModuleId, $"{DiagnosticsModuleId}:::trigger_gc");
    public static DefId PrintStackTrace { get; } = new(DiagnosticsModuleId, $"{DiagnosticsModuleId}:::print_stack_trace");
    public static DefId PrintAllTypes { get; } = new(DiagnosticsModuleId, $"{DiagnosticsModuleId}:::print_all_types");
    public static DefId PrintAllMethods { get; } = new(DiagnosticsModuleId, $"{DiagnosticsModuleId}:::print_all_methods");

    public static DefId TypeId { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::TypeId");
    public static DefId MethodId { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::MethodId");
    public static DefId MethodInfo { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::MethodInfo");
    public static DefId TypeInfo { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::TypeInfo");
    public static DefId VariantInfo { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::VariantInfo");
    public static DefId StaticFieldInfo { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::StaticFieldInfo");
    public static DefId FieldInfo { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::FieldInfo");
    public static DefId MethodLocal { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::MethodLocal");
    public static DefId MethodParameter { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::MethodParameter");
    public static DefId VariablePlace { get; } = new(ReflectionModuleId, $"{ReflectionModuleId}:::VariablePlace");

    public static DefId BoxedValue { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::BoxedValue");
    public static DefId ObjectHeader { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::ObjectHeader");

    public static DefId Unbox { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::unbox");
    public static DefId Box { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::box");
    public static DefId PrintString { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_string");
    public static DefId PrintI8 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_i8");
    public static DefId PrintI16 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_i16");
    public static DefId PrintI32 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_i32");
    public static DefId PrintI64 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_i64");
    public static DefId PrintU8 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_u8");
    public static DefId PrintU16 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_u16");
    public static DefId PrintU32 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_u32");
    public static DefId PrintU64 { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::print_u64");
    public static DefId Allocate { get; } = new(CoreLibModuleId, $"{CoreLibModuleId}:::allocate");

    public static DefId Unit { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::Unit");

    public static DefId String { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::string");
    public static DefId Array { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::array");

    public static DefId Int64 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::i64");
    public static DefId Int32 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::i32");
    public static DefId Int16 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::i16");
    public static DefId Int8 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::i8");
    public static DefId UInt64 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::u64");
    public static DefId UInt32 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::u32");
    public static DefId UInt16 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::u16");
    public static DefId UInt8 { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::u8");

    public static DefId RawPointer { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::rawPointer");
    public static DefId MethodPointer { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::methodPointer");

    public static DefId Boolean { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::bool");

    public static DefId Never { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::!");

    public static DefId Result { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::result");

    public static DefId Result_Create_Error { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::result__Create__Error");
    public static DefId Result_Create_Ok { get; } = new(CoreLibModuleId, CoreLibModuleId.Value + ":::result__Create__Ok");

    public static readonly IReadOnlyList<DefId> SignedInts = [Int8, Int16, Int32, Int64];
    public static readonly IReadOnlyList<DefId> UnsignedInts = [UInt8, UInt16, UInt32, UInt64];

    public static DefId Main(ModuleId moduleId) => new(moduleId, moduleId.Value + ":::_Main");

    public static DefId FunctionObject(int parameterCount) => new(CoreLibModuleId, CoreLibModuleId.Value + $":::Function`{parameterCount + 1}");

    public static DefId FunctionObject_Call(int parameterCount) => new(CoreLibModuleId, CoreLibModuleId.Value + $":::Function`{parameterCount + 1}__Call");

    public static DefId Tuple(int elementCount) => new(CoreLibModuleId, CoreLibModuleId.Value + $":::Tuple`{elementCount}");
}
