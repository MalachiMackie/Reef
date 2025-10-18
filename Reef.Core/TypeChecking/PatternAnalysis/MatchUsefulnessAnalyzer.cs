// This is entirely copied from Rust's pattern matching exhaustiveness checking.
// Relevant reads:
// https://rustc-dev-guide.rust-lang.org/pat-exhaustive-checking.html
// https://doc.rust-lang.org/nightly/nightly-rustc/src/rustc_pattern_analysis/usefulness.rs.html#1-1882
// https://github.com/rust-lang/rust/blob/master/src/tools/rust-analyzer/crates/hir-ty/src/diagnostics/match_check/pat_analysis.rs
// https://github.com/rust-lang/rust/blob/master/compiler/rustc_pattern_analysis/src/usefulness.rs
// https://blog.rust-lang.org/2016/04/19/MIR/
// https://smallcultfollowing.com/babysteps/blog/2018/08/13/never-patterns-exhaustive-matching-and-uninhabited-types-oh-my/

using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking.PatternAnalysis;


public class UsefulnessReport
{
    public required IReadOnlyList<(PatternMatchArm, IUsefulness)> ArmUsefulness { get; set; }
    public required IReadOnlyList<WitnessPattern> NonExhaustivenessWitnesses { get; set; }
    public required IReadOnlyList<HashSet<uint>> ArmIntersections { get; set; }
}

public class RedundancyExplanation
{
    public required IReadOnlyList<DeconstructedPattern> CoveredBy { get; set; }
}

public interface IUsefulness;
public record Useful(IReadOnlyList<(DeconstructedPattern, RedundancyExplanation)> OrPatternRedundancies) : IUsefulness;
public record Redundant(RedundancyExplanation Explanation) : IUsefulness;

public class BranchPatternUsefulness
{
    public required bool Useful { get; set; }
    public required HashSet<DeconstructedPattern> CoveredBy { get; init; }

    public RedundancyExplanation? IsRedundant()
    {
        if (Useful)
        {
            return null;
        }

        var coveredBy = CoveredBy.OrderBy(x => x.Id).ToList();
        return new RedundancyExplanation
        {
            CoveredBy = coveredBy
        };
    }

    public void Update(MatrixRow row, Matrix matrix)
    {
        Useful |= row.Useful;

        foreach (var innerRow in row.IntersectsAtLeast.Select(rowId => matrix.Rows[(int)rowId]))
        {
            if (innerRow is
                {
                    Useful: true, IsUnderGuard: false, Head: PatternPatternOrWild
                    {
                        Pattern: { } intersecting
                    }
                })
            {
                CoveredBy.Add(intersecting);
            }
        }
    }
}

public class UsefulnessContext
{
    public required Dictionary<PatternId, BranchPatternUsefulness> BranchUsefulness { get; set; }
    public required uint ComplexityLimit { get; set; }
    public required uint ComplexityLevel { get; set; }

    public void IncreaseComplexityLevel(uint increase)
    {
        ComplexityLevel += increase;
        if (ComplexityLevel > ComplexityLimit)
        {
            throw new InvalidOperationException("Complexity Exceeded");
        }
    }
}

public class PlaceContext
{
    public required TypeChecker.ITypeReference Type { get; set; }

    public WitnessPattern WildFromCtor(IConstructor ctor)
    {
        return WitnessPattern.WildFromCtor(ctor, Type);
    }
}

public interface IPatternOrWild
{
    public IConstructor Ctor()
    {
        return this switch
        {
            Wild => new WildcardConstructor(),
            PatternPatternOrWild { Pattern: { } pattern } => pattern.Constructor,
            _ => throw new UnreachableException($"{GetType()}")
        };
    }

    IEnumerable<IPatternOrWild> Specialize(IConstructor ctor, int ctorArity)
    {
        return this switch
        {
            Wild => Enumerable.Range(0, ctorArity).Select(_ => new Wild()),
            PatternPatternOrWild { Pattern: { } pat } => pat.Specialize(ctor, ctorArity),
            _ => throw new UnreachableException($"{GetType()}")
        };
    }
}

public record Wild : IPatternOrWild;

public record PatternPatternOrWild(DeconstructedPattern Pattern) : IPatternOrWild;

public class PatternStack
{
    public IReadOnlyList<IPatternOrWild> Patterns { get; set; }
    public bool Relevant { get; set; }

    public IPatternOrWild Head => Patterns[0];

    public PatternStack(DeconstructedPattern pattern)
    {
        Patterns = [new PatternPatternOrWild(pattern)];
        Relevant = true;
    }

    public PatternStack(IReadOnlyList<IPatternOrWild> patterns, bool relevant)
    {
        Patterns = patterns;
        Relevant = relevant;
    }

    public PatternStack PopHeadConstructor(IConstructor ctor, int ctorArity, bool ctorIsRelevant)
    {
        var headPat = Head;
        if (headPat is PatternPatternOrWild { Pattern: { } pat } && pat.Arity > ctorArity)
        {
            throw new InvalidOperationException($"Uncaught type error: pattern {pat} has inconsistent arity");
        }

        var newPats = headPat.Specialize(ctor, ctorArity).ToList();
        newPats.AddRange(Patterns.Skip(1));

        ctorIsRelevant |= Head.Ctor() is WildcardConstructor;

        return new PatternStack(newPats, Relevant && ctorIsRelevant);
    }
}

public class MatrixRow
{
    public PatternStack Patterns { get; set; }
    public bool IsUnderGuard { get; set; }
    public uint ParentRow { get; set; }
    public bool Useful { get; set; }
    public HashSet<uint> IntersectsAtLeast { get; set; }
    public bool HeadIsBranch { get; set; }

    public int Count => Patterns.Patterns.Count;
    public IPatternOrWild Head => Patterns.Patterns[0];

    public MatrixRow(PatternMatchArm arm, uint armId)
    {
        Patterns = new PatternStack(arm.Pattern);
        ParentRow = armId;
        // todo: arm.hasGuard
        IsUnderGuard = false;
        Useful = false;
        IntersectsAtLeast = [];
        HeadIsBranch = true;
    }

    public MatrixRow(PatternStack patterns,
        bool isUnderGuard,
        uint parentRow,
        bool useful,
        HashSet<uint> intersectsAtLeast,
        bool headIsBranch)
    {
        Patterns = patterns;
        IsUnderGuard = isUnderGuard;
        ParentRow = parentRow;
        Useful = useful;
        IntersectsAtLeast = intersectsAtLeast;
        HeadIsBranch = headIsBranch;
    }

    public MatrixRow PopHeadConstructor(IConstructor ctor, int ctorArity, bool ctorIsRelevant, int parentRow)
    {
        return new MatrixRow(
            Patterns.PopHeadConstructor(ctor, ctorArity, ctorIsRelevant),
            IsUnderGuard,
            (uint)parentRow,
            useful: false,
            intersectsAtLeast: [],
            headIsBranch: false);
    }
}

public enum PlaceValidity
{
    ValidOnly,
    MaybeInvalid
}

public static class PlaceValidityExtensions
{
    public static PlaceValidity Specialize(this PlaceValidity validity, IConstructor ctor)
    {
        // rust implementation has a different case for when ctor is of types that we don't support
        return validity;
    }
}

public class PlaceInfo
{
    public required TypeChecker.ITypeReference Type { get; set; }
    public required bool PrivateUninhabited { get; set; }
    public required PlaceValidity Validity { get; set; }
    public required bool IsScrutinee { get; set; }

    public (IReadOnlyList<IConstructor> splitCtors, IReadOnlyList<IConstructor> missingCtors) SplitColumnCtors(
        IEnumerable<IConstructor> ctors)
    {
        if (PrivateUninhabited)
        {
            return ([new PrivateUninhabitedConstructor()], []);
        }

        var ctorsForType = IConstructor.CtorsForType(Type);
        var isTopLevelException = IsScrutinee && ctorsForType is NoConstructorsConstructorSet;
        var emptyArmsAreUnreachable = Validity == PlaceValidity.ValidOnly
            && isTopLevelException;
        var canOmitEmptyArms = Validity == PlaceValidity.ValidOnly
                               || isTopLevelException;

        var splitSet = ctorsForType.Split(ctors);
        var allMissing = splitSet.Present.Count == 0;

        var splitCtors = splitSet.Present;
        if (!(splitSet.Missing.Count == 0 && (splitSet.MissingEmpty.Count == 0 || emptyArmsAreUnreachable)))
        {
            splitCtors.Add(new MissingConstructor());
        }

        var missingCtors = splitSet.Missing;
        if (!canOmitEmptyArms)
        {
            missingCtors.AddRange(splitSet.MissingEmpty);
            splitSet.MissingEmpty.Clear();
        }

        var reportIndividualMissingCtors = IsScrutinee || !allMissing;
        if (missingCtors.Count > 0 && !reportIndividualMissingCtors)
        {
            missingCtors = [
                new WildcardConstructor(),
            ];
        }
        else if (missingCtors.Any(x => x.IsNonExhaustive()))
        {
            missingCtors = [new NonExhaustiveConstructor()];
        }

        return (splitCtors, missingCtors);
    }

    public IReadOnlyList<PlaceInfo> Specialize(IConstructor ctor)
    {
        var ctorSubTypes = IConstructor.CtorSubTypes(ctor, Type);
        var ctorSubValidity = Validity.Specialize(ctor);

        return ctorSubTypes.Select(x => new PlaceInfo
        {
            Type = x.Item1,
            PrivateUninhabited = x.Item2.Value,
            Validity = ctorSubValidity,
            IsScrutinee = false
        }).ToArray();
    }
}

public class Matrix
{
    public List<MatrixRow> Rows { get; set; }
    public IReadOnlyList<PlaceInfo> PlaceInfo { get; set; }
    public bool WildcardRowIsRelevant { get; set; }

    public int ColumnCount => PlaceInfo.Count;

    public PlaceInfo? HeadPlace => PlaceInfo.Count > 0 ? PlaceInfo[0] : null;

    public IEnumerable<IPatternOrWild> Heads()
    {
        return Rows.Select(x => x.Head);
    }

    private Matrix(List<MatrixRow> rows, IReadOnlyList<PlaceInfo> placeInfo, bool wildcardRowIsRelevant)
    {
        Rows = rows;
        PlaceInfo = placeInfo;
        WildcardRowIsRelevant = wildcardRowIsRelevant;
    }

    private Matrix(
        int rowCount,
        TypeChecker.ITypeReference scrutineeType,
        PlaceValidity scrutValidity)
    {
        PlaceInfo = [new PlaceInfo
        {
            Type = scrutineeType,
            Validity = scrutValidity,
            IsScrutinee = true,
            PrivateUninhabited = false
        }];
        WildcardRowIsRelevant = true;
        Rows = new List<MatrixRow>(rowCount);
    }

    public void Push(MatrixRow row)
    {
        row.IntersectsAtLeast = new HashSet<uint>(Rows.Count);
        Rows.Add(row);
    }

    public static Matrix Create(IReadOnlyList<PatternMatchArm> arms,
        TypeChecker.ITypeReference scrutineeType,
        PlaceValidity scrutValidity)
    {
        var matrix = new Matrix(arms.Count, scrutineeType, scrutValidity);

        for (var i = 0; i < arms.Count; i++)
        {
            var arm = arms[i];
            matrix.Push(new MatrixRow(arm, (uint)i));
        }

        return matrix;
    }

    public Matrix SpecializeConstructor(PlaceContext pcx, IConstructor ctor, bool ctorIsRelevant)
    {
        var subfieldPlaceInfo = PlaceInfo[0].Specialize(ctor);
        var arity = subfieldPlaceInfo.Count();
        var specializedPlaceInfo = subfieldPlaceInfo.Concat(PlaceInfo.Skip(1)).ToArray();
        var matrix = new Matrix([], specializedPlaceInfo, WildcardRowIsRelevant && ctorIsRelevant);
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            if (ctor.IsCoveredBy(row.Head.Ctor()))
            {
                var newRow = row.PopHeadConstructor(ctor, arity, ctorIsRelevant, i);
                matrix.Push(newRow);
            }
        }

        return matrix;
    }

    public void Unspecialize(Matrix specMatrix)
    {
        foreach (var childRow in specMatrix.Rows)
        {
            var parentRowId = childRow.ParentRow;
            var parentRow = Rows[(int)parentRowId];
            parentRow.Useful |= childRow.Useful;
            foreach (var parentIntersection in childRow.IntersectsAtLeast
                         .Select(childIntersection => specMatrix.Rows[(int)childIntersection].ParentRow)
                         .Where(parentIntersection => parentIntersection != parentRowId))
            {
                parentRow.IntersectsAtLeast.Add(parentIntersection);
            }
        }
    }
}

public record WitnessStack(List<WitnessPattern> Patterns)
{
    public WitnessPattern SinglePattern()
    {
        if (Patterns.Count != 1)
        {
            throw new InvalidOperationException("Expected Witness Stack to only contain a single pattern");
        }

        return Patterns[0];
    }

    public void PushPattern(WitnessPattern pattern)
    {
        Patterns.Add(pattern);
    }

    public IEnumerable<WitnessStack> ApplyConstructor(PlaceContext pcx, IConstructor ctor)
    {
        var length = Patterns.Count;
        var arity = ctor.Arity(pcx.Type);
        var fields = new List<WitnessPattern>();
        var fieldsStartIndex = length - arity;
        for (var i = length - 1; i >= fieldsStartIndex; i--)
        {
            var pattern = Patterns[i];
            Patterns.Remove(pattern);
            fields.Add(pattern);
        }

        if (Patterns.Count + fields.Count != length)
        {
            throw new InvalidOperationException("Wrong number of fields");
        }

        Patterns.Add(new WitnessPattern
        {
            Constructor = ctor,
            Fields = fields,
            Type = pcx.Type
        });

        return [this];
    }
}

public record WitnessMatrix(List<WitnessStack> Stacks)
{
    public static WitnessMatrix Empty => new([]);
    public static WitnessMatrix UnitWitness => new([new WitnessStack([])]);

    public IReadOnlyList<WitnessPattern> SingleColumn()
    {
        return Stacks.Select(x => x.SinglePattern()).ToArray();
    }

    public WitnessMatrix ApplyConstructor(PlaceContext pcx, IReadOnlyList<IConstructor> missingCtors, IConstructor ctor)
    {
        if (Stacks.Count == 0/* || ctor is OrConstructor*/)
        {
            return this;
        }

        if (ctor is MissingConstructor)
        {
            var ret = Empty;
            foreach (var missingCtor in missingCtors)
            {
                var pattern = pcx.WildFromCtor(missingCtor);
                var witMatrix = new WitnessMatrix([.. Stacks]);
                witMatrix.PushPattern(pattern);
                ret.Add(witMatrix);
            }

            return ret;
        }

        var stacks = Stacks.ToList();
        Stacks.Clear();
        foreach (var witness in stacks)
        {
            Stacks.AddRange(witness.ApplyConstructor(pcx, ctor));
        }

        return this;
    }

    public void PushPattern(WitnessPattern pattern)
    {
        foreach (var witness in Stacks)
        {
            witness.PushPattern(pattern);
        }
    }

    public void Add(WitnessMatrix witnesses)
    {
        Stacks.AddRange(witnesses.Stacks);
    }
}

public static class MatchUsefulnessAnalyzer
{
    public static UsefulnessReport ComputeMatchUsefulness(
        IReadOnlyList<MatchArm> matchArms,
        TypeChecker.ITypeReference scrutineeType,
        PlaceValidity scrutValidity,
        uint complexityLimit)
    {
        var cx = new UsefulnessContext
        {
            BranchUsefulness = [],
            ComplexityLimit = complexityLimit,
            ComplexityLevel = 0
        };

        var arms = matchArms.Select(y => new PatternMatchArm(LowerPattern(y.Pattern), HasGuard: false))
            .ToArray();

        var matrix = Matrix.Create(arms, scrutineeType, scrutValidity);
        var nonExhaustivenessWitness = ComputeExhaustivenessAndUsefulness(cx, matrix)
            .SingleColumn();

        var armUsefulness = arms.Select(arm =>
        {
            IUsefulness usefulness;
            if (cx.BranchUsefulness[arm.Pattern.Id].IsRedundant() is { } redundancyExplanation)
            {
                usefulness = new Redundant(redundancyExplanation);
            }
            else
            {
                var redundantSubPatterns = new List<(DeconstructedPattern, RedundancyExplanation)>();
                arm.Pattern.Walk((subPattern) =>
                {
                    if (cx.BranchUsefulness.TryGetValue(subPattern.Id, out var subPatternUsefulness)
                        && subPatternUsefulness.IsRedundant() is { } subPatRedundancyExplanation)
                    {
                        redundantSubPatterns.Add((subPattern, subPatRedundancyExplanation));
                        return false;
                    }

                    return true;
                });
                usefulness = new Useful(redundantSubPatterns);
            }

            return (arm, usefulness);
        }).ToArray();

        var armIntersections = matrix.Rows
            .Select(row => row.IntersectsAtLeast)
            .ToArray();

        return new UsefulnessReport
        {
            ArmUsefulness = armUsefulness,
            NonExhaustivenessWitnesses = nonExhaustivenessWitness,
            ArmIntersections = armIntersections
        };
    }

    private static WitnessMatrix ComputeExhaustivenessAndUsefulness(UsefulnessContext cx, Matrix matrix)
    {
        if (matrix.Rows.Any(x => x.Count != matrix.ColumnCount))
        {
            throw new InvalidOperationException("Expected all rows to have the same length as the number of columns");
        }

        if (!matrix.WildcardRowIsRelevant && matrix.Rows.All(x => !x.Patterns.Relevant))
        {
            return WitnessMatrix.Empty;
        }

        if (matrix.HeadPlace is not { } place)
        {
            cx.IncreaseComplexityLevel((uint)matrix.Rows.Count);
            var useful = true;
            for (var i = 0; i < matrix.Rows.Count; i++)
            {
                var row = matrix.Rows[i];
                for (uint j = 0; j < i; j++)
                {
                    row.IntersectsAtLeast.Add(j);
                }

                useful &= row.IsUnderGuard;
            }

            return useful && matrix.WildcardRowIsRelevant
                ? WitnessMatrix.UnitWitness
                : WitnessMatrix.Empty;
        }

        var ctors = matrix.Heads().Select(x => x.Ctor());
        var (splitCtors, missingCtors) = place.SplitColumnCtors(ctors);
        var ty = place.Type;
        var pcx = new PlaceContext { Type = ty };
        var ret = WitnessMatrix.Empty;
        foreach (var ctor in splitCtors)
        {
            var ctorIsRelevant = ctor is MissingConstructor || missingCtors.Count == 0;
            var specMatrix = matrix.SpecializeConstructor(pcx, ctor, ctorIsRelevant);
            var witnesses = ComputeExhaustivenessAndUsefulness(cx, specMatrix);

            witnesses = witnesses.ApplyConstructor(pcx, missingCtors, ctor);
            ret.Add(witnesses);

            matrix.Unspecialize(specMatrix);
        }

        foreach (var row in matrix.Rows.Where(x => x.HeadIsBranch))
        {
            if (row.Head is PatternPatternOrWild { Pattern: { } pattern })
            {
                if (!cx.BranchUsefulness.TryGetValue(pattern.Id, out var usefulness))
                {
                    usefulness = new BranchPatternUsefulness
                    {
                        CoveredBy = [],
                        Useful = false
                    };
                    cx.BranchUsefulness[pattern.Id] = usefulness;
                }

                usefulness.Update(row, matrix);
            }
        }

        return ret;
    }

    public static DeconstructedPattern LowerPattern(IPattern pattern)
    {
        IConstructor constructor;
        uint arity;
        IReadOnlyList<IndexedPattern> fields;
        var type = pattern.TypeReference
            ?? throw new InvalidOperationException("Expected pattern type");

        switch (pattern)
        {
            case TypePattern:
                {
                    arity = 0;
                    fields = [];
                    // type pattern creates a wildcard constructor because it is guaranteed to match the type is was provided for
                    constructor = new WildcardConstructor();
                    break;
                }
            case UnionVariantPattern { VariantName.StringValue: { } variantName }:
                {
                    var unionType = type as TypeChecker.InstantiatedUnion
                                    ?? throw new InvalidOperationException("Expected union type");
                    var (index, variant) = unionType.Variants
                        .Index()
                        .FirstOrDefault(x => x.Item.Name == variantName);
                    constructor = new VariantConstructor((uint)index);
                    fields = variant switch
                    {
                        TypeChecker.ClassUnionVariant classUnionVariant =>
                            classUnionVariant.Fields
                                .Index()
                                .Select(x => new IndexedPattern
                                {
                                    Index = (uint)x.Index,
                                    // because the current pattern does not specify any fields,
                                    // we treat each field as if it's discarded
                                    Pattern = LowerPattern(new DiscardPattern(SourceRange.Default))
                                })
                                .ToArray(),
                        TypeChecker.TupleUnionVariant tupleUnionVariant =>
                            tupleUnionVariant.TupleMembers
                                .Index()
                                .Select(x => new IndexedPattern()
                                {
                                    Index = (uint)x.Index,
                                    Pattern = LowerPattern(new DiscardPattern(SourceRange.Default))
                                })
                                .ToArray(),
                        TypeChecker.UnitUnionVariant => [],
                        _ => throw new ArgumentOutOfRangeException(nameof(variant))
                    };

                    arity = (uint)fields.Count;
                    break;
                }
            case UnionTupleVariantPattern
            {
                VariantName.StringValue: { } variantName,
                TupleParamPatterns: { } tupleParamPatterns
            }:
                {
                    var unionType = type as TypeChecker.InstantiatedUnion
                                    ?? throw new InvalidOperationException("Expected union type");
                    var index = unionType.Variants
                        .Index()
                        .FirstOrDefault(x => x.Item.Name == variantName).Index;
                    constructor = new VariantConstructor((uint)index);
                    // we assume that every tuple member has been specified, so we don't need to get it from type
                    fields = tupleParamPatterns.Index()
                        .Select(x => new IndexedPattern
                        {
                            Index = (uint)x.Index,
                            Pattern = LowerPattern(x.Item)
                        })
                        .ToArray();
                    arity = (uint)fields.Count;
                    break;

                }
            case UnionClassVariantPattern
            {
                VariantName.StringValue: { } variantName,
                FieldPatterns: { } fieldPatterns
            }:
                {
                    var unionType = type as TypeChecker.InstantiatedUnion
                                    ?? throw new InvalidOperationException("Expected union type");
                    var (index, variant) = unionType.Variants
                        .Index()
                        .FirstOrDefault(x => x.Item.Name == variantName);
                    if (variant is not TypeChecker.ClassUnionVariant classUnionVariant)
                    {
                        throw new InvalidOperationException("Expected class union variant");
                    }
                    constructor = new VariantConstructor((uint)index);

                    fields = classUnionVariant.Fields
                        .Index()
                        .Select(x => new IndexedPattern
                        {
                            Index = (uint)x.Index,
                            // because the current pattern does not specify any fields,
                            // we treat each field as if it's discarded
                            Pattern = LowerPattern(
                                fieldPatterns.FirstOrDefault(y => y.FieldName.StringValue == x.Item.Name)?.Pattern
                                ?? new DiscardPattern(SourceRange.Default))
                        })
                        .ToArray();

                    arity = (uint)fields.Count;
                    break;
                }
            case DiscardPattern:
            case VariableDeclarationPattern:
                {
                    arity = 0;
                    fields = [];
                    constructor = new WildcardConstructor();
                    break;
                }
            case ClassPattern { FieldPatterns: { } fieldPatterns }:
                {
                    constructor = new ClassConstructor();
                    fields = (type as TypeChecker.InstantiatedClass)?.Fields
                        .Index()
                        .Select(x => new IndexedPattern
                        {
                            Index = (uint)x.Index,
                            Pattern = LowerPattern(
                                fieldPatterns.FirstOrDefault(y => y.FieldName.StringValue == x.Item.Name)?.Pattern
                                    ?? new DiscardPattern(SourceRange.Default))
                        }).ToArray()
                        ?? throw new InvalidOperationException("Expected class");
                    arity = (uint)fields.Count;
                    break;
                }
            default:
                throw new UnreachableException($"{pattern.GetType()}");
        }

        return new DeconstructedPattern
        {
            Arity = arity,
            TypeReference = type,
            Constructor = constructor,
            Fields = fields,
            PatternData = pattern
        };
    }
}
