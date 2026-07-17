using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using static Microsoft.CodeAnalysis.RefKind;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.FactoryMethod;

internal static class ParameterGenerationModelFactory
{
    private static readonly SymbolDisplayFormat ParameterTypeDisplayFormat = FullyQualifiedFormat
        .WithMiscellaneousOptions(
            IncludeNullableReferenceTypeModifier
            | UseSpecialTypes
        );

    public static ParameterGenerationModel Create(IParameterSymbol parameterSymbol)
    {
        var attributesString = GetParameterAttributesString(parameterSymbol);
        var modifiersString = GetParameterModifierString(parameterSymbol);
        var typeString = parameterSymbol.Type.ToDisplayString(ParameterTypeDisplayFormat);
        var nameString = GetParameterName(parameterSymbol);
        var defaultValueString = GetParameterDefaultValueString(parameterSymbol);

        return new ParameterGenerationModel(
            Attribute: attributesString,
            Modifier: modifiersString,
            Type: typeString,
            Name: nameString,
            DefaultValue: defaultValueString
        );
    }

    private static string GetParameterName(IParameterSymbol parameterSymbol)
    {
        return EscapeIdentifier(GetCamelCaseName(parameterSymbol.Name));
    }

    private static string EscapeIdentifier(string name)
    {
        return
            SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            ? $"@{name}"
            : name;
    }

    private static string GetCamelCaseName(string name)
    {
        if (name.Length == 0 || !char.IsLetter(name[0]))
        {
            return name;
        }

        var firstCharacter = char.ToLowerInvariant(name[0]);

        return firstCharacter == name[0]
            ? name
            : firstCharacter + (name.Length > 1 ? name[1..] : "");
    }

    private static string GetParameterAttributesString(IParameterSymbol parameterSymbol)
    {
        var attributes = parameterSymbol.GetAttributes();

        if (attributes.IsEmpty)
        {
            return "";
        }

        var attributesBuilder = new StringBuilder();

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            var attributeBuilder = new StringBuilder(
                $"[{attribute.AttributeClass.ToDisplayString(FullyQualifiedFormat)}"
            );

            if (attribute.ConstructorArguments.Length > 0 || attribute.NamedArguments.Length > 0)
            {
                attributeBuilder.Append("(");

                var constructorArguments = attribute
                    .ConstructorArguments
                    .Select(FormatAttributeArgument);
                var namedArguments = attribute
                    .NamedArguments
                    .Select(pair => $"{pair.Key} = {FormatAttributeArgument(pair.Value)}");

                attributeBuilder.Append(string.Join(", ", constructorArguments.Concat(namedArguments)));
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
            { RefKind: Ref } => "ref ",
            { RefKind: Out } => "out ",
            { RefKind: In } => "in ",
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
            var fullyQualifiedEnumName = enumType.ToDisplayString(FullyQualifiedFormat);

            foreach (var memberSymbol in enumType.GetMembers())
            {
                if (memberSymbol is IFieldSymbol { IsStatic: true, HasConstantValue: true } fieldSymbol
                    && fieldSymbol.ConstantValue.Equals(defaultValue)
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
