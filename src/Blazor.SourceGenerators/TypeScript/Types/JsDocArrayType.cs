// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocArrayType : JsDocType
{
    public JsDocArrayType()
    {
        Kind = TypeScriptSyntaxKind.JsDocArrayType;
    }

    public IJsDocType ElementType { get; set; }
}