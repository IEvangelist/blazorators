// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocOptionalType : JsDocType
{
    internal JsDocOptionalType() => ((INode)this).Kind = SyntaxKind.JsDocOptionalType;

    internal IJsDocType Type { get; set; }
}