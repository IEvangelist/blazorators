// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeReferenceNode : TypeNode
{
    public TypeReferenceNode()
    {
        Kind = TypeScriptSyntaxKind.TypeReference;
    }

    public IEntityName TypeName { get; set; }
    public NodeArray<ITypeNode> TypeArguments { get; set; }
}