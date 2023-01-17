// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class SetAccessorDeclaration : Declaration, IFunctionLikeDeclaration, IClassElement,
    IObjectLiteralElement,
    IAccessorDeclaration
{
    public SetAccessorDeclaration()
    {
        Kind = TypeScriptSyntaxKind.SetAccessor;
    }

    public object ClassElementBrand { get; set; }
    public object FunctionLikeDeclarationBrand { get; set; }
    public AsteriskToken AsteriskToken { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public IBlockOrExpression Body { get; set; }
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object ObjectLiteralBrandBrand { get; set; }
}