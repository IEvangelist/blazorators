// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxElement : PrimaryExpression, IJsxChild
{
    public JsxElement() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxElement;

    public IExpression OpeningElement? { get; set; }
    public NodeArray<IJsxChild> JsxChildren? { get; set; }
    public JsxClosingElement ClosingElement? { get; set; }
}