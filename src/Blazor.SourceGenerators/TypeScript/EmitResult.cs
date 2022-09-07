// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EmitResult
{
    internal bool EmitSkipped { get; set; }
    internal Diagnostic[] Diagnostics { get; set; }
    internal string[] EmittedFiles { get; set; }
    internal SourceMapData[] SourceMaps { get; set; }
}