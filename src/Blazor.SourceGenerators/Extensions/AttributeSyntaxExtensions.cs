// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class AttributeSyntaxExtensions
{
    /// <summary>
    /// Reads strongly-typed values out of an attribute's argument list.
    ///
    /// When a non-null <paramref name="semanticModel"/> is supplied, the
    /// parser routes every scalar argument through
    /// <see cref="SemanticModel.GetConstantValue(SyntaxNode, System.Threading.CancellationToken)"/>,
    /// which:
    ///
    ///   - resolves <c>const</c> and <c>nameof(...)</c> argument expressions to
    ///     their bound values,
    ///   - decodes string-literal escape sequences correctly (the pre-audit
    ///     text-based parser flat-stripped <c>"</c> characters with
    ///     <c>Replace("\"", "")</c>, which mangled <c>\\"</c> inside a literal),
    ///   - supports verbatim strings (<c>@"..."</c>) and raw-string literals.
    ///
    /// The semantic model is wired in via <c>GeneratorAttributeSyntaxContext.SemanticModel</c>
    /// from the generator's <c>ForAttributeWithMetadataName</c> transform (see T1.11).
    /// If <paramref name="semanticModel"/> is null we fall back to the syntactic path -
    /// kept around as a safety net but no longer exercised by the main pipeline.
    /// </summary>
    internal static GeneratorOptions GetGeneratorOptions(
        this AttributeSyntax attribute,
        bool supportsGenerics,
        SemanticModel? semanticModel = null)
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
                        TypeName = ReadString(arg.Expression, semanticModel)!
                    },
                    nameof(options.Implementation) => options with
                    {
                        Implementation = ReadString(arg.Expression, semanticModel)!
                    },
                    nameof(options.OnlyGeneratePureJS) => options with
                    {
                        OnlyGeneratePureJS = ReadBool(arg.Expression, semanticModel)
                    },
                    nameof(options.Url) => options with
                    {
                        Url = ReadString(arg.Expression, semanticModel)
                    },
                    "HostingModel" => options with
                    {
                        // BlazorHostingModel.WebAssembly is the source-of-truth
                        // member name. Compare textually so we don't have to
                        // pin the enum's underlying integer value across
                        // versions. `Contains(string)` lacks a
                        // `StringComparison` overload on netstandard2.0
                        // and falls through to the culture-sensitive
                        // implementation -- the analyzer process inherits
                        // the host machine's culture, so use
                        // `IndexOf(..., StringComparison.Ordinal)` for an
                        // explicitly ordinal substring check. The default-
                        // to-true branch keeps the prior behaviour when
                        // `HostingModel` is omitted entirely (the
                        // attribute's own default is `WebAssembly`).
                        IsWebAssembly = (ReadEnumMemberName(arg.Expression, semanticModel)?.IndexOf("WebAssembly", StringComparison.Ordinal) ?? 0) >= 0
                    },
                    nameof(options.GenericMethodDescriptors) => options with
                    {
                        GenericMethodDescriptors = ReadStringArray(arg.Expression, semanticModel)
                    },
                    nameof(options.PureJavaScriptOverrides) => options with
                    {
                        PureJavaScriptOverrides = ReadStringArray(arg.Expression, semanticModel)
                    },
                    nameof(options.TypeDeclarationSources) => options with
                    {
                        TypeDeclarationSources = ReadStringArray(arg.Expression, semanticModel)
                    },

                    _ => options
                };
            }
        }

        return options;
    }

    private static string? ReadString(ExpressionSyntax expr, SemanticModel? semanticModel)
    {
        if (semanticModel is not null)
        {
            var constant = semanticModel.GetConstantValue(expr);
            if (constant.HasValue && constant.Value is string s)
            {
                return s;
            }
        }

        return StripQuotesFallback(expr.ToString());
    }

    private static bool ReadBool(ExpressionSyntax expr, SemanticModel? semanticModel)
    {
        if (semanticModel is not null)
        {
            var constant = semanticModel.GetConstantValue(expr);
            if (constant.HasValue && constant.Value is bool b)
            {
                return b;
            }
        }

        return bool.TryParse(expr.ToString(), out var parsed) && parsed;
    }

    private static string? ReadEnumMemberName(ExpressionSyntax expr, SemanticModel? semanticModel)
    {
        if (semanticModel is not null)
        {
            // Member-access shape (`BlazorHostingModel.Server`) - resolve to the
            // member name via the bound symbol; far more robust than substring
            // matching on the raw expression text.
            var symbol = semanticModel.GetSymbolInfo(expr).Symbol;
            if (symbol is { } sym)
            {
                return sym.Name;
            }

            var constant = semanticModel.GetConstantValue(expr);
            if (constant.HasValue && constant.Value is not null)
            {
                return constant.Value.ToString();
            }
        }

        return expr.ToString();
    }

    private static string[]? ReadStringArray(ExpressionSyntax expr, SemanticModel? semanticModel)
    {
        // Array literals come in three forms:
        //   new[] { "a", "b" }       -> ImplicitArrayCreationExpressionSyntax
        //   new string[] { "a", "b" } -> ArrayCreationExpressionSyntax
        //   ["a", "b"]                -> CollectionExpressionSyntax (C# 12+)
        // Handle each shape via its bound syntax tree before falling back to
        // text-based parsing - that fallback is brittle around escaped quotes
        // and embedded commas, and only exists for shapes we don't recognize.
        if (expr is CollectionExpressionSyntax collection)
        {
            var collectionValues = new List<string>();
            foreach (var element in collection.Elements)
            {
                if (element is ExpressionElementSyntax exprElement)
                {
                    var value = ReadString(exprElement.Expression, semanticModel);
                    if (value is not null)
                    {
                        collectionValues.Add(value);
                    }
                }
            }

            return collectionValues.Count > 0 ? collectionValues.ToArray() : null;
        }

        InitializerExpressionSyntax? initializer = expr switch
        {
            ImplicitArrayCreationExpressionSyntax impl => impl.Initializer,
            ArrayCreationExpressionSyntax expl => expl.Initializer,
            _ => null,
        };

        if (initializer is null)
        {
            // Fall back to the legacy regex-based path so weird shapes don't
            // become silent nulls.
            return ParseArrayFallback(expr.ToString());
        }

        var values = new List<string>();
        foreach (var element in initializer.Expressions)
        {
            var value = ReadString(element, semanticModel);
            if (value is not null)
            {
                values.Add(value);
            }
        }

        return values.Count > 0 ? values.ToArray() : null;
    }

    private static string? StripQuotesFallback(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        // Strip a leading `@` (verbatim string prefix) and surrounding quotes.
        if (raw[0] == '@' && raw.Length >= 3)
        {
            raw = raw.Substring(1);
        }

        if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
        {
            return raw.Substring(1, raw.Length - 2);
        }

        return raw;
    }

    private static string[]? ParseArrayFallback(string args)
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

            return [.. descriptors
                .Select(descriptor =>
                {
                    descriptor = descriptor
                        .Replace("\"", "")
                        .Trim();
                    return descriptor;
                })];
        }

        return default;
    }
}
