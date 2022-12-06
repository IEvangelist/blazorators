// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDoc : Node
{
    public NodeArray<IJsDocTag> Tags? { get; set; }
    public string? Comment { get; set; }
}