// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ExternalModuleReference : Node
{
    internal ExternalModuleReference() => ((INode)this).Kind = CommentKind.ExternalModuleReference;

    internal IExpression Expression { get; set; }
}