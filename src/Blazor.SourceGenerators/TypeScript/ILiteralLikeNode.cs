// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface ILiteralLikeNode : INode
{
    internal string Text { get; set; }
    internal bool IsUnterminated { get; set; }
    internal bool HasExtendedUnicodeEscape { get; set; }
    internal bool IsOctalLiteral { get; set; }
}