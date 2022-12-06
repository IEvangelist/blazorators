// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class LiteralLikeNode : Node, ILiteralLikeNode
{
    string ILiteralLikeNode.Text? { get; set; }
    bool ILiteralLikeNode.IsUnterminated { get; set; }
    bool ILiteralLikeNode.HasExtendedUnicodeEscape { get; set; }
    bool ILiteralLikeNode.IsOctalLiteral { get; set; }
}