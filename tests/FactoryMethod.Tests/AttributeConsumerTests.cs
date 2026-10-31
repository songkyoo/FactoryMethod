using Microsoft.CodeAnalysis;

using static Macaron.FactoryMethod.Tests.Helper;

namespace Macaron.FactoryMethod.Tests;

[TestFixture]
public class AttributeConsumerTests
{
    [TestCase("public")]
    [TestCase("internal")]
    [TestCase("private")]
    public void When_ExternalConsumerIgnoresConstructor_Then_CompilesAndExcludesOnlyThatFactory(
        string accessibility
    )
    {
        var sourceCode = $$"""
            using Macaron.FactoryMethod;

            namespace ExternalConsumer;

            [AutoFactory(IncludePrivateConstructors = true)]
            public partial class Sample
            {
                [IgnoreAutoFactory]
                [FactoryMethodName("invalid name")]
                {{accessibility}} Sample(int value) { }

                public Sample(string value) { }
            }
            """;

        var compilation = CompileGeneratedCode(sourceCode);
        var sampleType = compilation.GetTypeByMetadataName("ExternalConsumer.Sample")!;
        var factoryMethods = sampleType.GetMembers("Of").OfType<IMethodSymbol>().ToArray();

        Assert.That(factoryMethods, Has.Length.EqualTo(1));

        var factoryParameterType = factoryMethods[0].Parameters.Single().Type.SpecialType;

        Assert.That(factoryParameterType, Is.EqualTo(SpecialType.System_String));
    }
}
