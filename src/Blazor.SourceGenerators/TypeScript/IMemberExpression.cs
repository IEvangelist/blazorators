// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IMemberExpression : ILeftHandSideExpression
{
    internal object MemberExpressionBrand { get; set; }
}