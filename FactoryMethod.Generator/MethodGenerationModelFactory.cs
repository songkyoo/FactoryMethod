using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.Accessibility;

namespace Macaron.FactoryMethod;

internal static class MethodGenerationModelFactory
{
    private const string DefaultMethodName = "Of";
    private const string DefaultParameterlessMethodName = "New";

    public static MethodGenerationModel? Create(IMethodSymbol methodSymbol)
    {
        string? methodName = null;

        foreach (var attributeData in methodSymbol.GetAttributes())
        {
            if (IsIgnoreAutoFactoryAttribute(attributeData.AttributeClass))
            {
                return null;
            }

            if (IsFactoryMethodNameAttribute(attributeData.AttributeClass)
                && TryGetGeneratedFactoryMethodName(attributeData, out var specifiedMethodName)
            )
            {
                methodName = specifiedMethodName;
            }
        }

        methodName ??= methodSymbol.Parameters.IsEmpty
            ? DefaultParameterlessMethodName
            : DefaultMethodName;

        var accessModifier = methodSymbol.DeclaredAccessibility switch
        {
            Internal => "internal",
            Public or Private => "public",
            _ => throw new InvalidOperationException(
                $"Invalid constructor accessibility: {methodSymbol.DeclaredAccessibility}"
            ),
        };
        var parameters = methodSymbol.Parameters;
        var parameterGenerationModels = ImmutableArray.CreateBuilder<ParameterGenerationModel>(
            parameters.Length
        );

        foreach (var parameterSymbol in parameters)
        {
            parameterGenerationModels.Add(ParameterGenerationModelFactory.Create(parameterSymbol));
        }

        return new MethodGenerationModel(
            AccessModifier: accessModifier,
            Name: methodName,
            Parameters: parameterGenerationModels.MoveToImmutable()
        );
    }

    private static bool IsFactoryMethodNameAttribute(INamedTypeSymbol? symbol)
    {
        return symbol is
        {
            Name: "FactoryMethodNameAttribute",
            ContainingNamespace:
            {
                Name: "FactoryMethod",
                ContainingNamespace:
                {
                    Name: "Macaron",
                    ContainingNamespace:
                    {
                        IsGlobalNamespace: true,
                    },
                },
            },
        };
    }

    private static bool IsIgnoreAutoFactoryAttribute(INamedTypeSymbol? symbol)
    {
        return symbol is
        {
            Name: "IgnoreAutoFactoryAttribute",
            ContainingNamespace:
            {
                Name: "FactoryMethod",
                ContainingNamespace:
                {
                    Name: "Macaron",
                    ContainingNamespace:
                    {
                        IsGlobalNamespace: true,
                    },
                },
            },
        };
    }

    private static bool TryGetGeneratedFactoryMethodName(AttributeData? attributeData, out string methodName)
    {
        if (attributeData?.ConstructorArguments is [{ Value: string methodName2 }])
        {
            methodName = methodName2.Trim();

            return !string.IsNullOrEmpty(methodName);
        }
        else
        {
            methodName = "";

            return false;
        }
    }
}
