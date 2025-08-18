using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Reef.Core.Common;

public static class NullExtensionMethods
{
    [return: NotNull]
    public static T NotNull<T>(this T? item, [CallerArgumentExpression(nameof(item))] string paramName = "")
        where T : class
    {
        return item ?? throw new InvalidOperationException($"Expected {paramName} not to be null");
    }
}
