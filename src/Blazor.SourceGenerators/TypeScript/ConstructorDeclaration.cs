// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ConstructorDeclaration : Declaration, IFunctionLikeDeclaration, IClassElement
{
    public ConstructorDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.Constructor;

    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand? { get; set; }
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken? { get; set; }
    QuestionToken IFunctionLikeDeclaration.QuestionToken? { get; set; }
    IBlockOrExpression IFunctionLikeDeclaration.Body? { get; set; }
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IClassElement.ClassElementBrand? { get; set; }
}
