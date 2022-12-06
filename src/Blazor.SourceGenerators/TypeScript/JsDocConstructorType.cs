// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocConstructorType : JsDocType
{
    public JsDocConstructorType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocConstructorType;

    public IJsDocType Type { get; set; }
}