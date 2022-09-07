// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocThisType : JsDocType
{
    internal JsDocThisType() => ((INode)this).Kind = CommentKind.JsDocThisType;

    internal IJsDocType Type { get; set; }
}