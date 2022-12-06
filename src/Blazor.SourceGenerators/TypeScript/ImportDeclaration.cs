// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ImportDeclaration : Statement
{
    public ImportDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.ImportDeclaration;

    public ImportClause ImportClause { get; set; }
    public IExpression ModuleSpecifier { get; set; }
}