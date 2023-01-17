// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IAccessorDeclaration : ISignatureDeclaration, IClassElement, IObjectLiteralElementLike
{
    IBlockOrExpression Body { get; set; }
}