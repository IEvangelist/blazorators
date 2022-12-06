// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class LeftHandSideExpression : IncrementExpression, ILeftHandSideExpression
{
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
}