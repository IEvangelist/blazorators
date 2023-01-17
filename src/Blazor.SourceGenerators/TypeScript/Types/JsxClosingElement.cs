// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxClosingElement : Node
{
    public JsxClosingElement()
    {
        Kind = TypeScriptSyntaxKind.JsxClosingElement;
    }

    public IJsxTagNameExpression TagName { get; set; }
}