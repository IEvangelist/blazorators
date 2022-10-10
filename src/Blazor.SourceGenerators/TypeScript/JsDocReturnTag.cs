// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocReturnTag : JsDocTag
{
    internal JsDocReturnTag() => ((INode)this).Kind = SyntaxKind.JsDocReturnTag;

    internal JsDocTypeExpression? TypeExpression { get; set; }
}