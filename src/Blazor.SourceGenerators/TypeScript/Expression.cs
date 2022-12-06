// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class Expression : Node, IExpression
{
    object IExpression.ExpressionBrand? { get; set; }
}