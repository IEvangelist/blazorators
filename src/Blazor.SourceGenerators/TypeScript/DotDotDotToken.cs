// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class DotDotDotToken : Token
{
    internal DotDotDotToken() => ((INode)this).Kind = CommentKind.DotDotDotToken;
}
