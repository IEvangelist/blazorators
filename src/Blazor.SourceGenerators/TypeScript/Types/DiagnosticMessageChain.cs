// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class DiagnosticMessageChain
{
    public string MessageText { get; set; }
    public DiagnosticCategory Category { get; set; }
    public int Code { get; set; }
    public DiagnosticMessageChain Next { get; set; }
}