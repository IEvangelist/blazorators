// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class HeritageClause : Node
{
    public HeritageClause()
    {
        Kind = TypeScriptSyntaxKind.HeritageClause;
    }

    public TypeScriptSyntaxKind Token { get; set; }
    public NodeArray<ExpressionWithTypeArguments> Types { get; set; }
}