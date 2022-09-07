// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class QualifiedName : Node, IEntityName
{
    internal QualifiedName() => ((INode)this).Kind = CommentKind.QualifiedName;

    internal IEntityName Left { get; set; }
    internal Identifier Right { get; set; }
}
