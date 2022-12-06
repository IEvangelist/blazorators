// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EmitResult
{
    public bool EmitSkipped { get; set; }
    public TypeScriptDiagnostic[] Diagnostics { get; set; } = Array.Empty<TypeScriptDiagnostic>();
    public string[] EmittedFiles { get; set; } = Array.Empty<string>();
    public SourceMapData[] SourceMaps { get; set; } = Array.Empty<SourceMapData>();
}