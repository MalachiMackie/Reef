using System.ComponentModel;

namespace Reef.Core.TypeChecking.PatternAnalysis;

public class SplitConstructorSet
{
    public required List<IConstructor> Present { get; set; }
    public required List<IConstructor> Missing { get; set; }
    public required List<IConstructor> MissingEmpty { get; set; }
}

public interface IConstructorSet
{
    SplitConstructorSet Split(IEnumerable<IConstructor> constructors)
    {
        var present = new List<IConstructor>(capacity: 1);
        var missing = new List<IConstructor>();
        var missingEmpty = new List<IConstructor>();
        IReadOnlyList<IConstructor> seen = [..constructors.Where(ctor => ctor is not WildcardConstructor)];

        switch (this)
        {
            case ClassConstructorSet { Empty: var empty }:
            {
                if (seen.Count > 0)
                {
                    present.Add(new ClassConstructor());
                }
                else if (empty)
                {
                    missingEmpty.Add(new ClassConstructor());
                }
                else
                {
                    missing.Add(new ClassConstructor());
                }

                break;
            }
            case VariantsConstructorSet { Variants: var variants, NonExhaustive: var nonExhaustive }:
            {
                var seenVariants = new HashSet<uint>(variants.Count);
                foreach (var index in seen.Select(x => x.AsVariant()).OfType<uint>())
                {
                    seenVariants.Add(index);
                }

                var skippedAHiddenVariant = false;
                for (uint i = 0; i < variants.Count; i++)
                {
                    var visibility = variants[(int)i];
                    var ctor = new VariantConstructor(i);
                    if (seenVariants.Contains(i))
                    {
                        present.Add(ctor);
                    }
                    else
                    {
                        switch (visibility)
                        {
                            case VariantVisibility.Visible:
                                missing.Add(ctor);
                                break;
                            case VariantVisibility.Hidden:
                                skippedAHiddenVariant = true;
                                break;
                            case VariantVisibility.Empty:
                                missingEmpty.Add(ctor);
                                break;
                            default:
                                throw new InvalidEnumArgumentException(nameof(visibility), (int)visibility, typeof(VariantVisibility));
                        }
                    }
                }

                if (skippedAHiddenVariant)
                {
                    missing.Add(new HiddenConstructor());
                }

                break;
            }
            case BooleanConstructorSet:
            {
                var seenFalse = false;
                var seenTrue = false;
                foreach (var b in seen.Select(x => x.AsBool()).OfType<bool>())
                {
                    if (b)
                    {
                        seenTrue = true;
                    }
                    else
                    {
                        seenFalse = true;
                    }
                }

                if (seenFalse)
                {
                    present.Add(new BooleanConstructor(false));
                }
                else
                {
                    missing.Add(new BooleanConstructor(false));
                }

                if (seenTrue)
                {
                    present.Add(new BooleanConstructor(true));
                }
                else
                {
                    missing.Add(new BooleanConstructor(true));
                }
                
                break;
            }
            case UnlistableConstructorSet:
            {
                present.AddRange(seen);
                missing.Add(new NonExhaustiveConstructor());
                break;
            }
            case NoConstructorsConstructorSet:
            {
                missingEmpty.Add(new NeverConstructor());
                break;
            }
        }

        return new SplitConstructorSet
        {
            Missing = missing,
            Present = present,
            MissingEmpty = missingEmpty
        };
    }

    bool AllEmpty()
    {
        return this switch
        {
            BooleanConstructorSet or UnlistableConstructorSet => false,
            NoConstructorsConstructorSet => true,
            ClassConstructorSet { Empty: var empty } => empty,
            VariantsConstructorSet { Variants: var variants, NonExhaustive: var nonExhaustive } => !nonExhaustive &&
                variants.All(x => x == VariantVisibility.Empty),
            _ => throw new InvalidOperationException($"{GetType()}")
        };
    }
}

public record ClassConstructorSet(bool Empty) : IConstructorSet;
public record VariantsConstructorSet(IReadOnlyList<VariantVisibility> Variants, bool NonExhaustive) : IConstructorSet;

public record BooleanConstructorSet : IConstructorSet;

public record UnlistableConstructorSet : IConstructorSet;
public record NoConstructorsConstructorSet : IConstructorSet;
