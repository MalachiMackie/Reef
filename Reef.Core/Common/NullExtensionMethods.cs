using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Reef.Core.Common;

public static class NullExtensionMethods
{
    [return: NotNull]
    public static T NotNull<T>(this T? item, [CallerArgumentExpression(nameof(item))] string paramName = "",
        string? expectedReason = null)
        where T : class
    {
        var sb = new StringBuilder($"Expected {paramName} not to be null");
        if (expectedReason is not null)
        {
            sb.Append($" because {expectedReason}");
        }
        return item
            ?? throw new InvalidOperationException(sb.ToString());
    }

    public static T NotNull<T>(
            this T? item,
            [CallerArgumentExpression(nameof(item))] string paramName = "",
            string? expectedReason = null)
        where T : struct
    {
        var sb = new StringBuilder($"Expected {paramName} not to be null");
        if (expectedReason is not null)
        {
            sb.Append($" because {expectedReason}");
        }
        return item
            ?? throw new InvalidOperationException(sb.ToString());
    }
}
