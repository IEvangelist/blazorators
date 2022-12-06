// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class FunctionDeclaration : Node, IFunctionLikeDeclaration, IDeclarationStatement
{
    public FunctionDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.FunctionDeclaration;

    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand? { get; set; }
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken? { get; set; }
    QuestionToken IFunctionLikeDeclaration.QuestionToken? { get; set; }
    IBlockOrExpression IFunctionLikeDeclaration.Body? { get; set; }
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
    object IStatement.StatementBrand? { get; set; }
}
