// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeOperatorNode : ParenthesizedTypeNode
{
    public TypeOperatorNode()
    {
        Kind = TypeScriptSyntaxKind.TypeOperator;
    }

    public TypeScriptSyntaxKind Operator { get; set; } = TypeScriptSyntaxKind.KeyOfKeyword;
}