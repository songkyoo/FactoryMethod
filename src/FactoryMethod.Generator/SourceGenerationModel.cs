using System.Collections.Immutable;

namespace Macaron.FactoryMethod;

internal sealed record SourceGenerationModel(
    string HintName,
    string Namespace,
    string TypeName,
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
            && TypeName == other.TypeName
            && TypeDeclarations.SequenceEqual(other.TypeDeclarations, StringComparer.Ordinal)
            && Methods.SequenceEqual(other.Methods);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var stringComparer = StringComparer.Ordinal;

            var hashCode = stringComparer.GetHashCode(HintName);

            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(Namespace);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(TypeName);

            foreach (var typeDeclaration in TypeDeclarations)
            {
                hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(typeDeclaration);
            }

            foreach (var method in Methods)
            {
                hashCode = (hashCode * 397) ^ method.GetHashCode();
            }

            return hashCode;
        }
    }
}
