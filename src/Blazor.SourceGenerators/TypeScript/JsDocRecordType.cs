// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocRecordType : JsDocType
{
    internal JsDocRecordType() => ((INode)this).Kind = CommentKind.JsDocRecordType;

    internal TypeLiteralNode Literal { get; set; }
}