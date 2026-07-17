using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Macaron.FactoryMethod.Tests;

internal static class Helper
{
    public static void Assert(string sourceCode, string expected)
    {
        var dataAnnotationsAssembly = typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly;

        var references = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Append(dataAnnotationsAssembly)
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>();

        var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(sourceCode, Encoding.UTF8));
        var compilation = CSharpCompilation.Create(
            assemblyName: "Macaron.FactoryMethod.Tests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var generator = new FactoryMethodGenerator();
        var driver = CSharpGeneratorDriver
            .Create(generator)
            .RunGenerators(compilation);

        var result = driver.GetRunResult().Results.Single();
        var generatedSources = result.GeneratedSources;

        // 0, 1, 2번은 AutoFactoryAttribute, FactoryMethodAttribute, IgnoreAutoFactoryAttribute
        var actual = generatedSources.Length > 3 ? generatedSources[3].SyntaxTree.ToString() : "";

        NUnit.Framework.Assert.That(actual.ReplaceLineEndings(), Is.EqualTo(expected.ReplaceLineEndings()));
    }
}
