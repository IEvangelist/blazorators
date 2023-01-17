// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EmitNode
{
    public Node[] AnnotatedNodes { get; set; }
    public EmitFlags Flags { get; set; }
    public SynthesizedComment[] LeadingComments { get; set; }
    public SynthesizedComment[] TrailingComments { get; set; }
    public TextRange CommentRange { get; set; }
    public TextRange SourceMapRange { get; set; }
    public TextRange[] TokenSourceMapRanges { get; set; }
    public int ConstantValue { get; set; }
    public Identifier ExternalHelpersModuleName { get; set; }
    public EmitHelper[] Helpers { get; set; }
}