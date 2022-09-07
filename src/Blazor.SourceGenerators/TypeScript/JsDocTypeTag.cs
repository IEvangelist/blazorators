// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocTypeTag : JsDocTag
{
    internal JsDocTypeTag() => ((INode)this).Kind = CommentKind.JsDocTypeTag;

    internal JsDocTypeExpression TypeExpression { get; set; }
}