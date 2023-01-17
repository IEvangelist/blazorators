// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class LiteralType : TypeScriptType
{
    public string Text { get; set; }
    public LiteralType FreshType { get; set; }
    public LiteralType RegularType { get; set; }
}