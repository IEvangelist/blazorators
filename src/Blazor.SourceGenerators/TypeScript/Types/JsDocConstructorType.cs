// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocConstructorType : JsDocType
{
    public JsDocConstructorType()
    {
        Kind = TypeScriptSyntaxKind.JsDocConstructorType;
    }

    public IJsDocType Type { get; set; }
}