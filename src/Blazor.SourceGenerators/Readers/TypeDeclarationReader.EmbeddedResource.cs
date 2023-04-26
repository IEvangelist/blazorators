// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    string GetEmbeddedResourceText(
        string resourceName = "Blazor.SourceGenerators.Data.lib.dom.d.ts")
    {
        using var stream = typeof(TypeDeclarationReader).Assembly
            .GetManifestResourceStream(resourceName)!;

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
