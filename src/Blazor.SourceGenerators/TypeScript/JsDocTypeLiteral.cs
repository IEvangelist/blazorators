// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocTypeLiteral : JsDocType
{
    internal JsDocTypeLiteral() => ((INode)this).Kind = CommentKind.JsDocTypeLiteral;

    internal NodeArray<JsDocPropertyTag> JsDocPropertyTags { get; set; }
    internal JsDocTypeTag JsDocTypeTag { get; set; }
}