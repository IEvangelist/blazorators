// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocTag : Node, IJsDocTag
{
    internal JsDocTag() => ((INode)this).Kind = CommentKind.JsDocTag;

    AtToken IJsDocTag.AtToken { get; set; } = default!;
    Identifier IJsDocTag.TagName { get; set; } = default!;
    string IJsDocTag.Comment { get; set; } = default!;
}