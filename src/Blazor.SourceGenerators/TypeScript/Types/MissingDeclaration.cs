// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class MissingDeclaration : Node, IDeclarationStatement, IClassElement, IObjectLiteralElement, ITypeElement
{
    public MissingDeclaration()
    {
        Kind = TypeScriptSyntaxKind.MissingDeclaration;
    }

    public object ClassElementBrand { get; set; }
    public INode Name { get; set; }
    public object DeclarationBrand { get; set; }
    public object StatementBrand { get; set; }
    public object ObjectLiteralBrandBrand { get; set; }
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}