// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocNullableType : JsDocType
{
    internal JsDocNullableType() => ((INode)this).Kind = CommentKind.JsDocNullableType;

    internal IJsDocType Type { get; set; }
}