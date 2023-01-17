// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ExpressionWithTypeArguments : TypeNode
{
    public ExpressionWithTypeArguments()
    {
        Kind = TypeScriptSyntaxKind.ExpressionWithTypeArguments;
    }

    public IExpression Expression { get; set; }
    public NodeArray<ITypeNode> TypeArguments { get; set; }
}