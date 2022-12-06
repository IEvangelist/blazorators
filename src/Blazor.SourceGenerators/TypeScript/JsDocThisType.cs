// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocThisType : JsDocType
{
    public JsDocThisType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocThisType;

    public IJsDocType Type { get; set; }
}