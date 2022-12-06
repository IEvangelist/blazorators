// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class LabeledStatement : Statement
{
    public LabeledStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.LabeledStatement;

    public Identifier Label { get; set; }
    public IStatement Statement { get; set; }
}