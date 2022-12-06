// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class MissingDeclaration : Node,
    IDeclarationStatement,
    IClassElement,
    IObjectLiteralElement,
    ITypeElement
{
    public MissingDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.MissingDeclaration;

    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
    object IStatement.StatementBrand? { get; set; }
    object IClassElement.ClassElementBrand? { get; set; }
    object IObjectLiteralElement.ObjectLiteralBrandBrand? { get; set; }
    object ITypeElement.TypeElementBrand? { get; set; }
    QuestionToken ITypeElement.QuestionToken? { get; set; }
}