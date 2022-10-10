// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocArrayType : JsDocType
{
    internal JsDocArrayType() => ((INode)this).Kind = SyntaxKind.JsDocArrayType;

    internal IJsDocType ElementType { get; set; }
}