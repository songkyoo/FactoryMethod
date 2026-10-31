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

    [Test]
    public void When_OneAutoFactoryMethodNameChanges_Then_UnchangedTargetUsesCachedOutput()
    {
        AssertUnchangedTargetUsesCachedOutput(
            initialSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory(methodName: "Create")]
            public partial class Foo
            {
                public Foo(int value) { }
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory(methodName: "FromValue")]
            public partial class Foo
            {
                public Foo(int value) { }
            }
            """,
            unchangedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory(methodName: "FromString")]
            public partial class Bar
            {
                public Bar(string value) { }
            }
            """
        );
    }

    [Test]
    public void When_DefaultValueChanges_Then_UnchangedTargetUsesCachedOutput()
    {
        AssertUnchangedTargetUsesCachedOutput(
            initialSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;
            [AutoFactory]
            public partial class Foo { public Foo(int value = 1) { } }
            """,
            updatedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;
            [AutoFactory]
            public partial class Foo { public Foo(int value = 2) { } }
            """,
            unchangedSourceCode:
            """
            namespace Macaron.FactoryMethod.Tests;
            [AutoFactory]
            public partial class Bar { public Bar(int value = 3) { } }
            """
        );
    }

    [TestCase("(Mode)1", "(global::Macaron.FactoryMethod.Tests.Mode)(1)", false)]
    [TestCase("Mode.Ready", "global::Macaron.FactoryMethod.Tests.Mode.Ready", true)]
    public void When_EnumDeclarationChanges_Then_UpdatesOnlyAffectedOutput(
        string defaultExpression,
        string expectedExpression,
        bool outputIsUnchanged
    )
    {
        var initialEnum = ParseSyntaxTree("namespace Macaron.FactoryMethod.Tests; public enum Mode { Ready = 1 }");
        var updatedEnum = ParseSyntaxTree("namespace Macaron.FactoryMethod.Tests; public enum Mode { Ready = 2 }");
        var target = ParseSyntaxTree($$"""
            namespace Macaron.FactoryMethod.Tests;
            [AutoFactory]
            public partial class Foo { public Foo(Mode? value = {{defaultExpression}}) { } }
            """);
        var otherTarget = ParseSyntaxTree("""
            namespace Macaron.FactoryMethod.Tests;
            [AutoFactory]
            public partial class Bar { public Bar(int value = 3) { } }
            """);
        var initialCompilation = CreateCompilation(initialEnum, target, otherTarget);
        var updatedCompilation = initialCompilation.ReplaceSyntaxTree(initialEnum, updatedEnum);
        var driver = CreateGeneratorDriver(trackIncrementalSteps: true);

        driver = RunGenerator(driver, initialCompilation);
        driver = driver.RunGeneratorsAndUpdateCompilation(updatedCompilation, out var outputCompilation, out var diagnostics);

        Assert.That(diagnostics, Is.Empty);
        Assert.That(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty);

        var result = GetGeneratorResult(driver);

        Assert.That(
            GetTrackedOutputReasons(result, "SourceOutput"),
            Is.EquivalentTo(new[]
            {
                outputIsUnchanged ? IncrementalStepRunReason.Cached : IncrementalStepRunReason.New,
                IncrementalStepRunReason.Cached,
            })
        );
        Assert.That(
            result.GeneratedSources.Single(s => s.HintName.StartsWith("Foo_")).SourceText.ToString(),
            Does.Contain($"value = {expectedExpression}")
        );

        var foo = outputCompilation.GetTypeByMetadataName("Macaron.FactoryMethod.Tests.Foo")!;
        var factory = (IMethodSymbol)foo.GetMembers("Of").Single();

        Assert.That(factory.Parameters.Single().ExplicitDefaultValue, Is.EqualTo(outputIsUnchanged ? 2 : 1));
    }

    [Test]
    public void When_ConstructorBodyChanges_Then_TargetOutputUsesCachedOutput()
    {
        var initialSyntaxTree = ParseSyntaxTree(
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Foo
            {
                public Foo(int value = 42) { }
            }
            """
        );
        var updatedSyntaxTree = ParseSyntaxTree(
            """
            namespace Macaron.FactoryMethod.Tests;

            [AutoFactory]
            public partial class Foo
            {
                public Foo(int value = 42)
                {
                    _ = value;
                }
            }
            """
        );
        var initialCompilation = CreateCompilation(initialSyntaxTree);
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
            Is.EqualTo(new[] { IncrementalStepRunReason.Cached })
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
        var initialResult = GetGeneratorResult(driver);
        driver = RunGenerator(driver, updatedCompilation);

        var result = GetGeneratorResult(driver);
        var outputReasons = GetTrackedOutputReasons(result, "SourceOutput");

        NUnit.Framework.Assert.That(
            outputReasons,
            Is.EquivalentTo(new[]
            {
                IncrementalStepRunReason.New,
                IncrementalStepRunReason.Cached,
            })
        );
        var initialSources = initialResult.GeneratedSources.ToDictionary(source => source.HintName, source => source.SourceText.ToString());
        foreach (var source in result.GeneratedSources)
        {
            Assert.That(source.SourceText.ToString(), source.HintName.StartsWith("Bar_")
                ? Is.EqualTo(initialSources[source.HintName])
                : Is.Not.EqualTo(initialSources[source.HintName]));
        }
    }
}
