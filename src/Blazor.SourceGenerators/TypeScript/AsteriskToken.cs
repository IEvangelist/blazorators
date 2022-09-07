// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class AsteriskToken : Token
{
    internal AsteriskToken() => ((INode)this).Kind = CommentKind.AsteriskToken;
}
