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
            FullyQualifiedFormat.MiscellaneousOptions
            | IncludeNullableReferenceTypeModifier
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
                    .Select(pair => $"{EscapeIdentifier(pair.Key)} = {FormatAttributeArgument(pair.Value)}");

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
        if (typedConstant.IsNull)
        {
            return typedConstant.Type is { SpecialType: not SpecialType.System_Object } type
                ? $"({type.ToDisplayString(FullyQualifiedFormat)})null"
                : "null";
        }

        return typedConstant switch
        {
            { Kind: TypedConstantKind.Array } => $"new {typedConstant.Type!.ToDisplayString(FullyQualifiedFormat)} {{{string.Join(", ", typedConstant.Values.Select(FormatAttributeArgument))}}}",
            { Kind: TypedConstantKind.Type, Value: ITypeSymbol type } => $"typeof({type.ToDisplayString(FullyQualifiedFormat)})",
            { Kind: TypedConstantKind.Enum } => FormatEnumConstant(typedConstant.Type!, typedConstant.Value!),
            { Value: byte or sbyte or short or ushort } => $"({typedConstant.Type!.ToDisplayString(FullyQualifiedFormat)}){FormatPrimitiveConstant(typedConstant.Value)}",
            _ => FormatPrimitiveConstant(typedConstant.Value!),
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
            return parameterType.IsReferenceType || GetNullableUnderlyingType(parameterType) is not null
                ? " = null"
                : " = default";
        }

        var valueType = GetNullableUnderlyingType(parameterSymbol.Type) ?? parameterSymbol.Type;
        return " = " + (valueType.TypeKind == TypeKind.Enum
            ? FormatEnumConstant(valueType, defaultValue)
            : FormatPrimitiveConstant(defaultValue));
    }

    private static ITypeSymbol? GetNullableUnderlyingType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType
            ? namedType.TypeArguments[0]
            : null;
    }

    private static string FormatEnumConstant(ITypeSymbol enumType, object value)
    {
        var fullyQualifiedEnumName = enumType.ToDisplayString(FullyQualifiedFormat);

        // 동일한 값을 가진 값이 여러개 있는 경우 순서를 유지한다.
        foreach (var memberSymbol in enumType.GetMembers())
        {
            if (memberSymbol is IFieldSymbol
                {
                    IsStatic: true,
                    HasConstantValue: true,
                    DeclaredAccessibility: Accessibility.Public,
                } fieldSymbol
                && value.Equals(fieldSymbol.ConstantValue)
            )
            {
                return $"{fullyQualifiedEnumName}.{EscapeIdentifier(fieldSymbol.Name)}";
            }
        }

        return $"({fullyQualifiedEnumName})({FormatPrimitiveConstant(value)})";
    }

    private static string FormatPrimitiveConstant(object constantValue)
    {
        var specialFloatingPointConstant = constantValue switch
        {
            float number when float.IsNaN(number) => "float.NaN",
            float number when float.IsPositiveInfinity(number) => "float.PositiveInfinity",
            float number when float.IsNegativeInfinity(number) => "float.NegativeInfinity",
            double number when double.IsNaN(number) => "double.NaN",
            double number when double.IsPositiveInfinity(number) => "double.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) => "double.NegativeInfinity",
            _ => null,
        };

        if (specialFloatingPointConstant is not null)
        {
            return specialFloatingPointConstant;
        }

        var literalExpression = constantValue switch
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

        // 로슬린은 D를 생략하기 때문에 정수형 double은 int로 변환될 수 있고, -0은 부호를 잃을 수 있다.
        return literalExpression is null
            ? "default"
            : literalExpression.ToFullString() + (constantValue is double ? "D" : "");
    }
}
