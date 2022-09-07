// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class EndOfFileToken : Token
{
    internal EndOfFileToken() => ((INode)this).Kind = CommentKind.EndOfFileToken;
}
