// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EndOfDeclarationMarker : Statement
{
    public EndOfDeclarationMarker() => ((INode)this).Kind = TypeScriptSyntaxKind.EndOfDeclarationMarker;
}