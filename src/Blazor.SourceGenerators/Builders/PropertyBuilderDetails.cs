// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Builders;

/// <summary>
/// Represents the details of a property builder, including the C# property, its name, the fully qualified JavaScript identifier, the return type, the bare type, the suffix, the extending type, and the generic type arguments.
/// </summary>
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
        var fullyQualifiedJavaScriptIdentifier = DetermineJavaScriptIdentifier(property, options);
        var (returnType, bareType) = property.GetPropertyTypes(options);
        var (suffix, extendingType) = DetermineSuffixAndExtendingType(options);
        var genericTypeArgs = DetermineGenericTypeArgs(bareType);

        return new PropertyBuilderDetails(
            Property: property,
            CSharpPropertyName: csharpPropertyName,
            FullyQualifiedJavaScriptIdentifier: fullyQualifiedJavaScriptIdentifier,
            ReturnType: returnType,
            BareType: bareType,
            Suffix: suffix,
            ExtendingType: extendingType,
            GenericTypeArgs: genericTypeArgs
        );
    }

    private static string DetermineJavaScriptIdentifier(CSharpProperty property, GeneratorOptions options)
    {
        return options.Implementation is not null
            ? $"{options.Implementation}.{property.RawName}"
            : property.RawName;
    }

    private static (string Suffix, string ExtendingType) DetermineSuffixAndExtendingType(GeneratorOptions options)
    {
        return options.IsWebAssembly ? ("", "IJSInProcessRuntime") : ("Async", "IJSRuntime");
    }

    private static string DetermineGenericTypeArgs(string bareType)
    {
        return $"<{bareType}>";
    }
}
