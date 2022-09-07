// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IPrimaryExpression : IMemberExpression
{
    internal object PrimaryExpressionBrand { get; set; }
}