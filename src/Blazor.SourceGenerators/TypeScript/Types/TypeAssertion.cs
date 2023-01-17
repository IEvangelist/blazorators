// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeAssertion : UnaryExpression
{
    public TypeAssertion()
    {
        Kind = TypeScriptSyntaxKind.TypeAssertionExpression;
    }

    public ITypeNode Type { get; set; }
    public IExpression Expression { get; set; }
}