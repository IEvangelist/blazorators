// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocVariadicType : JsDocType
{
    internal JsDocVariadicType() => ((INode)this).Kind = SyntaxKind.JsDocVariadicType;

    internal IJsDocType Type { get; set; }
}