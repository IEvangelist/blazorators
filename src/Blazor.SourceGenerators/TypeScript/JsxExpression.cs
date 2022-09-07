// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxExpression : Expression, IJsxChild
{
    internal JsxExpression() => ((INode)this).Kind = CommentKind.JsxExpression;

    internal Token DotDotDotToken { get; set; }
    internal IExpression Expression { get; set; }
}