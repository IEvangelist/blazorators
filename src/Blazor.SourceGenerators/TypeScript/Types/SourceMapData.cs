// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class SourceMapData
{
    public string SourceMapFilePath { get; set; }
    public string JsSourceMappingUrl { get; set; }
    public string SourceMapFile { get; set; }
    public string SourceMapSourceRoot { get; set; }
    public string[] SourceMapSources { get; set; }
    public string[] SourceMapSourcesContent { get; set; }
    public string[] InputSourceFileNames { get; set; }
    public string[] SourceMapNames { get; set; }
    public string SourceMapMappings { get; set; }
    public SourceMapSpan[] SourceMapDecodedMappings { get; set; }
}