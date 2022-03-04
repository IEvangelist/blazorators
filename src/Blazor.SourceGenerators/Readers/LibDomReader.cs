// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed class LibDomReader
{
    private static readonly HttpClient _httpClient = new();

    private readonly Lazy<IDictionary<string, string>> _lazyTypeDeclarationMap =
        new(() =>
        {
            ConcurrentDictionary<string, string> map = new();

            try
            {
                var rawUrl = "https://raw.githubusercontent.com/microsoft/TypeScript/main/lib/lib.dom.d.ts";
                var libDomDefinitionTypeScript = _httpClient.GetStringAsync(rawUrl).ConfigureAwait(false).GetAwaiter().GetResult();
                if (libDomDefinitionTypeScript is { Length: > 0 })
                {
                    var matchCollection = InterfaceRegex.Matches(libDomDefinitionTypeScript).Cast<Match>().Select(m => m.Value);
                    Parallel.ForEach(
                        matchCollection,
                        match =>
                        {
                            var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(match, "TypeName");
                            if (typeName is not null)
                            {
                                map[typeName] = match;
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error intializing lib dom parser. {ex}");
            }

            return map;
        });

    /// <summary>
    /// For testing purposes.
    /// </summary>
    internal bool IsInitialized => _lazyTypeDeclarationMap is { IsValueCreated: true };

    public bool TryGetDeclaration(
        string typeName, out string? declaration) =>
        _lazyTypeDeclarationMap.Value.TryGetValue(typeName, out declaration);
}
