// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeOperatorNode : ParenthesizedTypeNode
{
    public TypeOperatorNode() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeOperator;

    public TypeScriptSyntaxKind Operator { get; set; } = TypeScriptSyntaxKind.KeyOfKeyword;
}