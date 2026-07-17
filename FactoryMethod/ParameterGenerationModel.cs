namespace Macaron.FactoryMethod;

public sealed record ParameterGenerationModel(
    string Attribute,
    string Modifier,
    string Type,
    string Name,
    string DefaultValue
);
