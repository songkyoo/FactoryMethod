using System.Diagnostics;

namespace Macaron.FactoryMethod;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class AutoFactoryAttribute(
    string methodName = "Of",
    string parameterlessMethodName = "New"
) : Attribute
{
    public string MethodName { get; } = methodName;

    public string ParameterlessMethodName { get; } = parameterlessMethodName;

    public bool IncludePrivateConstructors { get; set; }
}
