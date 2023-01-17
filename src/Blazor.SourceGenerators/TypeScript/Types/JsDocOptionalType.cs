// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocOptionalType : JsDocType
{
    public JsDocOptionalType()
    {
        Kind = TypeScriptSyntaxKind.JsDocOptionalType;
    }

    public IJsDocType Type { get; set; }
}