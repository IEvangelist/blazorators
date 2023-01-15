// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.TypeScript.Compiler;

public static class Factory
{
    public static INode SkipPartiallyEmittedExpressions(INode node)
    {
        while (node is { Kind: TypeScriptSyntaxKind.PartiallyEmittedExpression } and
            PartiallyEmittedExpression expression)
        {
            node = expression.Expression;
        }

        return node;
    }
}
