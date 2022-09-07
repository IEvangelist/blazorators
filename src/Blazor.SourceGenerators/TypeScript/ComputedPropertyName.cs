// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ComputedPropertyName : Node, IPropertyName
{
    internal ComputedPropertyName() => ((INode)this).Kind = CommentKind.ComputedPropertyName;

    internal IExpression Expression { get; set; }
}
