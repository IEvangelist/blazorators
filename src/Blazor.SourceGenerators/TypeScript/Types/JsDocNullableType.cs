// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocNullableType : JsDocType
{
    public JsDocNullableType()
    {
        Kind = TypeScriptSyntaxKind.JsDocNullableType;
    }

    public IJsDocType Type { get; set; }
}