using Reef.Core.LoweredExpressions;

namespace Reef.Core;

public class TreeShaker(LoweredProgram program)
{
    private readonly HashSet<DefId> _usefulMethodDefIds = [];
    private readonly Dictionary<DefId, IMethod> _methods = program.Methods
        .ToDictionary(x => x.Id);

    public HashSet<DefId> Shake()
    {
        var mainMethod = program.Methods.OfType<LoweredMethod>().Single(x => x.Name == "_Main");

        ShakeMethod(mainMethod);

        return _usefulMethodDefIds;
    }

    private void ShakeMethod(IMethod method)
    {
        if (!_usefulMethodDefIds.Add(method.Id) || method is not LoweredMethod loweredMethod)
        {
            return;
        }

        foreach (var basicBlock in loweredMethod.BasicBlocks)
        {
            if (basicBlock.Terminator is MethodCall { Function: var function })
            {
                ShakeMethod(_methods[function.DefinitionId]);
            }
        }
    }
}
