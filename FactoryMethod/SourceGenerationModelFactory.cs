using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.Accessibility;
using static Microsoft.CodeAnalysis.SymbolDisplayTypeQualificationStyle;

namespace Macaron.FactoryMethod;


internal static class SourceGenerationModelFactory
{
    private const string IncludePrivateConstructorsPropertyName = "IncludePrivateConstructors";

    private static readonly SymbolDisplayFormat ReturnTypeDisplayFormat = new(
        typeQualificationStyle: NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
    );

    public static SourceGenerationModel? Create(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var includePrivateConstructors = GetIncludePrivateConstructors(context);
        var methodsBuilder = ImmutableArray.CreateBuilder<MethodGenerationModel>();
        var typeString = typeSymbol.ToDisplayString(ReturnTypeDisplayFormat);

        foreach (var methodSymbol in FilterTargetConstructors(typeSymbol, includePrivateConstructors))
        {
            cancellationToken.ThrowIfCancellationRequested();

            methodsBuilder.Add(MethodGenerationModelFactory.Create(typeString, methodSymbol));
        }

        if (methodsBuilder.Count == 0)
        {
            return null;
        }

        var typeSymbols = new List<INamedTypeSymbol> { typeSymbol };
        var parentType = typeSymbol.ContainingType;

        while (parentType is not null)
        {
            typeSymbols.Add(parentType);

            parentType = parentType.ContainingType;
        }

        var typeDeclarations = typeSymbols
            .AsEnumerable()
            .Reverse()
            .Select(GetPartialTypeDeclarationString)
            .ToImmutableArray();
        var @namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new SourceGenerationModel(
            HintName: GetHintName(typeSymbol),
            Namespace: @namespace,
            TypeDeclarations: typeDeclarations,
            Methods: methodsBuilder.ToImmutable()
        );
    }

    private static bool GetIncludePrivateConstructors(GeneratorAttributeSyntaxContext context)
    {
        foreach (var attributeData in context.Attributes)
        {
            foreach (var namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument is { Key: IncludePrivateConstructorsPropertyName, Value.Value: true })
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<IMethodSymbol> FilterTargetConstructors(
        INamedTypeSymbol typeSymbol,
        bool includePrivateConstructors
    )
    {
        return typeSymbol
            .InstanceConstructors
            .Where(methodSymbol =>
            {
                if (HasIgnoreAutoFactoryAttribute(methodSymbol))
                {
                    return false;
                }

                var declaredAccessibility = methodSymbol.DeclaredAccessibility;

                return declaredAccessibility is Public or Internal
                       || includePrivateConstructors && declaredAccessibility is Private;
            });
    }

    private static bool HasIgnoreAutoFactoryAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().Any(x => IsIgnoreAutoFactoryAttribute(x.AttributeClass));
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

    private static string GetPartialTypeDeclarationString(INamedTypeSymbol typeSymbol)
    {
        var typeKind = GetTypeKindString(typeSymbol);
        var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        return $"partial {typeKind} {typeName}";
    }

    private static string GetTypeKindString(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsRecord)
        {
            return typeSymbol.TypeKind is TypeKind.Struct ? "record struct" : "record" ;
        }

        return typeSymbol.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => throw new InvalidOperationException($"Invalid type kind: {typeSymbol.TypeKind}"),
        };
    }

    private static string GetHintName(INamedTypeSymbol typeSymbol)
    {
        var qualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        const uint fnvPrime = 16777619;
        const uint offsetBasis = 2166136261;

        var bytes = Encoding.UTF8.GetBytes($"{qualifiedName}");
        uint hash = offsetBasis;

        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return $"{typeSymbol.Name}_{typeSymbol.Arity}.{hash:x8}.g.cs";
    }
}
