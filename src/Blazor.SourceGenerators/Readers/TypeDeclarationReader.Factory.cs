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
        // Legacy entry point kept around for back-compat. The current
        // generator pipeline reads consumer-supplied .d.ts content via
        // `AdditionalFiles` (see `JavaScriptInteropGenerator.ResolveParsers`)
        // and constructs `TypeDeclarationReader` directly from the in-memory
        // content, bypassing this factory entirely. New call sites should
        // prefer that path. Retained because removing it would break source-
        // level back-compat for downstream consumers that referenced it
        // (it's `internal`, but the assembly ships as an analyzer so we
        // keep the surface stable across versions).
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
