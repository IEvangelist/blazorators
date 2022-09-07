// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EndOfDeclarationMarker : Statement
{
    internal EndOfDeclarationMarker() => ((INode)this).Kind = CommentKind.EndOfDeclarationMarker;
}