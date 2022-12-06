// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocNonNullableType : JsDocType
{
    public JsDocNonNullableType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocNonNullableType;

    public IJsDocType Type { get; set; }
}