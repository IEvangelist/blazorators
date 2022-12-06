// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class SetAccessorDeclaration : Declaration, IFunctionLikeDeclaration, IClassElement, IObjectLiteralElement,
    IAccessorDeclaration
{
    public SetAccessorDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.SetAccessor;

    public object ClassElementBrand { get; set; }
    public object FunctionLikeDeclarationBrand { get; set; }
    public AsteriskToken AsteriskToken { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public IBlockOrExpression Body { get; set; } //  Block | Expression
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object ObjectLiteralBrandBrand { get; set; }
}
