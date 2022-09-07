// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class MissingDeclaration : Node,
    IDeclarationStatement,
    IClassElement,
    IObjectLiteralElement,
    ITypeElement
{
    internal MissingDeclaration() => ((INode)this).Kind = CommentKind.MissingDeclaration;

    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
    object IStatement.StatementBrand { get; set; } = default!;
    object IClassElement.ClassElementBrand { get; set; } = default!;
    object IObjectLiteralElement.ObjectLiteralBrandBrand { get; set; } = default!;
    object ITypeElement.TypeElementBrand { get; set; } = default!;
    QuestionToken ITypeElement.QuestionToken { get; set; } = default!;
}