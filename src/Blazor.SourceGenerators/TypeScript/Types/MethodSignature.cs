// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class MethodSignature : Declaration, ISignatureDeclaration, ITypeElement, IFunctionLikeDeclaration
{
    public MethodSignature()
    {
        Kind = TypeScriptSyntaxKind.MethodSignature;
    }

    public object FunctionLikeDeclarationBrand { get; set; }
    public AsteriskToken AsteriskToken { get; set; }
    public IBlockOrExpression Body { get; set; }
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}