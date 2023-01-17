// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ComputedPropertyName : Node, IPropertyName
{
    public ComputedPropertyName()
    {
        Kind = TypeScriptSyntaxKind.ComputedPropertyName;
    }

    public IExpression Expression { get; set; }
}