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
        // `TypeDeclarationSources` is reserved for future use; the generator
        // currently always parses the embedded `lib.dom.d.ts`. When the
        // scaffolded URL/file ingestion is implemented this method should
        // resolve `source` (file path or URL), hash-invalidate against the
        // `s_readerCache`, and return a per-source `TypeDeclarationReader`.
        _ = source;
        return Default;
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
