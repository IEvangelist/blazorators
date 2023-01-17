// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IndexInfo
{
    public TypeScriptType Type { get; set; }
    public bool IsReadonly { get; set; }
    public SignatureDeclaration Declaration { get; set; }
}