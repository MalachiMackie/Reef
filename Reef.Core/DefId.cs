namespace Reef.Core;

public record DefId(string ModuleId, string FullName)
{
    public static string CoreLibModuleId => "Reef.Core";

    public static string CoreLibNamespace => "System";

    public static DefId Printf { get; } = new(CoreLibModuleId, "printf");

    public static DefId Unit { get; } = new(CoreLibModuleId, CoreLibNamespace + ".Unit");

    public static DefId String { get; } = new(CoreLibModuleId, CoreLibNamespace + ".string");

    public static DefId Int { get; } = new(CoreLibModuleId, CoreLibNamespace + ".int");

    public static DefId RawPointer { get; } = new(CoreLibModuleId, CoreLibNamespace + ".rawPointer");

    public static DefId Boolean { get; } = new(CoreLibModuleId, CoreLibNamespace + ".bool");

    public static DefId Never { get; } = new(CoreLibModuleId, CoreLibNamespace + ".!");

    public static DefId Result { get; } = new(CoreLibModuleId, CoreLibNamespace + ".result");

    public static DefId Result_Create_Error { get; } = new(CoreLibModuleId, CoreLibNamespace + ".result__Create__Error");
    public static DefId Result_Create_Ok { get; } = new(CoreLibModuleId, CoreLibNamespace + ".result__Create__Ok");

    public static DefId Main(string moduleId) => new(moduleId, moduleId + "._Main");

    public static DefId FunctionObject(int parameterCount) => new(CoreLibModuleId, CoreLibNamespace + $".Function`{parameterCount + 1}");

    public static DefId FunctionObject_Call(int parameterCount) => new(CoreLibModuleId, CoreLibNamespace + $".Function`{parameterCount + 1}__Call");

    public static DefId Tuple(int elementCount) => new(CoreLibModuleId, CoreLibNamespace + $".Tuple`{elementCount}");
}
