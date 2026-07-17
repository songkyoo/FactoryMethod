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

    public static Diagnostic CreateInvalidFactoryMethodName(
        AttributeData attributeData,
        string methodName
    )
    {
        var location = GetFactoryMethodNameLocation(attributeData);

        return Diagnostic.Create(
            descriptor: InvalidFactoryMethodNameDescriptor,
            location,
            messageArgs: methodName
        );
    }

    private static Location GetFactoryMethodNameLocation(AttributeData attributeData)
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax
            {
                ArgumentList: { Arguments.Count: > 0 } argumentList,
            }
        )
        {
            return argumentList.Arguments[0].Expression.GetLocation();
        }

        return Location.None;
    }
}
