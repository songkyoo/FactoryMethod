using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Macaron.FactoryMethod.Tests.Helper;

namespace Macaron.FactoryMethod.Tests;

[TestFixture]
public class ConstantExpressionTests
{
    [TestCase("Mode?", "Mode.Ready", "global::Mode.Ready", TestName = "NullableEnumDefaultCompiles")]
    [TestCase("Mode", "Mode.@default", "global::Mode.@default", TestName = "KeywordEnumMemberCompiles")]
    [TestCase("float", "float.NaN", "float.NaN", TestName = "FloatNaNDefaultCompiles")]
    [TestCase("float", "float.PositiveInfinity", "float.PositiveInfinity", TestName = "FloatPositiveInfinityDefaultCompiles")]
    [TestCase("double", "double.NegativeInfinity", "double.NegativeInfinity", TestName = "DoubleNegativeInfinityDefaultCompiles")]
    public void When_DefaultWasInvalid_Then_GeneratedDefaultCompilesAndPreservesValue(
        string type,
        string value,
        string expected
    )
    {
        AssertDefault(type, value, expected, "public enum Mode { Ready = 1, @default = 2 }");
    }

    [TestCase("bool", "false")]
    [TestCase("sbyte", "sbyte.MinValue")]
    [TestCase("sbyte", "sbyte.MaxValue")]
    [TestCase("byte", "byte.MinValue")]
    [TestCase("byte", "byte.MaxValue")]
    [TestCase("short", "short.MinValue")]
    [TestCase("short", "short.MaxValue")]
    [TestCase("ushort", "ushort.MinValue")]
    [TestCase("ushort", "ushort.MaxValue")]
    [TestCase("int", "int.MinValue")]
    [TestCase("int", "int.MaxValue")]
    [TestCase("uint", "uint.MinValue")]
    [TestCase("uint", "uint.MaxValue")]
    [TestCase("long", "long.MinValue")]
    [TestCase("long", "long.MaxValue")]
    [TestCase("ulong", "ulong.MinValue")]
    [TestCase("ulong", "ulong.MaxValue")]
    [TestCase("float", "float.MinValue")]
    [TestCase("float", "float.Epsilon")]
    [TestCase("float", "-0.0F")]
    [TestCase("float", "float.NegativeInfinity")]
    [TestCase("double", "double.NaN")]
    [TestCase("double", "double.PositiveInfinity")]
    [TestCase("double", "double.MaxValue")]
    [TestCase("double", "double.Epsilon")]
    [TestCase("double", "-0.0D")]
    [TestCase("decimal", "decimal.MinValue")]
    [TestCase("decimal", "1.2300M")]
    [TestCase("char", "'\\''")]
    [TestCase("char", "'\\0'")]
    [TestCase("string", "\"quote\\\" slash\\\\\\n\\r\\t\\0\\u0001\"")]
    [TestCase("int?", "42")]
    public void When_PrimitiveDefaultIsPresent_Then_PreservesMetadataAndPassedValue(string type, string value)
    {
        AssertDefault(type, value);
    }

    [TestCase("string?", "default", "null")]
    [TestCase("object?", "null", "null")]
    [TestCase("int?", "default", "null")]
    [TestCase("System.DateTime", "new System.DateTime()", "default")]
    [TestCase("(int, string?)", "default", "default")]
    public void When_NullOrStructDefaultIsPresent_Then_PreservesMeaning(string type, string value, string expected)
    {
        AssertDefault(type, value, expected);
    }

    [TestCase("", "T", "default")]
    [TestCase("", "T?", "default")]
    [TestCase("where T : struct", "T", "default")]
    [TestCase("where T : struct", "T?", "null")]
    [TestCase("where T : class", "T?", "null")]
    [TestCase("where T : class?", "T?", "null")]
    [TestCase("where T : notnull", "T", "default")]
    [TestCase("where T : System.IO.Stream", "T?", "null")]
    public void When_GenericDefaultIsPresent_Then_UsesNullOnlyForKnownNullableTypes(
        string constraint,
        string parameterType,
        string expected
    )
    {
        var typeArgument = constraint.Contains("Stream")
            ? "System.IO.MemoryStream"
            : constraint.Contains("class") ? "string" : "int";
        var compilation = CompileGeneratedCode(
            $$"""
            using Macaron.FactoryMethod;

            [AutoFactory]
            public partial class Sample<T> {{constraint}}
            {
                public {{parameterType}} Value;
                public Sample({{parameterType}} value = default) => Value = value;
            }

            public static class Probe
            {
                public static object?[] Values() => new object?[] {
                    new Sample<{{typeArgument}}>().Value, Sample<{{typeArgument}}>.Of().Value };
            }
            """
        );

        Assert.That(GeneratedText(compilation), Does.Contain($"{parameterType} value = {expected}"));
        AssertDefaultsAndRuntime(compilation, "Sample`1");
    }

    [TestCase("Mode", "Mode.Ready", "global::Mode.Ready")]
    [TestCase("Mode?", "(Mode)17", "(global::Mode)(17)")]
    [TestCase("Mode", "Mode.Ready | Mode.Other", "(global::Mode)(3)")]
    [TestCase("Mode", "Mode.Alias", "global::Mode.Ready")]
    public void When_EnumDefaultIsPresent_Then_UsesFirstExactMemberOrCast(string type, string value, string expected)
    {
        AssertDefault(type, value, expected, "[System.Flags] public enum Mode { Ready = 1, Alias = 1, Other = 2 }");
    }

    [TestCase("sbyte", "sbyte.MinValue")]
    [TestCase("byte", "byte.MaxValue")]
    [TestCase("short", "short.MinValue")]
    [TestCase("ushort", "ushort.MaxValue")]
    [TestCase("int", "int.MinValue")]
    [TestCase("uint", "uint.MaxValue")]
    [TestCase("long", "long.MinValue")]
    [TestCase("ulong", "ulong.MaxValue")]
    public void When_UnnamedEnumDefaultUsesUnderlyingBoundary_Then_CastPreservesValue(
        string underlyingType,
        string value
    )
    {
        AssertDefault("Mode?", $"(Mode){value}", declarations: $"public enum Mode : {underlyingType} {{ Zero }}");
    }

    [Test]
    public void When_ParameterTypeAndEnumIdentifiersAreKeywords_Then_EscapesQualifiedNames()
    {
        AssertDefault(
            "@namespace.@class",
            "@namespace.@class.@event",
            "global::@namespace.@class.@event",
            "namespace @namespace { public enum @class { @event = 1 } }"
        );
    }

    [Test]
    public void When_ParameterModifiersArePresent_Then_PreservesDeclarationsAndForwarding()
    {
        var compilation = CompileGeneratedCode(
            """
            using Macaron.FactoryMethod;

            [AutoFactory]
            public partial class Sample
            {
                public double Value;

                public Sample([System.ComponentModel.DataAnnotations.Required] ref int Count,
                    out string? Name, in double Value, params string?[] @event)
                {
                    Count++;
                    Name = @event[0];
                    this.Value = Value;
                }
            }
            public static class Probe
            {
                public static bool Verify()
                {
                    var count = 1;
                    var value = 2.5;
                    var sample = Sample.Of(ref count, out var name, in value, null, "text");

                    return count == 2 && name is null && sample.Value == value;
                }
            }
            """
        );

        Assert.That(
            GeneratedText(compilation),
            Does.Contain("[global::System.ComponentModel.DataAnnotations.RequiredAttribute] ref int count, out string? name, in double value, params string?[] @event")
        );

        WithAssembly(compilation, assembly =>
        {
            Assert.That(assembly.GetType("Probe")!.GetMethod("Verify")!.Invoke(null, null), Is.EqualTo(true));
        });
    }

    [TestCase("true")]
    [TestCase("'\\n'")]
    [TestCase("\"C:\\\\folder\\n\\t\\0\\u0001\\\"\"")]
    [TestCase("Mode.@default")]
    [TestCase("(Mode)17")]
    [TestCase("typeof(@namespace.@class)")]
    [TestCase("typeof(System.Collections.Generic.Dictionary<,>)")]
    [TestCase("new System.Type[] { typeof(@namespace.@class), typeof(int[]), null }")]
    [TestCase("new int[] { }")]
    [TestCase("(int[]?)null")]
    [TestCase("new object[] { true, (byte)1, (sbyte)-1, (short)-2, (ushort)2, 3U, 4L, 5UL, 'x', 1.5F, 2.5D, Mode.@default, typeof(int), null }")]
    [TestCase("new Mode[] { Mode.@default, (Mode)17 }")]
    [TestCase("new string[] { \"slash\\\\\\n\", null }")]
    [TestCase("float.NaN")]
    [TestCase("double.PositiveInfinity")]
    [TestCase("new object[] { 1D, -0D, 0U, 0L, 0UL, (System.Type?)null, (string?)null, (object?)null, (int[]?)null }")]
    public void When_AttributeConstantIsPresent_Then_PreservesArgumentTypeAndValue(string expression)
    {
        var compilation = CompileGeneratedCode(
            $$"""
            using System;
            using Macaron.FactoryMethod;

            namespace @namespace { public class @class { } }

            public enum Mode { @default = 1 }

            [AttributeUsage(AttributeTargets.Parameter)]
            public class ValueAttribute : Attribute
            {
                public ValueAttribute(object? value) { }
                public object? @event { get; set; }
            }

            [AutoFactory]
            public partial class Sample
            {
                public Sample([Value({{expression}}, @event = {{expression}})] int value = 0) { }
            }

            public static class Probe { public static bool Verify() => true; }
            """
        );

        WithAssembly(compilation, assembly =>
        {
            var type = assembly.GetType("Sample")!;
            var original = type
                .GetConstructors()
                .Single()
                .GetParameters()
                .Single()
                .GetCustomAttributesData()
                .Single(attribute => attribute.AttributeType.Name == "ValueAttribute");
            var generated = type
                .GetMethod("Of")!
                .GetParameters()
                .Single()
                .GetCustomAttributesData()
                .Single(attribute => attribute.AttributeType.Name == "ValueAttribute");

            AssertAttributeValue(original.ConstructorArguments.Single(), generated.ConstructorArguments.Single());
            Assert.That(generated.NamedArguments.Single().MemberName, Is.EqualTo("event"));
            AssertAttributeValue(
                original.NamedArguments.Single().TypedValue,
                generated.NamedArguments.Single().TypedValue
            );
        });
    }

    [TestCase("fr-FR")]
    [TestCase("tr-TR")]
    [TestCase("ar-SA")]
    public void When_CultureChanges_Then_GeneratedConstantsRemainIdentical(string culture)
    {
        const string source =
            """
            using Macaron.FactoryMethod;

            [AutoFactory]
            public partial class Sample
            {
                public Sample([System.ComponentModel.DataAnnotations.Range(1.5, 2.5)] double value = 1.25, decimal amount = 2.50M) { }
            }
            """;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

            var expected = GeneratedText(CompileGeneratedCode(source));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            Assert.That(GeneratedText(CompileGeneratedCode(source)), Is.EqualTo(expected));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private static void AssertDefault(string type, string value, string? expected = null, string declarations = "")
    {
        var compilation = CompileGeneratedCode(
            $$"""
            using Macaron.FactoryMethod;

            {{declarations}}

            [AutoFactory]
            public partial class Sample
            {
                public {{type}} Value;
                public Sample({{type}} value = {{value}}) => Value = value;
            }

            public static class Probe
            {
                public static object?[] Values() => new object?[] { new Sample().Value, Sample.Of().Value };
            }
            """
        );

        if (expected is not null)
        {
            Assert.That(GeneratedText(compilation), Does.Contain($"value = {expected}"));
        }

        AssertDefaultsAndRuntime(compilation, "Sample");
    }

    private static string GeneratedText(CSharpCompilation compilation)
    {
        return compilation.SyntaxTrees.Last().ToString();
    }

    private static void AssertDefaultsAndRuntime(CSharpCompilation compilation, string metadataName)
    {
        var symbol = compilation.GetTypeByMetadataName(metadataName)!;
        var original = symbol.InstanceConstructors.Single().Parameters.Single();
        var generated = ((IMethodSymbol)symbol.GetMembers("Of").Single()).Parameters.Single();

        Assert.That(generated.HasExplicitDefaultValue, Is.True);
        Assert.That(generated.ExplicitDefaultValue, Is.EqualTo(original.ExplicitDefaultValue));
        Assert.That(generated.Type, Is.EqualTo(original.Type));

        WithAssembly(compilation, assembly =>
        {
            var type = assembly.GetType(metadataName)!;
            var constructorParameter = type.GetConstructors().Single().GetParameters().Single();
            var factoryParameter = type.GetMethod("Of")!.GetParameters().Single();

            Assert.That(factoryParameter.HasDefaultValue, Is.True);
            AssertConstantValue(constructorParameter.DefaultValue, factoryParameter.DefaultValue);

            var values = (object?[])assembly.GetType("Probe")!.GetMethod("Values")!.Invoke(null, null)!;

            AssertConstantValue(values[0], values[1]);
        });
    }

    private static void AssertAttributeValue(CustomAttributeTypedArgument original, CustomAttributeTypedArgument generated)
    {
        Assert.That(generated.ArgumentType, Is.EqualTo(original.ArgumentType));

        if (original.Value is IEnumerable<CustomAttributeTypedArgument> originalValues)
        {
            var generatedValues = ((IEnumerable<CustomAttributeTypedArgument>)generated.Value!).ToArray();
            var expectedValues = originalValues.ToArray();

            Assert.That(generatedValues, Has.Length.EqualTo(expectedValues.Length));

            for (var i = 0; i < expectedValues.Length; ++i)
            {
                AssertAttributeValue(expectedValues[i], generatedValues[i]);
            }
        }
        else
        {
            AssertConstantValue(original.Value, generated.Value);
        }
    }

    private static void AssertConstantValue(object? original, object? generated)
    {
        Assert.That(generated, Is.EqualTo(original));

        if (original is float single && generated is float generatedSingle && !float.IsNaN(single))
        {
            Assert.That(BitConverter.SingleToInt32Bits(generatedSingle), Is.EqualTo(BitConverter.SingleToInt32Bits(single)));
        }
        else if (original is double number && generated is double generatedNumber && !double.IsNaN(number))
        {
            Assert.That(BitConverter.DoubleToInt64Bits(generatedNumber), Is.EqualTo(BitConverter.DoubleToInt64Bits(number)));
        }
        else if (original is decimal amount && generated is decimal generatedAmount)
        {
            Assert.That(decimal.GetBits(generatedAmount), Is.EqualTo(decimal.GetBits(amount)));
        }
    }

    private static void WithAssembly(CSharpCompilation compilation, Action<Assembly> assert)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.WithAssemblyName(Guid.NewGuid().ToString("N")).Emit(stream);

        Assert.That(emitResult.Success, Is.True, string.Join(Environment.NewLine, emitResult.Diagnostics));

        stream.Position = 0;

        var context = new AssemblyLoadContext(null, isCollectible: true);

        try
        {
            assert(context.LoadFromStream(stream));
        }
        finally
        {
            context.Unload();
        }
    }
}
