using Xunit.v3;

namespace Reef.Core.Tests;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TestMeAttribute : Attribute, ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        return [KeyValuePair.Create("Category", "TestMe")];
    }

}
