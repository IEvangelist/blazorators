// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ExportDeclaration : DeclarationStatement
{
    public ExportDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.ExportDeclaration;

    public NamedExports? ExportClause { get; set; }
    public IExpression? ModuleSpecifier { get; set; }
}