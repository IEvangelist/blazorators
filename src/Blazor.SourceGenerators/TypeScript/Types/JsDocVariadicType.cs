// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocVariadicType : JsDocType
{
    public JsDocVariadicType()
    {
        Kind = TypeScriptSyntaxKind.JsDocVariadicType;
    }

    public IJsDocType Type { get; set; }
}