// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

// GeneratorOptions itself lives in the global namespace (see
// `Options/GeneratorOptions.cs`); match that convention so consumers of this
// helper - mainly `JavaScriptInteropGenerator.BuildTarget` - don't need an
// additional `using`.

/// <summary>
/// Reasonable-default inference for <see cref="GeneratorOptions"/> so a
/// consumer can opt into a minimal attribute form. When the consumer writes
/// <c>[JSAutoInterop]</c> (no arguments) on a <c>partial interface</c>, this
/// helper fills in the <see cref="GeneratorOptions.TypeName"/> and
/// <see cref="GeneratorOptions.Implementation"/> properties from the
/// interface name; explicit attribute arguments always win.
/// </summary>
internal static class OptionsInference
{
    private const string ServiceSuffix = "Service";

    /// <summary>
    /// Apply inferred defaults to <paramref name="options"/> based on
    /// <paramref name="interfaceName"/>. Any property that the consumer has
    /// already supplied (non-null) is preserved as-is. If inference cannot
    /// produce a sensible <see cref="GeneratorOptions.TypeName"/>, the value
    /// is left null so the existing BR0001 diagnostic continues to fire.
    /// </summary>
    internal static GeneratorOptions ApplyInferredDefaults(
        GeneratorOptions options,
        string interfaceName)
    {
        var typeName = options.TypeName ?? InferTypeName(interfaceName);

        // When TypeName couldn't be inferred (e.g. degenerate "IService"),
        // leave both as the consumer provided them so BR0001/BR0002 surface
        // the error to the user without a misleading inferred Implementation.
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return options;
        }

        var implementation = options.Implementation ?? InferImplementation(typeName!);

        if (ReferenceEquals(typeName, options.TypeName) &&
            ReferenceEquals(implementation, options.Implementation))
        {
            return options;
        }

        return options with
        {
            TypeName = typeName!,
            Implementation = implementation,
        };
    }

    /// <summary>
    /// Strip a leading <c>I</c> (when followed by an uppercase letter, so
    /// <c>Inline</c> stays put) and a trailing <c>Service</c> from
    /// <paramref name="interfaceName"/>. Returns <c>null</c> when the result
    /// would be empty or whitespace (e.g. <c>IService</c>).
    /// </summary>
    internal static string? InferTypeName(string? interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return null;
        }

        var name = interfaceName!;

        if (name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        if (name.EndsWith(ServiceSuffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - ServiceSuffix.Length);
        }

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Produce <c>window.{camelCase(typeName)}</c> for the common shape
    /// where a DOM API hangs off the global <c>window</c> object (e.g.
    /// <c>localStorage</c>, <c>sessionStorage</c>, <c>console</c>,
    /// <c>history</c>, <c>location</c>, <c>crypto</c>). When the underlying
    /// API lives elsewhere (e.g. <c>navigator.geolocation</c>) the consumer
    /// must continue to supply <c>Implementation</c> explicitly.
    /// </summary>
    internal static string InferImplementation(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return "window";
        }

        var head = char.ToLowerInvariant(typeName[0]);
        var tail = typeName.Length > 1 ? typeName.Substring(1) : string.Empty;
        return $"window.{head}{tail}";
    }
}
