using Reef.Core.IL;

namespace Reef.Core;

public class TreeShaker(IReadOnlyList<ReefILModule> modules)
{
    private readonly HashSet<DefId> _usefulMethodDefIds = [];
    private readonly Dictionary<DefId, ReefMethod> _methods = modules.SelectMany(x => x.Methods).ToDictionary(x => x.Id);

    public HashSet<DefId> Shake()
    {
        var mainModule = modules.Where(x => x.MainMethod is not null).ToArray();

        if (mainModule is not [{ MainMethod: { } mainMethod }])
        {
            throw new InvalidOperationException("A single main method must be defined");
        }

        ShakeMethod(mainMethod);

        return _usefulMethodDefIds;
    } 

    private void ShakeMethod(ReefMethod method)
    {
        if (!_usefulMethodDefIds.Add(method.Id))
        {
            return;
        }

        foreach (var instruction in method.Instructions.Instructions)
        {
            if (instruction is LoadFunction loadFunction)
            {
                ShakeMethod(_methods[loadFunction.FunctionDefinitionReference.DefinitionId]);
            }
        }
    }
}
