// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SourceMapData
{
    internal string SourceMapFilePath { get; set; }
    internal string JsSourceMappingUrl { get; set; }
    internal string SourceMapFile { get; set; }
    internal string SourceMapSourceRoot { get; set; }
    internal string[] SourceMapSources { get; set; }
    internal string[] SourceMapSourcesContent { get; set; }
    internal string[] InputSourceFileNames { get; set; }
    internal string[] SourceMapNames { get; set; }
    internal string SourceMapMappings { get; set; }
    internal SourceMapSpan[] SourceMapDecodedMappings { get; set; }
}