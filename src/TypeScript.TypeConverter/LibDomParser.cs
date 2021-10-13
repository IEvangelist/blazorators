// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace TypeScript.TypeConverter;

public class LibDomParser
{
    private readonly string _rawUrl = "https://raw.githubusercontent.com/microsoft/TypeScript/main/lib/lib.dom.d.ts";
    private readonly HttpClient _httpClient = new();
    private readonly ConcurrentDictionary<string, string> _typeNameToTypeDefinitionMap = new();

    public async Task InitializeAsync()
    {
        try
        {
            var libDomDefinitionTypeScript = await _httpClient.GetStringAsync(_rawUrl);
            if (libDomDefinitionTypeScript is { Length: > 0 })
            {
                // TODO: parse entire file into
                // _typeNameToTypeDefinitionMap

                // key: type name
                // value: type definition
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error intializing lib dom parser. {ex}");
        }
    }

    private void ParseDefinitions(string libDomDefinitionTypeScript)
    {
        //var tokenizer = new StringTokenizer(libDomDefinitionTypeScript, );
    }

    public bool TryParseType(string typeName, out string? csharpSourceText)
    {
        if (_typeNameToTypeDefinitionMap.TryGetValue(typeName, out var typeScriptDefinitionText))
        {
            csharpSourceText = typeScriptDefinitionText.AsCSharpSourceText();
            return true;
        }

        csharpSourceText = null;
        return false;
    }
}
