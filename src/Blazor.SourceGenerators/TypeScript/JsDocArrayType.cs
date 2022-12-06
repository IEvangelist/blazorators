// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocArrayType : JsDocType
{
    public JsDocArrayType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocArrayType;

    public IJsDocType ElementType { get; set; }
}