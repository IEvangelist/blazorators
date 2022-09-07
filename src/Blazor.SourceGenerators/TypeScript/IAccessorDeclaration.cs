// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IAccessorDeclaration :
    ISignatureDeclaration,
    IClassElement,
    IObjectLiteralElementLike
{
    internal IBlockOrExpression Body { get; set; }
}
