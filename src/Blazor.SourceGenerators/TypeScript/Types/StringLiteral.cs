// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class StringLiteral : LiteralExpression, IPropertyName
{
    public StringLiteral()
    {
        Kind = TypeScriptSyntaxKind.StringLiteral;
    }

    public Node TextSourceNode { get; set; }
}