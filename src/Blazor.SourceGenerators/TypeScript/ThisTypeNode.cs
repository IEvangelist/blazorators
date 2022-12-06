// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ThisTypeNode : TypeNode
{
    public ThisTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.ThisType;
}