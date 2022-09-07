// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocAugmentsTag : JsDocTag
{
    internal JsDocAugmentsTag() => ((INode)this).Kind = CommentKind.JsDocAugmentsTag;

    internal JsDocTypeExpression TypeExpression { get; set; }
}