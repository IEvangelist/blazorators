// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;
internal class JsDocTypeExpression : Node
{
    internal JsDocTypeExpression() => ((INode)this).Kind = CommentKind.JsDocTypeExpression;

    internal IJsDocType Type { get; set; }
}