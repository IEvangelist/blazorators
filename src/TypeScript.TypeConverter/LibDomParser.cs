// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TypeScript.TypeConverter;

public class LibDomParser
{
    private readonly string _rawUrl = "https://raw.githubusercontent.com/microsoft/TypeScript/main/lib/lib.dom.d.ts";
    private readonly HttpClient _httpClient = new();
    private readonly ConcurrentDictionary<string, string> _typeNameToTypeDefinitionMap = new();

    // See: https://regex101.com/r/GV3DiG/1
    private readonly Regex _interfacesRegex = new("(?'declaration'interface.*?{.*?})", RegexOptions.Singleline);
    private readonly Regex _interfaceTypeName = new("(?:interface )(?'TypeName'\\S+)");

    /// <summary>
    /// For testing purposes.
    /// </summary>
    internal bool IsInitialized => _typeNameToTypeDefinitionMap is { Count: > 100 };

    public async Task InitializeAsync()
    {
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

    //private void ParseDefinitions(string libDomDefinitionTypeScript)
    //{
    //    //var tokenizer = new StringTokenizer(libDomDefinitionTypeScript, );
    //}

    public bool TryParseType(string typeName, bool isParameter, out string? csharpSourceText)
    {
        if (_typeNameToTypeDefinitionMap.TryGetValue(typeName, out var typeScriptDefinitionText))
        {
            csharpSourceText = typeScriptDefinitionText.AsCSharpSourceText(isParameter);
            return true;
        }

        csharpSourceText = null;
        return false;
    }
}
