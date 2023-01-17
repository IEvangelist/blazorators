// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeInferences
{
    public TypeScriptType[] Primary { get; set; }
    public TypeScriptType[] Secondary { get; set; }
    public bool TopLevel { get; set; }
    public bool IsFixed { get; set; }
}