// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ParsedCommandLine
{
    public CompilerOptions? Options { get; set; }
    public TypeAcquisition? TypeAcquisition { get; set; }
    public string[]? FileNames { get; set; }
    public object? Raw { get; set; }
    public TypeScriptDiagnostic[]? Errors { get; set; }
    public MapLike<WatchDirectoryFlags>? WildcardDirectories { get; set; }
    public bool CompileOnSave { get; set; }
}