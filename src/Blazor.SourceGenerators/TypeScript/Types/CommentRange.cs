// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class CommentRange : TextRange
{
    public bool HasTrailingNewLine { get; set; }
    public TypeScriptSyntaxKind Kind { get; set; }
}