// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface IIncrementExpression : IUnaryExpression
{
    internal object IncrementExpressionBrand { get; set; }
}