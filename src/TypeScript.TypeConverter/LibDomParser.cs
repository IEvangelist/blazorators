// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TypeScript.TypeConverter;

public class LibDomParser
{
    private static readonly ConcurrentDictionary<string, string> _typeNameToTypeDefinitionMap = new();

    private readonly string _rawUrl = "https://raw.githubusercontent.com/microsoft/TypeScript/main/lib/lib.dom.d.ts";
    private readonly HttpClient _httpClient = new();

    // See: https://regex101.com/r/GV3DiG/1
    private readonly Regex _interfacesRegex = new("(?'declaration'interface.*?{.*?})", RegexOptions.Singleline);
    private readonly Regex _interfaceTypeName = new("(?:interface )(?'TypeName'\\S+)");

    /// <summary>
    /// For testing purposes.
    /// </summary>
    internal bool IsInitialized => _typeNameToTypeDefinitionMap is { Count: > 0 };

    public async ValueTask InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            var libDomDefinitionTypeScript = await _httpClient.GetStringAsync(_rawUrl);
            if (libDomDefinitionTypeScript is { Length: > 0 })
            {
                foreach (Match match in _interfacesRegex.Matches(libDomDefinitionTypeScript))
                {
                    var typeName = _interfaceTypeName.GetMatchGroupValue(match.Value, "TypeName");
                    if (typeName is not null)
                    {
                        _typeNameToTypeDefinitionMap[typeName] = match.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error intializing lib dom parser. {ex}");
        }
    }

    public bool TryParseType(string typeName, out string? csharpSourceText)
    {
        // TODO:
        // This needs to become smarter.
        // It needs to account for the fact that a single API could define peripheral assets in both
        // JavaScript and C# files.
        // As such it should probably return a more comprehensive type.
        if (_typeNameToTypeDefinitionMap.TryGetValue(typeName, out var typeScriptDefinitionText))
        {
            csharpSourceText = typeScriptDefinitionText.AsCSharpSourceText();
            return true;
        }

        csharpSourceText = null;
        return false;
    }
}
