// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Compiler;

// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ParsedCommandLine
{
    public CompilerOptions Options { get; set; }
    public TypeAcquisition TypeAcquisition { get; set; }
    public string[] FileNames { get; set; }
    public object Raw { get; set; }
    public TypeScriptDiagnostic[] Errors { get; set; }
    public Map<WatchDirectoryFlags> WildcardDirectories { get; set; }
    public bool CompileOnSave { get; set; }
}