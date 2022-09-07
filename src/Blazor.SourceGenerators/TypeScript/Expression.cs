// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class Expression : Node, IExpression
{
    object IExpression.ExpressionBrand { get; set; } = default!;
}