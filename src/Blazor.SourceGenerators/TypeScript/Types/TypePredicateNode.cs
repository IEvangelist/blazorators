// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypePredicateNode : TypeNode
{
    public TypePredicateNode()
    {
        Kind = TypeScriptSyntaxKind.TypePredicate;
    }

    public Node ParameterName { get; set; }
    public ITypeNode Type { get; set; }
}