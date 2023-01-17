// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxElement : PrimaryExpression, IJsxChild
{
    public JsxElement()
    {
        Kind = TypeScriptSyntaxKind.JsxElement;
    }

    public IExpression OpeningElement { get; set; }
    public NodeArray<IJsxChild> JsxChildren { get; set; }
    public JsxClosingElement ClosingElement { get; set; }
}