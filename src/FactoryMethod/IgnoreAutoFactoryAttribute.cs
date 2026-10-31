using System.Diagnostics;

namespace Macaron.FactoryMethod;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class IgnoreAutoFactoryAttribute : Attribute;
