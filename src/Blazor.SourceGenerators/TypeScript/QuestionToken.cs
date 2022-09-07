// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class QuestionToken : Token
{
    internal QuestionToken() => ((INode)this).Kind = CommentKind.QuestionToken;
}
