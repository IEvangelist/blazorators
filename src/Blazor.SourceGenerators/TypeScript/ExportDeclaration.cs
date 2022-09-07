// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ExportDeclaration : DeclarationStatement
{
    internal ExportDeclaration() => ((INode)this).Kind = CommentKind.ExportDeclaration;

    internal NamedExports ExportClause { get; set; }
    internal IExpression ModuleSpecifier { get; set; }
}