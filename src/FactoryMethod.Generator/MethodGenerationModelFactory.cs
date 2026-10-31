using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Microsoft.CodeAnalysis.Accessibility;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Macaron.FactoryMethod;

internal static class MethodGenerationModelFactory
{
    public static Result<MethodGenerationModel, Diagnostic>? Create(
        IMethodSymbol methodSymbol,
        string defaultMethodName,
        string defaultParameterlessMethodName
    )
    {
        AttributeData? factoryMethodNameAttribute = null;

        foreach (var attributeData in methodSymbol.GetAttributes())
        {
            if (IsIgnoreAutoFactoryAttribute(attributeData.AttributeClass))
            {
                return null;
            }

            if (!IsFactoryMethodNameAttribute(attributeData.AttributeClass))
            {
                continue;
            }

            if (factoryMethodNameAttribute is not null)
            {
                return null;
            }

            factoryMethodNameAttribute = attributeData;
        }

        string? methodName = null;

        if (TryGetGeneratedFactoryMethodName(factoryMethodNameAttribute, out var specifiedMethodName))
        {
            if (!TryEscapeIdentifier(specifiedMethodName, out methodName))
            {
                return DiagnosticFactory.InvalidFactoryMethodName(
                    factoryMethodNameAttribute!,
                    specifiedMethodName
                );
            }
        }

        methodName ??= methodSymbol.Parameters.IsEmpty
            ? defaultParameterlessMethodName
            : defaultMethodName;

        var accessModifier = methodSymbol.DeclaredAccessibility switch
        {
            Internal => "internal",
            Public or Private => "public",
            _ => throw new InvalidOperationException($"Invalid constructor accessibility: {methodSymbol.DeclaredAccessibility}"),
        };
        var parameters = methodSymbol.Parameters;
        var parameterGenerationModels = ImmutableArray.CreateBuilder<ParameterGenerationModel>(
            initialCapacity: parameters.Length
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

    internal static bool TryEscapeIdentifier(string name, out string escapedName)
    {
        if (string.IsNullOrEmpty(name))
        {
            escapedName = "";

            return false;
        }

        if (name[0] == '@')
        {
            escapedName = name;

            return SyntaxFacts.IsValidIdentifier(name[1..]);
        }

        if (!SyntaxFacts.IsValidIdentifier(name))
        {
            escapedName = "";

            return false;
        }

        escapedName =
            SyntaxFacts.GetKeywordKind(name) != None
            || SyntaxFacts.GetContextualKeywordKind(name) != None
            ? $"@{name}"
            : name;

        return true;
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
