// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocTypeLiteral : JsDocType
{
    public JsDocTypeLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTypeLiteral;

    public NodeArray<JsDocPropertyTag> JsDocPropertyTags { get; set; }
    public JsDocTypeTag JsDocTypeTag { get; set; }
}