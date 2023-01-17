// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocUnionType : JsDocType
{
    public JsDocUnionType()
    {
        Kind = TypeScriptSyntaxKind.JsDocUnionType;
    }

    public NodeArray<IJsDocType> Types { get; set; }
}