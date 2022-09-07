// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDoc : Node
{
    internal NodeArray<IJsDocTag> Tags { get; set; } = default!;
    internal string Comment { get; set; } = default!;
}