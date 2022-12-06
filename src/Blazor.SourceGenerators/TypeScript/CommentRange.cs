// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class CommentRange : TextRange
{
    public bool HasTrailingNewLine { get; set; }
    public TypeScriptSyntaxKind Kind { get; set; }
}