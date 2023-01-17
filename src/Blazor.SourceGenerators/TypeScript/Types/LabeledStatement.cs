// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class LabeledStatement : Statement
{
    public LabeledStatement()
    {
        Kind = TypeScriptSyntaxKind.LabeledStatement;
    }

    public Identifier Label { get; set; }
    public IStatement Statement { get; set; }
}