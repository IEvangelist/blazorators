// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxClosingElement : Node
{
    internal JsxClosingElement() => ((INode)this).Kind = CommentKind.JsxClosingElement;

    internal IJsxTagNameExpression TagName { get; set; } = default!;
}