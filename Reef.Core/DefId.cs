namespace Reef.Core;

public record DefId(string ModuleId, string FullName)
{
    public static string CoreLibModuleId => "Reef.Core";

    public static string CoreLibNamespace => "System";

    public static DefId Printf { get; } = new(CoreLibModuleId, "printf");

    public static DefId Unit { get; } = new(CoreLibModuleId, CoreLibNamespace + ".Unit");

    public static DefId String { get; } = new(CoreLibModuleId, CoreLibNamespace + ".string");

    public static DefId Int64 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".i64");
    public static DefId Int32 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".i32");
    public static DefId Int16 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".i16");
    public static DefId Int8 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".i8");
    public static DefId UInt64 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".u64");
    public static DefId UInt32 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".u32");
    public static DefId UInt16 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".u16");
    public static DefId UInt8 { get; } = new(CoreLibModuleId, CoreLibNamespace + ".u8");

    public static DefId RawPointer { get; } = new(CoreLibModuleId, CoreLibNamespace + ".rawPointer");

    public static DefId Boolean { get; } = new(CoreLibModuleId, CoreLibNamespace + ".bool");

    public static DefId Never { get; } = new(CoreLibModuleId, CoreLibNamespace + ".!");

    public static DefId Result { get; } = new(CoreLibModuleId, CoreLibNamespace + ".result");

    public static DefId Result_Create_Error { get; } = new(CoreLibModuleId, CoreLibNamespace + ".result__Create__Error");
    public static DefId Result_Create_Ok { get; } = new(CoreLibModuleId, CoreLibNamespace + ".result__Create__Ok");
    
    public static readonly IReadOnlyList<DefId> SignedInts = [Int8, Int16, Int32, Int64];
    public static readonly IReadOnlyList<DefId> UnsignedInts = [UInt8, UInt16, UInt32, UInt64];

    public static DefId Main(string moduleId) => new(moduleId, moduleId + "._Main");

    public static DefId FunctionObject(int parameterCount) => new(CoreLibModuleId, CoreLibNamespace + $".Function`{parameterCount + 1}");

    public static DefId FunctionObject_Call(int parameterCount) => new(CoreLibModuleId, CoreLibNamespace + $".Function`{parameterCount + 1}__Call");

    public static DefId Tuple(int elementCount) => new(CoreLibModuleId, CoreLibNamespace + $".Tuple`{elementCount}");
    
}
