namespace Reef.Core.TypeChecking.PatternAnalysis;

public class IndexedPattern
{
    public required uint Index { get; set; }
    public required DeconstructedPattern Pattern { get; set; }
}

public class DeconstructedPattern
{
    public required IConstructor Constructor { get; set; }
    public required IReadOnlyList<IndexedPattern> Fields { get; set; }
    public required uint Arity { get; set; }
    public required TypeChecker.ITypeReference TypeReference { get; set; }
    public required IPattern PatternData { get; set; }
    public Guid Id { get; } = Guid.NewGuid();

    public void Walk(Func<DeconstructedPattern, bool> path)
    {
        if (!path(this))
        {
            return;
        }

        foreach (var field in Fields)
        {
            field.Pattern.Walk(path);
        }
    }

    public IEnumerable<IPatternOrWild> Specialize(IConstructor ctor, int ctorArity)
    {
        if (ctor is PrivateUninhabitedConstructor)
        {
            return [];
        }

        var fields = Enumerable.Range(0, ctorArity).Select(IPatternOrWild (_) => new Wild()).ToList();
        foreach (var innerPattern in Fields)
        {
            fields[(int)innerPattern.Index] = new PatternPatternOrWild(innerPattern.Pattern);
        }

        return fields;
    }
}

public record PatternMatchArm(DeconstructedPattern Pattern, bool HasGuard)
{
}

public class WitnessPattern
{
    public required IConstructor Constructor { get; set; }
    public required IReadOnlyList<WitnessPattern> Fields { get; set; }
    public required TypeChecker.ITypeReference Type { get; set; }

    public static WitnessPattern WildFromCtor(IConstructor ctor, TypeChecker.ITypeReference type)
    {
        if (ctor is WildcardConstructor)
        {
            return Wildcard(type);
        }

        var fields = IConstructor.CtorSubTypes(ctor, type)
            .Where(tuple => !tuple.Item2.Value)
            .Select(tuple => Wildcard(tuple.Item1))
            .ToList();

        return new WitnessPattern
        {
            Constructor = ctor,
            Fields = fields,
            Type = type
        };
    }

    public static WitnessPattern Wildcard(TypeChecker.ITypeReference type)
    {
        var isEmpty = IConstructor.CtorsForType(type).AllEmpty();
        IConstructor ctor = isEmpty ? new NeverConstructor() : new WildcardConstructor();
        return new WitnessPattern
        {
            Constructor = ctor,
            Fields = [],
            Type = type
        };
    }
}
