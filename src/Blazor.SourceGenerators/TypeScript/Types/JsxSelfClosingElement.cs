// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class JsxSelfClosingElement : PrimaryExpression, IJsxChild
{
    public JsxSelfClosingElement()
    {
        Kind = TypeScriptSyntaxKind.JsxSelfClosingElement;
    }

    public IJsxTagNameExpression TagName { get; set; }
    public JsxAttributes Attributes { get; set; }
}