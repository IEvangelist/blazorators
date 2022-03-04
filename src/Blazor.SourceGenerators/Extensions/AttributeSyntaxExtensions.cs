// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.



namespace Blazor.SourceGenerators.Extensions;

static class AttributeSyntaxExtensions
{
    internal static GeneratorOptions GetGeneratorOptions(
        this AttributeSyntax attribute,
        bool supportsGenerics)
    {
        GeneratorOptions options = new(supportsGenerics);
        if (attribute is { ArgumentList: not null })
        {
            var removeQuotes = static string (string value) => value.Replace("\"", "");

            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                var propName = arg.NameEquals?.Name?.ToString();
                options = propName switch
                {
                    nameof(options.TypeName) => options with
                    {
                        TypeName = removeQuotes(arg.Expression.ToString())
                    },
                    nameof(options.PathFromWindow) => options with
                    {
                        PathFromWindow = removeQuotes(arg.Expression.ToString())
                    },
                    nameof(options.OnlyGeneratePureJS) => options with
                    {
                        OnlyGeneratePureJS = bool.Parse(arg.Expression.ToString())
                    },
                    nameof(options.Url) => options with
                    {
                        Url = removeQuotes(arg.Expression.ToString())
                    },
                    "HostingModel" => options with
                    {
                        IsWebAssembly = arg.Expression.ToString().Contains("WebAssembly")
                    },
                    nameof(options.GenericMethodDescriptors) => options with
                    {
                        GenericMethodDescriptors = ParseDescriptors(arg.Expression.ToString())
                    },

                    _ => options
                };
            }
        }

        return options;
    }

    static string[]? ParseDescriptors(string args)
    {
        var replacedArgs = args
            .Replace("new[]", "")
            .Replace("new []", "")
            .Replace("new string[]", "")
            .Replace("new string []", "")
            .Replace("{", "[")
            .Replace("}", "]");

        var values = SharedRegex.ArrayValuesRegex
            .GetMatchGroupValue(replacedArgs, "Values");

        if (values is not null)
        {
            var trimmed = values.Trim();
            var descriptors = trimmed.Split(',');

            return descriptors
                .Select(descriptor =>
                {
                    descriptor = descriptor
                        .Replace("\"", "")
                        .Trim();
                    return descriptor;
                })
                .ToArray();
        }

        return default;
    }
}
