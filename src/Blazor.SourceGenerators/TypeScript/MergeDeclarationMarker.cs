// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class MergeDeclarationMarker : Statement
{
    public MergeDeclarationMarker() => ((INode)this).Kind = TypeScriptSyntaxKind.MergeDeclarationMarker;
}