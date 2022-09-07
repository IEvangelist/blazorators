// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocUnknownType : JsDocType
{
    internal JsDocUnknownType() => ((INode)this).Kind = CommentKind.JsDocUnknownType;
}