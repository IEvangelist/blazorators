// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxElement : PrimaryExpression, IJsxChild
{
    internal JsxElement() => ((INode)this).Kind = CommentKind.JsxElement;

    internal IExpression OpeningElement { get; set; } = default!;
    internal NodeArray<IJsxChild> JsxChildren { get; set; } = default!;
    internal JsxClosingElement ClosingElement { get; set; } = default!;
}