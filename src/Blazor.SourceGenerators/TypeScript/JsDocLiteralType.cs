// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocLiteralType : JsDocType
{
    public JsDocLiteralType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocLiteralType;

    public LiteralTypeNode Literal { get; set; }
}