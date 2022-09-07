// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ParsedCommandLine
{
    internal CompilerOptions Options { get; set; }
    internal TypeAcquisition TypeAcquisition { get; set; }
    internal string[] FileNames { get; set; }
    internal object Raw { get; set; }
    internal Diagnostic[] Errors { get; set; }
    internal MapLike<WatchDirectoryFlags> WildcardDirectories { get; set; }
    internal bool CompileOnSave { get; set; }
}