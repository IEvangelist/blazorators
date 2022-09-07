// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class CommentRange : TextRange
{
    internal bool HasTrailingNewLine { get; set; }
    internal CommentKind Kind { get; set; }
}