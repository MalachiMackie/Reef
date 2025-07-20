using FluentAssertions;
using Reef.IL;

namespace Reef.Core.Tests.ILCompilerTests;

public class Tests
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompileToIL_Should_GenerateCorrectIL(string description, string source, ReefModule expectedModule)
    {
        var tokens = Tokenizer.Tokenize(source);
        var program = Parser.Parse(tokens);
        program.Errors.Should().BeEmpty();
        var typeCheckErrors = TypeChecker.TypeCheck(program.ParsedProgram);
        typeCheckErrors.Should().BeEmpty();

        var module = ILCompile.CompileToIL(program.ParsedProgram);
        module.Should().BeEquivalentTo(
            expectedModule,
            opts => opts.For(x => x.Types)
                .Exclude(x => x.Id),
            description);
    }

    public static TheoryData<string, string, ReefModule> TestCases()
    {
        return new TheoryData<string, string, ReefModule>
        {
            {"empty module", "", Module() },
            {"empty class", "class MyClass{}", Module([Class("MyClass")])},
            {"empty union", "union MyUnion{}", Module([Union("MyUnion")])},
            {"union with unit variants", "union MyUnion{A, B}", Module([
                Union("MyUnion", [Variant("A"), Variant("B")])
            ])},
            {
                "empty top level method",
                "static fn someFn() {}",
                Module(methods: [
                    Method("someFn", isStatic: true)
                ])
            },
            {
                "empty class static method",
                "class MyClass { static fn SomeFn() {} }",
                Module([
                    Class("MyClass", methods: [Method("SomeFn", isStatic: true)])
                ])
            },
            {
                "empty union static method",
                "union MyUnion { static fn SomeFn() {} }",
                Module([
                    Union("MyUnion", methods: [Method("SomeFn", isStatic: true)])
                ])
            },
        };
    }

    private static ReefMethod Method(string name, bool isStatic = false)
    {
        return new ReefMethod
        {
            DisplayName = name,
            IsStatic = isStatic,
            TypeParameters = [],
            Instructions = [],
            Locals = [],
            Parameters = [],
            ReturnType = new ConcreteReefTypeReference
            {
                Name = "Unit",
                DefinitionId = Guid.Empty,
                TypeArguments = []
            }
        };
    }

    private static ReefVariant Variant(string name)
    {
        return new ReefVariant
        {
            DisplayName = name,
            Fields = []
        };
    }

    private static ReefTypeDefinition Union(
        string name,
        IReadOnlyList<ReefVariant>? variants = null,
        IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Methods = methods ?? [],
            Id = Guid.Empty,
            IsValueType = false,
            TypeParameters = [],
            Variants = variants ?? []
        };
    }

    private static ReefTypeDefinition Class(string name, IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefTypeDefinition
        {
            DisplayName = name,
            Id = Guid.Empty,
            Methods = methods ?? [],
            IsValueType = false,
            TypeParameters = [],
            Variants = [Variant("!ClassVariant")]
        };
    }

    private static ReefModule Module(IReadOnlyList<ReefTypeDefinition>? types = null, IReadOnlyList<ReefMethod>? methods = null)
    {
        return new ReefModule
        {
            MainMethod = null,
            Methods = methods ?? [],
            Types = types ?? []
        };
    }
}