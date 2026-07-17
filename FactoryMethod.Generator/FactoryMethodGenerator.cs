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
        var modelProvider = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: AutoFactoryAttributeMetadataName,
                predicate: static (syntaxNode, _) => syntaxNode is TypeDeclarationSyntax,
                transform: static (attributeContext, cancellationToken) => SourceGenerationModelFactory.Create(
                    attributeContext,
                    cancellationToken
                )
            )
            .Where(static model => model != null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(
            source: modelProvider.Select(static (model, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return GenerateSource(model);
            }),
            action: static (sourceProductionContext, source) =>
            {
                sourceProductionContext.AddSource(
                    hintName: source.HintName,
                    sourceText: SourceText.From(source.Source, Encoding.UTF8)
                );
            }
        );
    }
    #endregion
}
