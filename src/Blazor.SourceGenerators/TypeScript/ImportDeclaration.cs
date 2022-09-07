// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ImportDeclaration : Statement
{
    internal ImportDeclaration() => ((INode)this).Kind = CommentKind.ImportDeclaration;

    internal ImportClause ImportClause { get; set; }
    internal IExpression ModuleSpecifier { get; set; }
}