// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDoc : Node
{
    public NodeArray<IJsDocTag> Tags { get; set; }
    public string Comment { get; set; }
}