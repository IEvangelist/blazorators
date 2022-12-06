// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxClosingElement : Node
{
    public JsxClosingElement() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxClosingElement;

    public IJsxTagNameExpression TagName? { get; set; }
}