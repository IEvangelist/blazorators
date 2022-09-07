// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class SetAccessorDeclaration : Declaration, IFunctionLikeDeclaration, IClassElement, IObjectLiteralElement,
    IAccessorDeclaration
{
    internal SetAccessorDeclaration() => ((INode)this).Kind = CommentKind.SetAccessor;

    internal object ClassElementBrand { get; set; }
    internal object FunctionLikeDeclarationBrand { get; set; }
    internal AsteriskToken AsteriskToken { get; set; }
    internal QuestionToken QuestionToken { get; set; }
    internal IBlockOrExpression Body { get; set; } //  Block | Expression
    internal NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    internal NodeArray<ParameterDeclaration> Parameters { get; set; }
    internal ITypeNode Type { get; set; }
    internal object ObjectLiteralBrandBrand { get; set; }
}
