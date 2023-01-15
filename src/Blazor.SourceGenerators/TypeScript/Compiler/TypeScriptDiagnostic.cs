// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.TypeScript.Compiler;

public class TypeScriptDiagnostic
{
    public SourceFile File { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
    public object MessageText { get; set; }
    public DiagnosticCategory Category { get; set; }
    public int Code { get; set; }
}
