// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxSpreadAttribute : ObjectLiteralElement
{
    public JsxSpreadAttribute()
    {
        Kind = TypeScriptSyntaxKind.JsxSpreadAttribute;
    }

    public IExpression Expression { get; set; }
}