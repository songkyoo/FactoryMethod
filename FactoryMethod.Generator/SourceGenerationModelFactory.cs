using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.Accessibility;
using static Microsoft.CodeAnalysis.SymbolDisplayTypeQualificationStyle;

namespace Macaron.FactoryMethod;

internal static class SourceGenerationModelFactory
{
    private const int MethodNameArgumentIndex = 0;
    private const int ParameterlessMethodNameArgumentIndex = 1;
    private const string IncludePrivateConstructorsPropertyName = "IncludePrivateConstructors";

    private static readonly SymbolDisplayFormat ReturnTypeDisplayFormat = new(
        typeQualificationStyle: NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
    );

    public static (SourceGenerationModel?, ImmutableArray<Diagnostic>) Create(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.Attributes.Length != 1
            || context.TargetSymbol is not INamedTypeSymbol typeSymbol
        )
        {
            return (null, ImmutableArray<Diagnostic>.Empty);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var autoFactoryAttribute = context.Attributes[0];
        var methodName = GetMethodName(autoFactoryAttribute, MethodNameArgumentIndex);
        var parameterlessMethodName = GetMethodName(autoFactoryAttribute, ParameterlessMethodNameArgumentIndex);

        var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

        ValidateMethodName(
            autoFactoryAttribute,
            specifiedName: methodName,
            argumentIndex: MethodNameArgumentIndex,
            diagnosticsBuilder,
            out methodName
        );
        ValidateMethodName(
            autoFactoryAttribute,
            specifiedName: parameterlessMethodName,
            argumentIndex: ParameterlessMethodNameArgumentIndex,
            diagnosticsBuilder,
            out parameterlessMethodName
        );

        // AutoFactory 어트리뷰트가 유효하지 않다면 더 진행할 수 없음
        if (diagnosticsBuilder.Count > 0)
        {
            return (null, diagnosticsBuilder.MoveOrCopyToImmutable());
        }

        var includePrivateConstructors = GetIncludePrivateConstructors(autoFactoryAttribute);
        var constructors = typeSymbol.InstanceConstructors;
        var methodsBuilder = ImmutableArray.CreateBuilder<MethodGenerationModel>(initialCapacity: constructors.Length);

        foreach (var methodSymbol in constructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var declaredAccessibility = methodSymbol.DeclaredAccessibility;

            if (declaredAccessibility is not (Public or Internal)
                && !(includePrivateConstructors && declaredAccessibility is Private)
            )
            {
                continue;
            }

            var result = MethodGenerationModelFactory.Create(
                methodSymbol,
                defaultMethodName: methodName,
                defaultParameterlessMethodName: parameterlessMethodName
            );

            switch (result)
            {
                case null: continue;
                case { HasValue: true, Value: var methodModel }: methodsBuilder.Add(methodModel); break;
                case { HasError: true, Error: var diagnostic }: diagnosticsBuilder.Add(diagnostic); break;
                default: throw new InvalidOperationException($"Unexpected result: {result}");
            }
        }

        if (methodsBuilder.Count == 0)
        {
            return (null, diagnosticsBuilder.MoveOrCopyToImmutable());
        }

        var typeSymbols = new List<INamedTypeSymbol> { typeSymbol };
        var parentType = typeSymbol.ContainingType;

        while (parentType is not null)
        {
            typeSymbols.Add(parentType);

            parentType = parentType.ContainingType;
        }

        var typeDeclarations = ImmutableArray.CreateBuilder<string>(typeSymbols.Count);

        for (var i = typeSymbols.Count - 1; i >= 0; i--)
        {
            typeDeclarations.Add(GetPartialTypeDeclarationString(typeSymbols[i]));
        }

        var @namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var model =  new SourceGenerationModel(
            HintName: GetHintName(typeSymbol),
            Namespace: @namespace,
            TypeName: typeSymbol.ToDisplayString(ReturnTypeDisplayFormat),
            TypeDeclarations: typeDeclarations.MoveOrCopyToImmutable(),
            Methods: methodsBuilder.MoveOrCopyToImmutable()
        );

        return (model, diagnosticsBuilder.MoveOrCopyToImmutable());
    }

    private static string GetMethodName(AttributeData autoFactoryAttribute, int argumentIndex)
    {
        return autoFactoryAttribute.ConstructorArguments[argumentIndex].Value as string ?? "";
    }

    private static void ValidateMethodName(
        AttributeData autoFactoryAttribute,
        string specifiedName,
        int argumentIndex,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder,
        out string methodName
    )
    {
        specifiedName = specifiedName.Trim();

        if (MethodGenerationModelFactory.TryEscapeIdentifier(specifiedName, out methodName))
        {
            return;
        }

        diagnosticsBuilder.Add(DiagnosticFactory.InvalidFactoryMethodName(
            autoFactoryAttribute,
            specifiedName,
            argumentIndex
        ));
    }

    private static bool GetIncludePrivateConstructors(AttributeData autoFactoryAttribute)
    {
        foreach (var namedArgument in autoFactoryAttribute.NamedArguments)
        {
            if (namedArgument is { Key: IncludePrivateConstructorsPropertyName, Value.Value: true })
            {
                return true;
            }
        }

        return false;
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

        var bytes = Encoding.UTF8.GetBytes(qualifiedName);
        var hash = offsetBasis;

        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return $"{typeSymbol.Name}_{typeSymbol.Arity}.{hash:x8}.g.cs";
    }
}

file static class FileScopeExtensions
{
    public static ImmutableArray<T> MoveOrCopyToImmutable<T>(this ImmutableArray<T>.Builder builder)
    {
        return builder.Count == builder.Capacity
            ? builder.MoveToImmutable()
            : builder.ToImmutable();
    }
}
