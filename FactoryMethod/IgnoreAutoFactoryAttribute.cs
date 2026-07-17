using System.Diagnostics;

namespace Macaron.FactoryMethod
{
    [Conditional("SOURCE_GENERATOR_ONLY")]
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    internal sealed class IgnoreAutoFactoryAttribute : Attribute;
}
