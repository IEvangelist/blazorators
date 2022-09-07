// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class MergeDeclarationMarker : Statement
{
    internal MergeDeclarationMarker() => ((INode)this).Kind = CommentKind.MergeDeclarationMarker;
}