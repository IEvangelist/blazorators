// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ImportEqualsDeclaration : DeclarationStatement
{
    internal ImportEqualsDeclaration() => ((INode)this).Kind = CommentKind.ImportEqualsDeclaration;

    internal INode ModuleReference { get; set; } = default!;
}