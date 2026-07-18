using System.Diagnostics;

namespace Macaron.FactoryMethod;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class FactoryMethodNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
