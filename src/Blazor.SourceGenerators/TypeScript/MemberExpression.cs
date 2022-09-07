// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class MemberExpression : LeftHandSideExpression, IMemberExpression
{
    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
}