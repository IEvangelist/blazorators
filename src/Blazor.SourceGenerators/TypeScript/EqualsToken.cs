// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class EqualsToken : Token
{
    public EqualsToken() => ((INode)this).Kind = SyntaxKind.EqualsToken;
}
