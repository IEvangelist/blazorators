// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxText : Node, IJsxChild
{
    internal JsxText() => ((INode)this).Kind = CommentKind.JsxText;
}