// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxSpreadAttribute : ObjectLiteralElement
{
    internal JsxSpreadAttribute() => ((INode)this).Kind = SyntaxKind.JsxSpreadAttribute;

    internal IExpression Expression { get; set; }
}