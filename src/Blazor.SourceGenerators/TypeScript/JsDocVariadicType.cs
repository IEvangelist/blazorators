// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocVariadicType : JsDocType
{
    public JsDocVariadicType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocVariadicType;

    public IJsDocType Type { get; set; }
}