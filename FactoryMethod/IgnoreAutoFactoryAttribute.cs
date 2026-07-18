using System.Diagnostics;

namespace Macaron.FactoryMethod;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Constructor)]
internal sealed class IgnoreAutoFactoryAttribute : Attribute;
