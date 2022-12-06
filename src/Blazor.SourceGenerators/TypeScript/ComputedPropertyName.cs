// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ComputedPropertyName : Node, IPropertyName
{
    public ComputedPropertyName() => ((INode)this).Kind = TypeScriptSyntaxKind.ComputedPropertyName;

    public IExpression? Expression { get; set; }
}
