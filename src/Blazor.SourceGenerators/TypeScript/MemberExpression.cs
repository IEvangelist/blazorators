// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class MemberExpression : LeftHandSideExpression, IMemberExpression
{
    object IMemberExpression.MemberExpressionBrand? { get; set; }
}