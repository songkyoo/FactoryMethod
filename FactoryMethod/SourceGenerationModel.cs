using System.Collections.Immutable;

namespace Macaron.FactoryMethod;

internal sealed record SourceGenerationModel(
    string HintName,
    string Namespace,
    ImmutableArray<string> TypeDeclarations,
    ImmutableArray<MethodGenerationModel> Methods
)
{
    public bool Equals(SourceGenerationModel? other)
    {
        return ReferenceEquals(this, other)
            || other is not null
            && HintName == other.HintName
            && Namespace == other.Namespace
            && TypeDeclarations.SequenceEqual(other.TypeDeclarations, StringComparer.Ordinal)
            && Methods.SequenceEqual(other.Methods);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(HintName);

            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Namespace);

            foreach (var typeDeclaration in TypeDeclarations)
            {
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(typeDeclaration);
            }

            foreach (var method in Methods)
            {
                hashCode = (hashCode * 397) ^ method.GetHashCode();
            }

            return hashCode;
        }
    }
}
