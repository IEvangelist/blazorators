// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocReturnTag : JsDocTag
{
    public JsDocReturnTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocReturnTag;

    public JsDocTypeExpression? TypeExpression { get; set; }
}