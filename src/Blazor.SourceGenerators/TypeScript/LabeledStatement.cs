// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class LabeledStatement : Statement
{
    internal LabeledStatement() => ((INode)this).Kind = CommentKind.LabeledStatement;

    internal Identifier Label { get; set; }
    internal IStatement Statement { get; set; }
}