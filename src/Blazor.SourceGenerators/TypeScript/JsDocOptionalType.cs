// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocOptionalType : JsDocType
{
    public JsDocOptionalType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocOptionalType;

    public IJsDocType Type { get; set; }
}