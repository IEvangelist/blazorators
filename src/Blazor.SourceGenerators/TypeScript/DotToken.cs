// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class DotToken : Token
{
    internal DotToken() => ((INode)this).Kind = CommentKind.DotToken;
}
