// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxSelfClosingElement : PrimaryExpression, IJsxChild
{
    internal JsxSelfClosingElement() => ((INode)this).Kind = CommentKind.JsxSelfClosingElement;

    internal IJsxTagNameExpression TagName { get; set; }
    internal JsxAttributes Attributes { get; set; }
}