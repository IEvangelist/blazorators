// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypePredicateNode : TypeNode
{
    public TypePredicateNode() => ((INode)this).Kind = TypeScriptSyntaxKind.TypePredicate;

    public Node ParameterName { get; set; } // Identifier | ThisTypeNode
    public ITypeNode Type { get; set; }
}