// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class VariableStatement : Statement
{
    public VariableStatement()
    {
        Kind = TypeScriptSyntaxKind.VariableStatement;
    }

    public IVariableDeclarationList DeclarationList { get; set; }
}