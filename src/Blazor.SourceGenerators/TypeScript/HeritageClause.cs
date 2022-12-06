// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class HeritageClause : Node
{
    public HeritageClause() => ((INode)this).Kind = TypeScriptSyntaxKind.HeritageClause;

    public TypeScriptSyntaxKind Token? { get; set; }
    public NodeArray<ExpressionWithTypeArguments> Types? { get; set; }
}