// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Options;

namespace Blazor.SourceGenerators.Extensions;

internal static class AttributeSyntaxExtensions
{
    internal static GeneratorOptions GetGeneratorOptions(
        this AttributeSyntax attribute,
        bool supportsGenerics)
    {
        GeneratorOptions options = new(supportsGenerics);
        if (attribute is { ArgumentList: not null })
        {
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                var propName = arg.NameEquals?.Name?.ToString();
                options = propName switch
                {
                    nameof(options.TypeName) => options with
                    {
                        TypeName = RemoveQuotes(arg.Expression.ToString())
                    },
                    nameof(options.Implementation) => options with
                    {
                        Implementation = RemoveQuotes(arg.Expression.ToString())
                    },
                    nameof(options.OnlyGeneratePureJS) => options with
                    {
                        OnlyGeneratePureJS = bool.Parse(arg.Expression.ToString())
                    },
                    nameof(options.Url) => options with
                    {
                        Url = RemoveQuotes(arg.Expression.ToString())
                    },
                    "HostingModel" => options with
                    {
                        IsWebAssembly = arg.Expression.ToString().Contains("WebAssembly")
                    },
                    nameof(options.GenericMethodDescriptors) => options with
                    {
                        GenericMethodDescriptors = ParseArray(arg.Expression.ToString())
                    },
                    nameof(options.PureJavaScriptOverrides) => options with
                    {
                        PureJavaScriptOverrides = ParseArray(arg.Expression.ToString())
                    },
                    nameof(options.TypeDeclarationSources) => options with
                    {
                        TypeDeclarationSources = ParseArray(arg.Expression.ToString())
                    },

                    _ => options
                };
            }
        }

        return options;
    }

    static string[]? ParseArray(string args)
    {
        // Remove unwanted parts of the string
        var replacedArgs = args
            .Replace("new[]", "")
            .Replace("new []", "")
            .Replace("new string[]", "")
            .Replace("new string []", "")
            .Replace("{", "[")
            .Replace("}", "]")
            .Trim();

        // Find the first '[' and the last ']' to extract the array contents
        var startIndex = replacedArgs.IndexOf('[') + 1;
        var endIndex = replacedArgs.LastIndexOf(']');

        // Check if the brackets are correctly positioned
        if (startIndex > 0 && endIndex > startIndex)
        {
            var values = replacedArgs.Substring(startIndex, endIndex - startIndex);

            // Split the values by commas
            var descriptors = values.Split(',');

            return descriptors
                .Select(descriptor => RemoveQuotes(descriptor).Trim())
                .ToArray();
        }

        return default;
    }


    private static string RemoveQuotes(string value) => value.Replace("\"", "");
}