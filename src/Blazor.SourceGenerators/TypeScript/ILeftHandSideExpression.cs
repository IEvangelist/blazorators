// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface ILeftHandSideExpression : IIncrementExpression
{
    public object LeftHandSideExpressionBrand { get; set; }
}