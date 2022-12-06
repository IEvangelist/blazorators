// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface ILiteralLikeNode : INode
{
    public string Text { get; set; }
    public bool IsUnterminated { get; set; }
    public bool HasExtendedUnicodeEscape { get; set; }
    public bool IsOctalLiteral { get; set; }
}