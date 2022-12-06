// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocUnionType : JsDocType
{
    public JsDocUnionType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocUnionType;

    public NodeArray<IJsDocType> Types { get; set; }
}