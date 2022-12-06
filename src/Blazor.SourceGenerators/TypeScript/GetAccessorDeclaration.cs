// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class GetAccessorDeclaration : Declaration,
    IFunctionLikeDeclaration,
    IClassElement,
    IObjectLiteralElement,
    IAccessorDeclaration
{
    public GetAccessorDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.GetAccessor;

    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand? { get; set; }
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken? { get; set; }
    QuestionToken IFunctionLikeDeclaration.QuestionToken? { get; set; }
    IBlockOrExpression IFunctionLikeDeclaration.Body? { get; set; }
    IBlockOrExpression IAccessorDeclaration.Body? { get; set; }
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IClassElement.ClassElementBrand? { get; set; }
    object IObjectLiteralElement.ObjectLiteralBrandBrand? { get; set; }
}
