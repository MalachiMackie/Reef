using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Reef.Core.Tests;

// ReSharper disable once UnusedType.Global
public static class FluentAssertionsGlobalConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AssertionConfiguration.Current.Equivalency.Modify(x => x.PreferringRuntimeMemberTypes().AllowingInfiniteRecursion());
    }
}
