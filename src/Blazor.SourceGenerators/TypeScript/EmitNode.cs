// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EmitNode
{
    public Node[] AnnotatedNodes { get; set; } = Array.Empty<Node>();
    public EmitFlags Flags { get; set; }
    public SynthesizedComment[] LeadingComments { get; set; } = Array.Empty<SynthesizedComment>();
    public SynthesizedComment[] TrailingComments { get; set; } = Array.Empty<SynthesizedComment>();
    public TextRange? CommentRange { get; set; }
    public TextRange? SourceMapRange { get; set; }
    public TextRange[] TokenSourceMapRanges { get; set; } = Array.Empty<Node>();
    public int ConstantValue { get; set; }
    public Identifier? ExternalHelpersModuleName { get; set; }
    public EmitHelper[] Helpers { get; set; } = Array.Empty<EmitHelper>();
}