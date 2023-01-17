// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTypeLiteral : JsDocType
{
    public JsDocTypeLiteral()
    {
        Kind = TypeScriptSyntaxKind.JsDocTypeLiteral;
    }

    public NodeArray<JsDocPropertyTag> JsDocPropertyTags { get; set; }
    public JsDocTypeTag JsDocTypeTag { get; set; }
}