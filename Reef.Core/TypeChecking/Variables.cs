namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public interface IVariable
    {
        StringToken Name { get; }

        ITypeReference Type { get; }
        bool ReferencedInClosure { get; set; }
    }

    public record LocalVariable(FunctionSignature? ContainingFunction, StringToken Name, ITypeReference Type, bool Instantiated, bool Mutable) : IVariable
    {
        public FunctionSignature? ContainingFunction { get;set; } = ContainingFunction;
        public bool Instantiated { get; set; } = Instantiated;
        public ITypeReference Type { get; set; } = Type;
        public bool ReferencedInClosure { get; set; }
    }

    public record FieldVariable(
        ITypeSignature ContainingSignature,
        StringToken Name,
        ITypeReference Type,
        bool Mutable,
        bool IsStaticField,
        uint FieldIndex) : IVariable
    {
        public bool ReferencedInClosure { get; set; }
    }

    public record ThisVariable(ITypeReference Type) : IVariable
    {
        public StringToken Name { get; } = Token.Identifier("this", SourceSpan.Default);
        public bool ReferencedInClosure { get; set; }
    }
}
