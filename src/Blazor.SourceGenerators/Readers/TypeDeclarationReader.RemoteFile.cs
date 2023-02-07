// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    static readonly HttpClient s_httpClient = new();
    static readonly Uri s_defaultTypeDeclarationSource =
        new("https://raw.githubusercontent.com/microsoft/TypeScript/main/lib/lib.dom.d.ts");

    string GetRemoteFileText(string url)
    {
        var typeDeclarationText =
            s_httpClient.GetStringAsync(url)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return typeDeclarationText;
    }
}
