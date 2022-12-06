// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxText : Node, IJsxChild
{
    public JsxText() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxText;
}