// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class VariableDeclarationList : Node, IVariableDeclarationList
{
    internal VariableDeclarationList() => ((INode)this).Kind = CommentKind.VariableDeclarationList;

    internal NodeArray<VariableDeclaration> Declarations { get; set; }
}
