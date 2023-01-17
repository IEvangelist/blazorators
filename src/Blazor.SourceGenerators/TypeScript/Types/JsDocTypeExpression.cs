// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTypeExpression : Node
{
    public JsDocTypeExpression()
    {
        Kind = TypeScriptSyntaxKind.JsDocTypeExpression;
    }

    public IJsDocType Type { get; set; }
}