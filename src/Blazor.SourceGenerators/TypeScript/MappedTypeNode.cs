// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class MappedTypeNode : Node, ITypeNode, IDeclaration
{
    public MappedTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.MappedType;

    object ITypeNode.TypeNodeBrand? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
}