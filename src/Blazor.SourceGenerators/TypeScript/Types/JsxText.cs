// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxText : Node, IJsxChild
{
    public JsxText()
    {
        Kind = TypeScriptSyntaxKind.JsxText;
    }
}