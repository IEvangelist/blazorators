// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ThisTypePredicate : TypePredicateBase
{
    internal ThisTypePredicate() => ((INode)this).Kind = TypePredicateKind.This;
}