using System.Text;
using Microsoft.CodeAnalysis;

namespace Macaron.FactoryMethod;

internal static class Helpers
{
    private const string GenerateFactoryMethodAttributeDisplayString =
        "Macaron.FactoryMethod.GenerateFactoryMethodAttribute";
    private const string IgnoreFactoryMethodAttributeDisplayString =
        "Macaron.FactoryMethod.IgnoreFactoryMethodAttribute";

    public static bool IsGenerateFactoryMethodAttribute(INamedTypeSymbol? symbol)
    {
        return symbol?.ToDisplayString() == GenerateFactoryMethodAttributeDisplayString;
    }

    public static bool IsIgnoreFactoryMethodAttribute(INamedTypeSymbol? symbol)
    {
        return symbol?.ToDisplayString() == IgnoreFactoryMethodAttributeDisplayString;
    }

    public static bool HasGenerateFactoryMethodAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().Any(attributeData =>
        {
            return IsGenerateFactoryMethodAttribute(attributeData.AttributeClass);
        });
    }

    public static bool HasIgnoreFactoryMethodAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().Any(attributeData =>
        {
            return IsIgnoreFactoryMethodAttribute(attributeData.AttributeClass);
        });
    }

    public static bool TryGetGeneratedFactoryMethodName(AttributeData? attributeData, out string methodName)
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

    public static (bool hasAttribute, string methodName) GetTypeContext(INamedTypeSymbol? typeSymbol)
    {
        var attributeData = typeSymbol?
            .GetAttributes()
            .FirstOrDefault(attributeData => IsGenerateFactoryMethodAttribute(attributeData.AttributeClass));
        return (
            hasAttribute: attributeData != null,
            methodName: TryGetGeneratedFactoryMethodName(attributeData, out var methodName) ? methodName : "Of"
        );
    }

    public static string GetParameterString(IParameterSymbol parameterSymbol)
    {
        var attributesString = GetParameterAttributesString(parameterSymbol);
        var modifiersString = GetParameterModifiersString(parameterSymbol);
        var typeString = parameterSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var nullabilityString = GetNullableAnnotationString(parameterSymbol, typeString);
        var nameString = GetCamelCaseName(parameterSymbol.Name);
        var defaultValueString = GetParameterDefaultValueString(parameterSymbol);

        return $"{attributesString}{modifiersString}{typeString}{nullabilityString} {nameString}{defaultValueString}";

        #region Local Functions
        static string GetNullableAnnotationString(IParameterSymbol parameterSymbol, string typeString) =>
            parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated && !typeString.EndsWith("?")
                ? "?"
                : "";
        #endregion
    }

    public static string GetArgumentString(IParameterSymbol parameterSymbol)
    {
        var prefix = parameterSymbol.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => ""
        };

        return $"{prefix}{GetCamelCaseName(parameterSymbol.Name)}";
    }

    private static string GetCamelCaseName(string name)
    {
        return name.Length > 0 && char.IsLetter(name[0])
            ? char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name[1..] : "")
            : name;
    }

    private static string GetParameterAttributesString(IParameterSymbol parameterSymbol)
    {
        if (!parameterSymbol.GetAttributes().Any())
        {
            return "";
        }

        var attributesBuilder = new StringBuilder();
        foreach (var attribute in parameterSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            var attributeBuilder = new StringBuilder(
                $"[{attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}"
            );

            if (attribute.ConstructorArguments.Length > 0 || attribute.NamedArguments.Length > 0)
            {
                attributeBuilder.Append("(");

                var ctorArgs = attribute.ConstructorArguments
                    .Select(FormatAttributeArgument)
                    .ToList();

                var namedArgs = attribute.NamedArguments
                    .Select(pair => $"{pair.Key} = {FormatAttributeArgument(pair.Value)}")
                    .ToList();

                attributeBuilder.Append(string.Join(", ", ctorArgs.Concat(namedArgs)));
                attributeBuilder.Append(")");
            }

            attributeBuilder.Append("] ");
            attributesBuilder.Append(attributeBuilder);
        }

        return attributesBuilder.ToString();
    }

    private static string FormatAttributeArgument(TypedConstant typedConstant)
    {
        return typedConstant switch
        {
            { Kind: TypedConstantKind.Array } => $"new[] {{{string.Join(", ", typedConstant.Values.Select(FormatAttributeArgument))}}}",
            { Kind: TypedConstantKind.Type } => $"typeof({typedConstant.Value})",
            { Kind: TypedConstantKind.Enum } => typedConstant.Value?.ToString() ?? "null",
            { Value: string value } => $"\"{value.Replace("\"", "\\\"")}\"",
            { Value: null } => "null",
            _ => typedConstant.Value.ToString(),
        };
    }

    private static string GetParameterModifiersString(IParameterSymbol parameterSymbol)
    {
        var modifiers = new List<string>();

        switch (parameterSymbol.RefKind)
        {
            case RefKind.Ref:
                modifiers.Add("ref");
                break;
            case RefKind.Out:
                modifiers.Add("out");
                break;
            case RefKind.In:
                modifiers.Add("in");
                break;
        }

        if (parameterSymbol.IsParams)
        {
            modifiers.Add("params");
        }

        return modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
    }

    private static string GetParameterDefaultValueString(IParameterSymbol parameterSymbol)
    {
        if (!parameterSymbol.HasExplicitDefaultValue)
        {
            return "";
        }

        var defaultValue = parameterSymbol.ExplicitDefaultValue;
        switch (defaultValue)
        {
            case null:
                return " = null";
            case string strValue:
                return $" = \"{strValue.Replace("\"", "\\\"")}\"";
            case bool boolValue:
                return $" = {boolValue.ToString().ToLowerInvariant()}";
            default:
            {
                if (parameterSymbol.Type.TypeKind == TypeKind.Enum)
                {
                    var enumType = parameterSymbol.Type;
                    var fullyQualifiedEnumName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    foreach (var fieldSymbol in enumType.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (fieldSymbol.HasConstantValue && fieldSymbol.ConstantValue.Equals(defaultValue))
                        {
                            return $" = {fullyQualifiedEnumName}.{fieldSymbol.Name}";
                        }
                    }

                    return $" = {fullyQualifiedEnumName}.{defaultValue}";
                }
                else
                {
                    return $" = {defaultValue}";
                }
            }
        }
    }
}
