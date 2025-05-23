﻿using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Macaron.FactoryMethod;

internal static class Helpers
{
    private const string AutoFactoryAttributeDisplayString =
        "Macaron.FactoryMethod.AutoFactoryAttribute";

    private const string IgnoreAutoFactoryAttributeDisplayString =
        "Macaron.FactoryMethod.IgnoreAutoFactoryAttribute";

    public static bool IsAutoFactoryAttribute(INamedTypeSymbol? symbol)
    {
        return symbol?.ToDisplayString() == AutoFactoryAttributeDisplayString;
    }

    public static bool IsIgnoreAutoFactoryAttribute(INamedTypeSymbol? symbol)
    {
        return symbol?.ToDisplayString() == IgnoreAutoFactoryAttributeDisplayString;
    }

    public static bool HasAutoFactoryAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().Any(attributeData =>
        {
            return IsAutoFactoryAttribute(attributeData.AttributeClass);
        });
    }

    public static bool HasIgnoreAutoFactoryAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().Any(attributeData =>
        {
            return IsIgnoreAutoFactoryAttribute(attributeData.AttributeClass);
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
            .FirstOrDefault(attributeData => IsAutoFactoryAttribute(attributeData.AttributeClass));
        return (
            hasAttribute: attributeData != null,
            methodName: TryGetGeneratedFactoryMethodName(attributeData, out var methodName) ? methodName : "Of"
        );
    }

    public static string GetParameterString(IParameterSymbol parameterSymbol)
    {
        var attributesString = GetParameterAttributesString(parameterSymbol);
        var modifiersString = GetParameterModifierString(parameterSymbol);
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

    private static string GetParameterModifierString(IParameterSymbol parameterSymbol)
    {
        return parameterSymbol switch
        {
            { RefKind: RefKind.Ref } => "ref ",
            { RefKind: RefKind.Out } => "out ",
            { RefKind: RefKind.In } => "in ",
            { IsParams: true } => "params ",
            _ => "",
        };
    }

    private static string GetParameterDefaultValueString(IParameterSymbol parameterSymbol)
    {
        if (!parameterSymbol.HasExplicitDefaultValue)
        {
            return "";
        }

        if (parameterSymbol.Type.TypeKind == TypeKind.Enum)
        {
            var enumType = parameterSymbol.Type;
            var fullyQualifiedEnumName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var defaultValue = parameterSymbol.ExplicitDefaultValue;

            foreach (var fieldSymbol in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (fieldSymbol.HasConstantValue && fieldSymbol.ConstantValue.Equals(defaultValue))
                {
                    return $" = {fullyQualifiedEnumName}.{fieldSymbol.Name}";
                }
            }

            return $" = ({fullyQualifiedEnumName})({defaultValue})";
        }

        var syntaxReference = parameterSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference?.GetSyntax() is ParameterSyntax { Default.Value: { } literal })
        {
            return $" = {literal.ToFullString().Trim()}";
        }
        else
        {
            var workspace = new AdhocWorkspace();
            var generator = SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp);
            var syntaxNode = generator.LiteralExpression(parameterSymbol.ExplicitDefaultValue);
            var code = syntaxNode.ToFullString();

            return $" = {code}";
        }
    }
}
