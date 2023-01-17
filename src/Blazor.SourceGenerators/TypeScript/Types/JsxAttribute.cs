// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxAttribute : ObjectLiteralElement
{
    public JsxAttribute()
    {
        Kind = TypeScriptSyntaxKind.JsxAttribute;
    }

    public Node Initializer { get; set; }
}