using System.Diagnostics;
using System.Text;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public class UnknownInferredType : ITypeReference
    {
        public ITypeReference? ResolvedType { get; set; }
    }

    public class UnspecifiedSizedIntType : ITypeReference
    {
        private InstantiatedClass? _resolvedIntType;
        public InstantiatedClass? ResolvedIntType
        {
            get => _resolvedIntType;
            set
            {
                if (ReferenceEquals(value, this))
                {
                    return;
                }

                _resolvedIntType = value.NotNull();
                foreach (var link in _links)
                {
                    link.ResolvedIntType ??= value;
                }
                foreach (var link in _genericLinks.Where(x => x.ResolvedType is null))
                {
                    link.SetResolvedType(_resolvedIntType, SourceRange.Default);
                }
            }
        }

        private readonly HashSet<UnspecifiedSizedIntType> _links = [];
        private readonly HashSet<GenericTypeReference> _genericLinks = [];

        internal void Link(UnspecifiedSizedIntType other)
        {
            _links.Add(other);
            other._links.Add(this);
        }

        //internal void Link(GenericTypeReference other)
        //{
        //    if (_genericLinks.Add(other))
        //    {
        //        other.Link(this);
        //    }
        //}

        public override string ToString()
        {
            return "UnknownSizedInt";
        }
    }

    public class UnknownType : ITypeReference
    {
        public static UnknownType Instance { get; } = new();

        private UnknownType()
        {

        }
    }

    public class GenericPlaceholder : ITypeReference
    {
        public required string GenericName { get; init; }
        public required ITypeSignature OwnerType { get; init; }

        public GenericTypeReference Instantiate(ITypeReference? resolvedType = null) => new()
        {
            GenericName = GenericName,
            OwnerType = OwnerType,
            ResolvedType = resolvedType
        };

        public override string ToString() => GenericName;
    }

    public class GenericTypeReference : ITypeReference, IEquatable<GenericTypeReference>
    {
        public required string GenericName { get; init; }

        public required ITypeSignature OwnerType { get; init; }

        private ITypeReference? _resolvedType;
        public ITypeReference? ResolvedType
        {
            get => _resolvedType;
            init => _resolvedType = value;
        }

        public TypeCheckerError? SetResolvedType(ITypeReference value, SourceRange sourceRange)
        {
            if (ReferenceEquals(value, this))
            {
                return null;
            }

            _resolvedType = value;
            foreach (var link in _links.Where(x => x._resolvedType is null))
            {
                link.SetResolvedType(_resolvedType, sourceRange);
            }
            if (value is InstantiatedClass klass)
            {
                if (_intLinks.Count > 0 && InstantiatedClass.IntTypes.All(x => !x.IsSameSignature(klass)))
                {
                    return TypeCheckerError.MismatchedTypes(sourceRange, InstantiatedClass.IntTypes, value);
                }

                foreach (var link in _intLinks.Where(x => x.ResolvedIntType is null))
                {
                    link.ResolvedIntType = klass;
                }
            }
            else if (_intLinks.Count > 0)
            {
                return TypeCheckerError.MismatchedTypes(sourceRange, InstantiatedClass.IntTypes, value);
            }

            return null;
        }

        private readonly HashSet<GenericTypeReference> _links = [];
        private readonly HashSet<UnspecifiedSizedIntType> _intLinks = [];

        public void Link(GenericTypeReference other)
        {
            _links.Add(other);
            other._links.Add(this);
        }

        //public void Link(UnspecifiedSizedIntType other)
        //{
        //    if (_intLinks.Add(other))
        //    {
        //        other.Link(this);
        //    }
        //}

        public bool Equals(GenericTypeReference? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return GenericName == other.GenericName && OwnerType.Equals(other.OwnerType);
        }

        private ITypeReference? GetConcreteTypeReference()
        {
            return ResolvedType switch
            {
                null => null,
                GenericTypeReference genericTypeReference => genericTypeReference.GetConcreteTypeReference(),
                _ => ResolvedType
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{GenericName}=[");
            sb.Append(GetConcreteTypeReference()?.ToString() ?? "??");
            sb.Append(']');

            return sb.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((GenericTypeReference)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GenericName, OwnerType);
        }

        public static bool operator ==(GenericTypeReference? left, GenericTypeReference? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GenericTypeReference? left, GenericTypeReference? right)
        {
            return !Equals(left, right);
        }
    }

    public interface ITypeReference
    {
        (ITypeReference Type, DefId Id) ConcreteType()
        {

            return this switch
            {
                GenericTypeReference genericTypeReference => genericTypeReference.ResolvedType?.ConcreteType()
                    ?? throw new InvalidOperationException("No resolved type"),
                InstantiatedClass instantiatedClass => (instantiatedClass, instantiatedClass.Signature.Id),
                InstantiatedUnion instantiatedUnion => (instantiatedUnion, instantiatedUnion.Signature.Id),
                _ => throw new UnreachableException()
            };
        }
    }

}
