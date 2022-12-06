// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class IncrementExpression : UnaryExpression, IIncrementExpression
{
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
}