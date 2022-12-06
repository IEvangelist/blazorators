// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocTag : Node, IJsDocTag
{
    public JsDocTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTag;

    AtToken IJsDocTag.AtToken? { get; set; }
    Identifier IJsDocTag.TagName? { get; set; }
    string IJsDocTag.Comment? { get; set; }
}