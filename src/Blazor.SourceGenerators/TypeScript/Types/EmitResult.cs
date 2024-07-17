// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Compiler;

// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EmitResult
{
    public bool EmitSkipped { get; set; }
    public TypeScriptDiagnostic[] Diagnostics { get; set; }
    public string[] EmittedFiles { get; set; }
    public SourceMapData[] SourceMaps { get; set; }
}