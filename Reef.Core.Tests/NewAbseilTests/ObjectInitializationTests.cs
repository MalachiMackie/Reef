using Reef.Core.Abseil.New;
using Reef.Core.LoweredExpressions.New;
using Xunit.Abstractions;

using static Reef.Core.Tests.NewLoweredProgramHelpers;

namespace Reef.Core.Tests.NewAbseilTests;

public class ObjectInitializationTests(ITestOutputHelper testOutputHelper) : NewTestBase(testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void ObjectInitializationAbseilTest(string description, string source, NewLoweredProgram expectedProgram)
    {
        description.Should().NotBeEmpty();
        var program = CreateProgram(ModuleId, source);
        var loweredProgram = NewProgramAbseil.Lower(program);

        PrintPrograms(expectedProgram, loweredProgram);

        loweredProgram.Should().BeEquivalentTo(expectedProgram);
    }

    private const string ModuleId = "ObjectInitializationTests";

    public static TheoryData<string, string, NewLoweredProgram> TestCases()
    {
        return new()
        {
            {
                "create empty class",
                "class MyClass{} var a = new MyClass{};",
                NewLoweredProgram(
                        types: [
                            NewDataType(ModuleId, "MyClass",
                                variants: [
                                    NewVariant("_classVariant")
                                ])
                        ],
                        methods: [
                            NewMethod(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
                                [
                                    new BasicBlock(new BasicBlockId("bb0"), [
                                        new Assign(
                                            new Local("_local0"),
                                            new CreateObject(new NewLoweredConcreteTypeReference(
                                                "MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), [])))
                                    ], new GoTo(new BasicBlockId("bb1"))),
                                    new BasicBlock(new BasicBlockId("bb1"), [], new Return())
                                ],
                                Unit,
                                locals: [
                                    new NewMethodLocal(
                                        "_local0",
                                        "a",
                                        new NewLoweredConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass"), []))
                                ])
                        ])
            },
            // {
            //     "create class with fields",
            //     "class MyClass{pub field MyField: string} new MyClass{MyField = \"\"}",
            //     LoweredProgram(
            //             types: [
            //                 DataType(ModuleId, "MyClass",
            //                     variants: [
            //                         Variant("_classVariant", [Field("MyField", StringType)])
            //                     ])
            //             ],
            //             methods: [
            //                 Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                     [
            //                         CreateObject(
            //                             ConcreteTypeReference("MyClass", new DefId(ModuleId, $"{ModuleId}.MyClass")),
            //                             "_classVariant",
            //                             false,
            //                             new(){{"MyField", StringConstant("", true)}}),
            //                         MethodReturnUnit()
            //                     ])
            //             ])
            // },
            // {
            //     "Create unit union variant",
            //     "union MyUnion{A} MyUnion::A",
            //     LoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [Variant("A", [Field("_variantIdentifier", UInt16_t)])])
            //         ],
            //         methods: [
            //             Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     CreateObject(
            //                         ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                         "A",
            //                         false,
            //                         new(){{"_variantIdentifier", UInt16Constant(0, true)}}),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // },
            // {
            //     "Create class union variant",
            //     "union MyUnion{A {field a: string}} new MyUnion::A { a = \"hi\"};",
            //     LoweredProgram(
            //         types: [
            //             DataType(ModuleId, "MyUnion",
            //                 variants: [
            //                     Variant("A", [
            //                         Field("_variantIdentifier", UInt16_t),
            //                         Field("a", StringType),
            //                     ])
            //                 ])
            //         ],
            //         methods: [
            //             Method(new DefId(ModuleId, $"{ModuleId}._Main"), "_Main",
            //                 [
            //                     CreateObject(
            //                         ConcreteTypeReference("MyUnion", new DefId(ModuleId, $"{ModuleId}.MyUnion")),
            //                         "A",
            //                         false,
            //                         new()
            //                         {
            //                             {"_variantIdentifier", UInt16Constant(0, true)},
            //                             {"a", StringConstant("hi", true)},
            //                         }),
            //                     MethodReturnUnit()
            //                 ])
            //         ])
            // }
        };
    }
}
