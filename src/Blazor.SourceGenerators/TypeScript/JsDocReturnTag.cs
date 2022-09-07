// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocReturnTag : JsDocTag
{
    internal JsDocReturnTag() => ((INode)this).Kind = CommentKind.JsDocReturnTag;

    internal JsDocTypeExpression TypeExpression { get; set; }
}