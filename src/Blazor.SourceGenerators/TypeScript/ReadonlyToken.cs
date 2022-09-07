// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ReadonlyToken : Token
{
    internal ReadonlyToken() => ((INode)this).Kind = CommentKind.ReadonlyKeyword;
}
