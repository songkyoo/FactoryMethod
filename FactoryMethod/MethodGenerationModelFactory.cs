using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.Accessibility;

namespace Macaron.FactoryMethod;

public static class MethodGenerationModelFactory
{
    private const string DefaultMethodName = "Of";
    private const string DefaultParameterlessMethodName = "New";

    public static MethodGenerationModel Create(string typeString, IMethodSymbol methodSymbol)
    {
        var methodName = GetMethodName(methodSymbol);
        var accessModifier = methodSymbol.DeclaredAccessibility == Private
            ? "public"
            : methodSymbol.DeclaredAccessibility.ToString().ToLowerInvariant(); // public or internal
        var parameterGenerationModels = methodSymbol
            .Parameters
            .Select(ParameterGenerationModelFactory.Create)
            .ToImmutableArray();

        return new MethodGenerationModel(
            AccessModifier: accessModifier,
            Type: typeString,
            Name: methodName,
            Parameters: parameterGenerationModels
        );
    }

    private static string GetMethodName(IMethodSymbol methodSymbol)
    {
        var parameters = methodSymbol.Parameters;
        var attributeData = methodSymbol
            .GetAttributes()
            .FirstOrDefault(x => IsFactoryMethodNameAttribute(x.AttributeClass));

        if (TryGetGeneratedFactoryMethodName(attributeData, out var methodName))
        {
            return methodName;
        }

        return parameters.IsEmpty ? DefaultParameterlessMethodName : DefaultMethodName;
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
