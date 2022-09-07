// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocConstructorType : JsDocType
{
    internal JsDocConstructorType() => ((INode)this).Kind = CommentKind.JsDocConstructorType;

    internal IJsDocType Type { get; set; }
}