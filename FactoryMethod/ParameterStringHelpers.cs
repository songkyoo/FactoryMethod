using System.Text;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Macaron.FactoryMethod;

internal static class ParameterStringHelpers
{
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

        var defaultValue = parameterSymbol.ExplicitDefaultValue;

        if (defaultValue == null)
        {
            var parameterType = parameterSymbol.Type;
            return !parameterType.IsValueType || parameterType.NullableAnnotation == NullableAnnotation.Annotated
                ? " = null"
                : " = default";
        }

        if (parameterSymbol.Type.TypeKind == TypeKind.Enum)
        {
            var enumType = parameterSymbol.Type;
            var fullyQualifiedEnumName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            foreach (var fieldSymbol in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (fieldSymbol is { IsStatic: true, HasConstantValue: true } &&
                    fieldSymbol.ConstantValue.Equals(defaultValue)
                )
                {
                    return $" = {fullyQualifiedEnumName}.{fieldSymbol.Name}";
                }
            }

            return $" = ({fullyQualifiedEnumName})({defaultValue})";
        }

        var literalExpression = defaultValue switch
        {
            string value => LiteralExpression(StringLiteralExpression, Literal(value)),
            char value => LiteralExpression(CharacterLiteralExpression, Literal(value)),
            bool value => LiteralExpression(value ? TrueLiteralExpression : FalseLiteralExpression),
            byte value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            sbyte value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            short value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            ushort value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            int value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            uint value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            long value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            ulong value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            float value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            double value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            decimal value => LiteralExpression(NumericLiteralExpression, Literal(value)),
            _ => null,
        };

        return $" = {literalExpression?.ToFullString() ?? "default"}";
    }
}
