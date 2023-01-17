// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ParenthesizedTypeNode : TypeNode
{
    public ParenthesizedTypeNode()
    {
        Kind = TypeScriptSyntaxKind.ParenthesizedType;
    }

    public ITypeNode Type { get; set; }
}