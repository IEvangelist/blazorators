// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxAttributes : ObjectLiteralExpressionBase<ObjectLiteralElement> // JsxAttributeLike>
{
    public JsxAttributes()
    {
        Kind = TypeScriptSyntaxKind.JsxAttributes;
    }
}