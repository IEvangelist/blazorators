// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class UnaryExpression : Expression, IUnaryExpression
{
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
}