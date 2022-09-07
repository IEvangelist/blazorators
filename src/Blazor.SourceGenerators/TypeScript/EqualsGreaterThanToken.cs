// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class EqualsGreaterThanToken : Token
{
    internal EqualsGreaterThanToken() => ((INode)this).Kind = CommentKind.EqualsGreaterThanToken;
}
