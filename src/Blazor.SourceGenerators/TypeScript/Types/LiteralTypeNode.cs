// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class LiteralTypeNode : TypeNode
{
    public LiteralTypeNode()
    {
        Kind = TypeScriptSyntaxKind.LiteralType;
    }

    public IExpression Literal { get; set; }
}