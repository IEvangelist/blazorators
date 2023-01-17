// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocNonNullableType : JsDocType
{
    public JsDocNonNullableType()
    {
        Kind = TypeScriptSyntaxKind.JsDocNonNullableType;
    }

    public IJsDocType Type { get; set; }
}