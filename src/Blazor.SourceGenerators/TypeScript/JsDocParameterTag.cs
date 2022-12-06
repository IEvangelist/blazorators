// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocParameterTag : JsDocTag
{
    public JsDocParameterTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocParameterTag;

    public Identifier? PreParameterName { get; set; }
    public JsDocTypeExpression? TypeExpression { get; set; }
    public Identifier? PostParameterName { get; set; }
    public Identifier? ParameterName { get; set; }
    public bool IsBracketed { get; set; }
}