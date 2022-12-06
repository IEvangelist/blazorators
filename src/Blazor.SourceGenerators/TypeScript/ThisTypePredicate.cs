// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ThisTypePredicate : TypePredicateBase
{
    public ThisTypePredicate() => ((INode)this).Kind = TypePredicateKind.This;
}