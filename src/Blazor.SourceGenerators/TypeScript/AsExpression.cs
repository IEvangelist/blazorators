// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class AsExpression : Expression
{
    internal AsExpression() => ((INode)this).Kind = SyntaxKind.AsExpression;

    internal IExpression Expression { get; set; } = default!;
    internal ITypeNode Type { get; set; } = default!;
}