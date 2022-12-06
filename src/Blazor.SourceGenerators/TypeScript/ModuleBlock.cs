// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ModuleBlock : Block
{
    public ModuleBlock() => ((INode)this).Kind = TypeScriptSyntaxKind.ModuleBlock;
}