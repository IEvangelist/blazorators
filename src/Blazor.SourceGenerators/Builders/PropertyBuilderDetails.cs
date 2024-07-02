// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Builders;

/// <summary>
/// Represents the details of a property builder, including the C# property, its name, the fully qualified JavaScript identifier, the return type, the bare type, the suffix, the extending type, and the generic type arguments.
/// </summary>
/// <param name="Property">The <see cref="CSharpProperty"/> to generate code for.</param>
/// <param name="CSharpPropertyName">The name of the property.</param>
/// <param name="FullyQualifiedJavaScriptIdentifier">The fully qualified JavaScript identifier.</param>
/// <param name="ReturnType">The return type of the property.</param>
/// <param name="BareType">The bare type of the property.</param>
/// <param name="Suffix">The suffix to append to the property name.</param>
/// <param name="ExtendingType">The type to extend.</param>
/// <param name="GenericTypeArgs">The generic type arguments.</param>
internal readonly record struct PropertyBuilderDetails(
    CSharpProperty Property,
    string CSharpPropertyName,
    string FullyQualifiedJavaScriptIdentifier,
    string ReturnType,
    string BareType,
    string Suffix,
    string ExtendingType,
    string GenericTypeArgs)
{
    /// <summary>
    /// Creates a new instance of <see cref="PropertyBuilderDetails"/> based on the provided <see cref="CSharpProperty"/> and <see cref="GeneratorOptions"/>.
    /// </summary>
    /// <param name="property">The <see cref="CSharpProperty"/> to use for creating the <see cref="PropertyBuilderDetails"/>.</param>
    /// <param name="options">The <see cref="GeneratorOptions"/> to use for creating the <see cref="PropertyBuilderDetails"/>.</param>
    /// <returns>A new instance of <see cref="PropertyBuilderDetails"/> based on the provided <see cref="CSharpProperty"/> and <see cref="GeneratorOptions"/>.</returns>
    internal static PropertyBuilderDetails Create(CSharpProperty property, GeneratorOptions options)
    {
        var csharpPropertyName = property.RawName.CapitalizeFirstLetter();
        var javaScriptIndentifier = options.Implementation is not null
            ? $"{options.Implementation}.{property.RawName}"
            : property.RawName;
        var (returnType, bareType) = property.GetPropertyTypes(options);
        var (suffix, extendingType) =
            options.IsWebAssembly ? ("", "IJSInProcessRuntime") : ("Async", "IJSRuntime");
        var genericTypeArgs = $"<{bareType}>";

        return new PropertyBuilderDetails(
            Property: property,
            CSharpPropertyName: csharpPropertyName,
            FullyQualifiedJavaScriptIdentifier: javaScriptIndentifier,
            ReturnType: returnType,
            BareType: bareType,
            Suffix: suffix,
            ExtendingType: extendingType,
            GenericTypeArgs: genericTypeArgs);
    }
}
