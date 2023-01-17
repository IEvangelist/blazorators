// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ElementAccessExpression : MemberExpression
{
    public ElementAccessExpression()
    {
        Kind = TypeScriptSyntaxKind.ElementAccessExpression;
    }

    public IExpression Expression { get; set; }
    public IExpression ArgumentExpression { get; set; }
}