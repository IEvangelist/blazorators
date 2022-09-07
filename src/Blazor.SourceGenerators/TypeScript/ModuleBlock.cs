// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ModuleBlock : Block
{
    internal ModuleBlock() => ((INode)this).Kind = CommentKind.ModuleBlock;
}