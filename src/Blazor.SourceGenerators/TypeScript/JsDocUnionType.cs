// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocUnionType : JsDocType
{
    internal JsDocUnionType() => ((INode)this).Kind = CommentKind.JsDocUnionType;

    internal NodeArray<IJsDocType> Types { get; set; }
}