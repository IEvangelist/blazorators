// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class CatchClause : Node
{
    internal CatchClause() => ((INode)this).Kind = CommentKind.CatchClause;

    internal VariableDeclaration VariableDeclaration { get; set; } = default!;
    internal Block Block { get; set; } = default!;
}