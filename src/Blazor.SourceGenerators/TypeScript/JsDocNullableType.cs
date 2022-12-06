// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocNullableType : JsDocType
{
    public JsDocNullableType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocNullableType;

    public IJsDocType Type { get; set; }
}