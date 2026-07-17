using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Macaron.FactoryMethod.Tests;

internal static class Helper
{
    private static readonly string[] AttributeHintNames =
    [
        "AutoFactoryAttribute.g.cs",
        "FactoryMethodNameAttribute.g.cs",
        "IgnoreAutoFactoryAttribute.g.cs",
    ];

    public static void AssertGeneratedCode(string sourceCode, string expected)
    {
        var compilation = CreateCompilation(sourceCode);
        var driver = CreateGeneratorDriver();
        driver = RunGenerator(driver, compilation);

        var result = GetGeneratorResult(driver);
        var generatedSources = GetFactoryGeneratedSources(result);
        var actual = generatedSources.Length > 0
            ? generatedSources.Single().SourceText.ToString()
            : "";

        Assert.That(actual.ReplaceLineEndings(), Is.EqualTo(expected.ReplaceLineEndings()));
    }

    public static CSharpSyntaxTree ParseSyntaxTree(string sourceCode)
    {
        return (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(SourceText.From(sourceCode, Encoding.UTF8));
    }

    public static CSharpCompilation CreateCompilation(params SyntaxTree[] syntaxTrees)
    {
        var dataAnnotationsAssembly = typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly;

        var references = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Append(dataAnnotationsAssembly)
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>();

        return CSharpCompilation.Create(
            assemblyName: "Macaron.FactoryMethod.Tests",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }

    public static GeneratorDriver CreateGeneratorDriver(bool trackIncrementalSteps = false)
    {
        return CSharpGeneratorDriver.Create(
            generators: [new FactoryMethodGenerator().AsSourceGenerator()],
            additionalTexts: [],
            parseOptions: CSharpParseOptions.Default,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalSteps
            )
        );
    }

    public static GeneratorDriver RunGenerator(GeneratorDriver driver, Compilation compilation)
    {
        return driver.RunGenerators(compilation);
    }

    public static GeneratorRunResult GetGeneratorResult(GeneratorDriver driver)
    {
        return driver.GetRunResult().Results.Single();
    }

    public static IncrementalStepRunReason[] GetTrackedOutputReasons(
        GeneratorRunResult result,
        string stepName
    )
    {
        return result
            .TrackedOutputSteps[stepName]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Item2)
            .ToArray();
    }

    private static CSharpCompilation CreateCompilation(string sourceCode)
    {
        return CreateCompilation(ParseSyntaxTree(sourceCode));
    }

    private static GeneratedSourceResult[] GetFactoryGeneratedSources(GeneratorRunResult result)
    {
        return result
            .GeneratedSources
            .Where(source => !AttributeHintNames.Contains(source.HintName))
            .ToArray();
    }
}
