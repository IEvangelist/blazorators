// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocTypeTag : JsDocTag
{
    public JsDocTypeTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTypeTag;

    public JsDocTypeExpression? TypeExpression { get; set; }
}