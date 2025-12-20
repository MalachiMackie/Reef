using Reef.Core.LoweredExpressions;

namespace Reef.Core;

public class TreeShaker(IReadOnlyList<LoweredModule> modules)
{
    private readonly HashSet<DefId> _usefulMethodDefIds = [];
    private readonly Dictionary<DefId, IMethod> _methods = modules.SelectMany(x => x.Methods)
        .ToDictionary(x => x.Id);

    public HashSet<DefId> Shake()
    {
        var mainModule = modules.Where(x => x.Methods.Any(y => y.Name == "_Main")).ToArray();

        if (mainModule.Length != 1)
        {
            throw new InvalidOperationException("A single main method must be defined");
        }

        var mainMethod = mainModule[0].Methods.OfType<LoweredMethod>().Single(x => x.Name == "_Main");

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
            if (basicBlock.Terminator is MethodCall{Function: var function})
            {
                ShakeMethod(_methods[function.DefinitionId]);
            }
        }
    }
}
