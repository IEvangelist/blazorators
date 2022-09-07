// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocNonNullableType : JsDocType
{
    internal JsDocNonNullableType() => ((INode)this).Kind = CommentKind.JsDocNonNullableType;

    internal IJsDocType Type { get; set; }
}