// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

/// <summary>
/// The options used (and parsed from the <c>JSAutoInteropAttribute</c>) to source-generate JavaScript interop.
/// </summary>
/// <param name="SupportsGenerics">Indicates whether the generator supports generics.</param>
/// <param name="TypeName">The type name that corresponds to the lib.dom.d.ts interface. For example, <c>"Geolocation"</c></param>
/// <param name="Implementation">The path from the <c>window</c> object. For example, <c>"window.navigator.geolocation"</c> (or <c>"navigator.geolocation"</c>)</param>
/// <param name="OnlyGeneratePureJS">Whether to generate only pure JavaScript functions that do not require callbacks. For example, <c>Geolocation.clearWatch</c> is consider pure, but <c>Geolocation.watchPosition</c> is not.</param>
/// <param name="Url">The optional URL to the corresponding API.</param>
/// <param name="GenericMethodDescriptors">The optional generic method descriptors value from the parsed <c>JSAutoGenericInteropAttribute.GenericMethodDescriptors</c>.</param>
/// <param name="PureJavaScriptOverrides">Overrides pure JavaScript calls. A custom impl must exist.</param>
/// <param name="TypeDeclarationSources">An optional array of TypeScript type declarations sources. Valid values are URLs or file paths.</param>
/// <param name="IsWebAssembly">
/// A value indicating whether to generate targeting WASM:
/// When <c>true</c>: Synchronous extensions are generated on the <c>IJSInProcessRuntime</c> type.
/// When <c>false</c>: Asynchronous extensions are generated on the <c>IJSRuntime</c> type.
/// </param>
internal sealed record GeneratorOptions(
    bool SupportsGenerics,
    string TypeName = null!,
    string Implementation = null!,
    bool OnlyGeneratePureJS = false,
    string? Url = null,
    string[]? GenericMethodDescriptors = null,
    string[]? PureJavaScriptOverrides = null,
    string[]? TypeDeclarationSources = null,
    bool IsWebAssembly = true)
{
    /// <summary>
    /// Get <see cref="GeneratorOptions"/> instance maps its
    /// <see cref="TypeDeclarationSources"/> into a set of parsers.
    /// When <see cref="TypeDeclarationSources"/> is null, or empty,
    /// the default <i>lib.dom.d.ts</i> parser is used.
    /// </summary>
#pragma warning disable CS9264 // Non-nullable property must contain a non-null value when exiting constructor. Consider adding the 'required' modifier, or declaring the property as nullable, or adding '[field: MaybeNull, AllowNull]' attributes.
    internal ISet<TypeDeclarationParser> Parsers =>
#pragma warning restore CS9264 // Non-nullable property must contain a non-null value when exiting constructor. Consider adding the 'required' modifier, or declaring the property as nullable, or adding '[field: MaybeNull, AllowNull]' attributes.
        field ??= BuildParsers();

    private ISet<TypeDeclarationParser> BuildParsers() =>
        // `Parsers` is the fallback path used when `TypeDeclarationSources`
        // is null/empty - the generator always wants a parser for the
        // embedded `lib.dom.d.ts`. When `TypeDeclarationSources` is set,
        // the generator builds per-source parsers from `AdditionalFiles`
        // directly inside `JavaScriptInteropGenerator.ResolveParsers` and
        // never reaches this code path. Returning a single shared parser
        // here keeps the work cached.
        new HashSet<TypeDeclarationParser> { TypeDeclarationParser.Default };

    public bool Equals(GeneratorOptions? other) =>
        other is not null &&
        SupportsGenerics == other.SupportsGenerics &&
        TypeName == other.TypeName &&
        Implementation == other.Implementation &&
        OnlyGeneratePureJS == other.OnlyGeneratePureJS &&
        Url == other.Url &&
        IsWebAssembly == other.IsWebAssembly &&
        ArrayEquals(GenericMethodDescriptors, other.GenericMethodDescriptors) &&
        ArrayEquals(PureJavaScriptOverrides, other.PureJavaScriptOverrides) &&
        ArrayEquals(TypeDeclarationSources, other.TypeDeclarationSources);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + SupportsGenerics.GetHashCode();
            hash = (hash * 31) + (TypeName?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Implementation?.GetHashCode() ?? 0);
            hash = (hash * 31) + OnlyGeneratePureJS.GetHashCode();
            hash = (hash * 31) + (Url?.GetHashCode() ?? 0);
            hash = (hash * 31) + IsWebAssembly.GetHashCode();
            hash = (hash * 31) + ArrayHashCode(GenericMethodDescriptors);
            hash = (hash * 31) + ArrayHashCode(PureJavaScriptOverrides);
            hash = (hash * 31) + ArrayHashCode(TypeDeclarationSources);
            return hash;
        }
    }

    private static bool ArrayEquals(string[]? left, string[]? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static int ArrayHashCode(string[]? items)
    {
        if (items is null)
        {
            return -1;
        }

        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + items.Length;
            foreach (var item in items)
            {
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }
}
