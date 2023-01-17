// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class CompilerOptions
{
    public bool All { get; set; }
    public bool AllowJs { get; set; }
    public bool AllowNonTsExtensions { get; set; }
    public bool AllowSyntheticDefaultImports { get; set; }
    public bool AllowUnreachableCode { get; set; }
    public bool AllowUnusedLabels { get; set; }
    public bool AlwaysStrict { get; set; }
    public string BaseUrl { get; set; }
    public string Charset { get; set; }
    public bool CheckJs { get; set; }
    public string ConfigFilePath { get; set; }
    public bool Declaration { get; set; }
    public string DeclarationDir { get; set; }
    public bool Diagnostics { get; set; }
    public bool ExtendedDiagnostics { get; set; }
    public bool DisableSizeLimit { get; set; }
    public bool DownlevelIteration { get; set; }
    public bool EmitBom { get; set; }
    public bool EmitDecoratorMetadata { get; set; }
    public bool ExperimentalDecorators { get; set; }
    public bool ForceConsistentCasingInFileNames { get; set; }
    public bool Help { get; set; }
    public bool ImportHelpers { get; set; }
    public bool Init { get; set; }
    public bool InlineSourceMap { get; set; }
    public bool InlineSources { get; set; }
    public bool IsolatedModules { get; set; }
    public JsxEmit Jsx { get; set; }
    public string[] Lib { get; set; }
    public bool ListEmittedFiles { get; set; }
    public bool ListFiles { get; set; }
    public string Locale { get; set; }
    public string MapRoot { get; set; }
    public int MaxNodeModuleJsDepth { get; set; }
    public ModuleKind Module { get; set; }
    public ModuleResolutionKind ModuleResolution { get; set; }
    public NewLineKind NewLine { get; set; }
    public bool NoEmit { get; set; }
    public bool NoEmitForJsFiles { get; set; }
    public bool NoEmitHelpers { get; set; }
    public bool NoEmitOnError { get; set; }
    public bool NoErrorTruncation { get; set; }
    public bool NoFallthroughCasesInSwitch { get; set; }
    public bool NoImplicitAny { get; set; }
    public bool NoImplicitReturns { get; set; }
    public bool NoImplicitThis { get; set; }
    public bool NoUnusedLocals { get; set; }
    public bool NoUnusedParameters { get; set; }
    public bool NoImplicitUseStrict { get; set; }
    public bool NoLib { get; set; }
    public bool NoResolve { get; set; }
    public string Out { get; set; }
    public string OutDir { get; set; }
    public string OutFile { get; set; }
    public Map<string[]> Paths { get; set; }
    public PluginImport[] Plugins { get; set; }
    public bool PreserveConstEnums { get; set; }
    public string Project { get; set; }
    public DiagnosticStyle Pretty { get; set; }
    public string ReactNamespace { get; set; }
    public string JsxFactory { get; set; }
    public bool RemoveComments { get; set; }
    public string RootDir { get; set; }
    public string[] RootDirs { get; set; }
    public bool SkipLibCheck { get; set; }
    public bool SkipDefaultLibCheck { get; set; }
    public bool SourceMap { get; set; }
    public string SourceRoot { get; set; }
    public bool Strict { get; set; }
    public bool StrictNullChecks { get; set; }
    public bool StripInternal { get; set; }
    public bool SuppressExcessPropertyErrors { get; set; }
    public bool SuppressImplicitAnyIndexErrors { get; set; }
    public bool SuppressOutputPathCheck { get; set; }
    public ScriptTarget Target { get; set; }
    public bool TraceResolution { get; set; }
    public string[] Types { get; set; }
    public string[] TypeRoots { get; set; }
    public bool Version { get; set; }
    public bool Watch { get; set; }
}