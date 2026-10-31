using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.FactoryMethod;

internal static class DiagnosticFactory
{
    private static readonly DiagnosticDescriptor InvalidFactoryMethodNameDescriptor = new(
        id: "MFM0001",
        title: "Invalid factory method name",
        messageFormat: "Factory method name '{0}' is not a valid C# identifier.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static Diagnostic InvalidFactoryMethodName(
        AttributeData attributeData,
        string methodName,
        int constructorArgumentIndex = 0
    )
    {
        var location = GetFactoryMethodNameLocation(attributeData, constructorArgumentIndex);

        return Diagnostic.Create(
            descriptor: InvalidFactoryMethodNameDescriptor,
            location,
            messageArgs: methodName
        );
    }

    private static Location GetFactoryMethodNameLocation(AttributeData attributeData, int constructorArgumentIndex)
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax
            {
                ArgumentList: { Arguments.Count: > 0 } argumentList,
            }
            || attributeData.AttributeConstructor is not { } attributeConstructor
        )
        {
            return Location.None;
        }

        var constructorParameters = attributeConstructor.Parameters;
        var assignedConstructorArguments = new bool[constructorParameters.Length];

        foreach (var argument in argumentList.Arguments)
        {
            int argumentIndex;

            if (argument.NameEquals is not null)
            {
                continue;
            }

            if (argument.NameColon is { Name.Identifier.ValueText: var parameterName })
            {
                argumentIndex = -1;

                for (var i = 0; i < constructorParameters.Length; i++)
                {
                    if (constructorParameters[i].Name == parameterName)
                    {
                        argumentIndex = i;

                        break;
                    }
                }

                if (argumentIndex < 0)
                {
                    continue;
                }
            }
            else
            {
                argumentIndex = Array.IndexOf(assignedConstructorArguments, false);

                if (argumentIndex < 0)
                {
                    continue;
                }
            }

            assignedConstructorArguments[argumentIndex] = true;

            if (argumentIndex == constructorArgumentIndex)
            {
                return argument.Expression.GetLocation();
            }
        }

        return Location.None;
    }
}
