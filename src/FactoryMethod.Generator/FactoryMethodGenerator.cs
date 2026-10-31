using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Macaron.FactoryMethod.SourceGenerationHelper;

namespace Macaron.FactoryMethod;

[Generator]
public sealed class FactoryMethodGenerator : IIncrementalGenerator
{
    #region Constants
    private const string AutoFactoryAttributeMetadataName = "Macaron.FactoryMethod.AutoFactoryAttribute";
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generationModelAndDiagnosticsProvider = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: AutoFactoryAttributeMetadataName,
                predicate: static (syntaxNode, _) => syntaxNode is TypeDeclarationSyntax,
                transform: SourceGenerationModelFactory.Create
            );

        context.RegisterSourceOutput(
            source: generationModelAndDiagnosticsProvider
                .Where(static x => x.Item2.Length > 0)
                .SelectMany(static (x, _) => x.Item2),
            action: static (sourceProductionContext, diagnostic) =>
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }
        );
        context.RegisterSourceOutput(
            source: generationModelAndDiagnosticsProvider
                .Where(static x => x.Item1 != null)
                .Select(static (x, _) => x.Item1!)
                .Select(static (model, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return GenerateSource(model);
                }),
            action: static (sourceProductionContext, source) =>
            {
                sourceProductionContext.AddSource(
                    hintName: source.HintName,
                    sourceText: SourceText.From(text: source.Source, encoding: Encoding.UTF8)
                );
            }
        );
    }
    #endregion
}
