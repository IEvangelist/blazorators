// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ExternalModuleReference : Node
{
    internal ExternalModuleReference() => ((INode)this).Kind = SyntaxKind.ExternalModuleReference;

    internal IExpression Expression { get; set; }
}