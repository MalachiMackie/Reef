using System.Runtime.CompilerServices;

namespace Reef.Core.Tests;

// ReSharper disable once UnusedType.Global
public static class FluentAssertionsGlobalConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        License.Accepted = true;
        AssertionConfiguration.Current.Equivalency.Modify(x => x.PreferringRuntimeMemberTypes().AllowingInfiniteRecursion().WithStrictTyping());
    }
}
