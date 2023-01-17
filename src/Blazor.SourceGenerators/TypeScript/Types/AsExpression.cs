// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class AsExpression : Expression
{
    public AsExpression()
    {
        Kind = TypeScriptSyntaxKind.AsExpression;
    }

    public IExpression Expression { get; set; }
    public ITypeNode Type { get; set; }
}