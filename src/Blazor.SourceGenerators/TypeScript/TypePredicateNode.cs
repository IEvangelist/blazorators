// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypePredicateNode : TypeNode
{
    internal TypePredicateNode() => ((INode)this).Kind = CommentKind.TypePredicate;

    internal Node ParameterName { get; set; } // Identifier | ThisTypeNode
    internal ITypeNode Type { get; set; }
}