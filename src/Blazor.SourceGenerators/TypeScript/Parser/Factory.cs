// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript.Parser;

internal static class Factory
{
    internal static INode SkipPartiallyEmittedExpressions(INode node)
    {
        while (node is { Kind: SyntaxKind.PartiallyEmittedExpression })
        {
            node = ((PartiallyEmittedExpression)node).Expression;
        }

        return node;
    }
}