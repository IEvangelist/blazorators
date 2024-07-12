// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    static readonly ConcurrentDictionary<string, TypeDeclarationReader> s_readerCache =
        new(StringComparer.OrdinalIgnoreCase);

    internal static TypeDeclarationReader FactoryEmbedded()
    {
        return Default;
    }

    internal static TypeDeclarationReader Factory(string source)
    {
        var uri = new Uri(source);
        var sourceKey = uri.IsFile ? uri.LocalPath : uri.OriginalString;

        var reader =
            s_readerCache.GetOrAdd(
                sourceKey, _ => new TypeDeclarationReader(uri));

        return reader;
    }

    internal static TypeDeclarationReader Default
    {
        get
        {
            var sourceKey = "embedded";
            var reader =
                s_readerCache.GetOrAdd(
                    sourceKey, _ => new TypeDeclarationReader());

            return reader;
        }
    }
}
