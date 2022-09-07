// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;


internal class JsDocTupleType : JsDocType
{
    internal JsDocTupleType() => ((INode)this).Kind = CommentKind.JsDocTupleType;

    internal NodeArray<IJsDocType> Types { get; set; }
}