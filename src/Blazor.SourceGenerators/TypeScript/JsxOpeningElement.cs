// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxOpeningElement : JsxSelfClosingElement
{
    public JsxOpeningElement() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxOpeningElement;
}