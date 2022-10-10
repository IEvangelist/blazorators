// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocLiteralType : JsDocType
{
    internal JsDocLiteralType() => ((INode)this).Kind = SyntaxKind.JsDocLiteralType;

    internal LiteralTypeNode Literal { get; set; }
}