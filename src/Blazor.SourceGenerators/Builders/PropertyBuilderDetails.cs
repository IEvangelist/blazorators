// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Builders;

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
