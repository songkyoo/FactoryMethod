using System.Collections.Immutable;

namespace Macaron.FactoryMethod;

public sealed record MethodGenerationModel(
    string AccessModifier,
    string Type,
    string Name,
    ImmutableArray<ParameterGenerationModel> Parameters
)
{
    public bool Equals(MethodGenerationModel? other)
    {
        return ReferenceEquals(this, other)
            || other is not null
            && AccessModifier == other.AccessModifier
            && Type == other.Type
            && Name == other.Name
            && Parameters.SequenceEqual(other.Parameters);
    }

    public override int GetHashCode()
    {
        var stringComparer = StringComparer.Ordinal;

        unchecked
        {
            var hashCode = stringComparer.GetHashCode(AccessModifier);

            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(Type);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(Name);

            foreach (var parameter in Parameters)
            {
                hashCode = (hashCode * 397) ^ parameter.GetHashCode();
            }

            return hashCode;
        }
    }
}
