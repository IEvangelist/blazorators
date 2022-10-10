// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface IExpression : IBlockOrExpression, IVariableDeclarationListOrExpression
{
    internal object ExpressionBrand { get; set; }
}