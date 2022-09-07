// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface ILiteralExpression : ILiteralLikeNode, IPrimaryExpression
{
    internal object LiteralExpressionBrand { get; set; }
}