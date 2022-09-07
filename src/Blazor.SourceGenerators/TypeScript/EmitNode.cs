// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EmitNode
{
    internal Node[] AnnotatedNodes { get; set; }
    internal EmitFlags Flags { get; set; }
    internal SynthesizedComment[] LeadingComments { get; set; }
    internal SynthesizedComment[] TrailingComments { get; set; }
    internal TextRange CommentRange { get; set; }
    internal TextRange SourceMapRange { get; set; }
    internal TextRange[] TokenSourceMapRanges { get; set; }
    internal int ConstantValue { get; set; }
    internal Identifier ExternalHelpersModuleName { get; set; }
    internal EmitHelper[] Helpers { get; set; }
}