using Microsoft.CodeAnalysis;

using static Macaron.FactoryMethod.Tests.Helper;

namespace Macaron.FactoryMethod.Tests;

[TestFixture]
public class FactoryMethodGeneratorIncrementalTests
{
    [Test]
    public void When_OneAutoFactoryTargetChanges_Then_UnchangedTargetUsesCachedOutput()
    {
        AssertUnchangedTargetUsesCachedOutput(
            initialSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Foo
            {
                public Foo(int value) { }
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Foo
            {
                public Foo(int value, string name) { }
            }
            """,
            unchangedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Bar
            {
                public Bar(string value) { }
            }
            """
        );
    }

    [Test]
    public void When_OneFactoryMethodNameChanges_Then_UnchangedTargetUsesCachedOutput()
    {
        AssertUnchangedTargetUsesCachedOutput(
            initialSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Foo
            {
                [FactoryMethodName("FromInt")]
                public Foo(int value) { }
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Foo
            {
                [FactoryMethodName("FromValue")]
                public Foo(int value) { }
            }
            """,
            unchangedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Bar
            {
                [FactoryMethodName("FromString")]
                public Bar(string value) { }
            }
            """
        );
    }

    private static void AssertUnchangedTargetUsesCachedOutput(
        string initialSourceCode,
        string updatedSourceCode,
        string unchangedSourceCode
    )
    {
        var initialSyntaxTree = ParseSyntaxTree(initialSourceCode);
        var updatedSyntaxTree = ParseSyntaxTree(updatedSourceCode);
        var unchangedSyntaxTree = ParseSyntaxTree(unchangedSourceCode);

        var initialCompilation = CreateCompilation(initialSyntaxTree, unchangedSyntaxTree);
        var updatedCompilation = initialCompilation.ReplaceSyntaxTree(
            initialSyntaxTree,
            updatedSyntaxTree
        );
        var driver = CreateGeneratorDriver(trackIncrementalSteps: true);

        driver = RunGenerator(driver, initialCompilation);
        driver = RunGenerator(driver, updatedCompilation);

        var result = GetGeneratorResult(driver);
        var outputReasons = GetTrackedOutputReasons(result, "SourceOutput");

        NUnit.Framework.Assert.That(
            outputReasons,
            Does.Contain(IncrementalStepRunReason.Cached)
        );
    }
}
