using System.Diagnostics;

namespace Macaron.FactoryMethod
{
    [Conditional("SOURCE_GENERATOR_ONLY")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class AutoFactoryAttribute : Attribute
    {
        public bool IncludePrivateConstructors { get; set; }
    }
}
