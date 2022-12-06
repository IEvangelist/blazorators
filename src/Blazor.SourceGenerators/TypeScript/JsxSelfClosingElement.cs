// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxSelfClosingElement : PrimaryExpression, IJsxChild
{
    public JsxSelfClosingElement() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxSelfClosingElement;

    public IJsxTagNameExpression TagName { get; set; }
    public JsxAttributes Attributes { get; set; }
}