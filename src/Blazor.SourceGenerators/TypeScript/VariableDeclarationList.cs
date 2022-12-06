// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class VariableDeclarationList : Node, IVariableDeclarationList
{
    public VariableDeclarationList() => ((INode)this).Kind = TypeScriptSyntaxKind.VariableDeclarationList;

    public NodeArray<VariableDeclaration> Declarations { get; set; }
}
