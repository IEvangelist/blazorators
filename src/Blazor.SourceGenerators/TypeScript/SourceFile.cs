// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SourceFile : Declaration, ISourceFileLike
{
    internal SourceFile() => ((INode)this).Kind = SyntaxKind.SourceFile;

    internal NodeArray<IStatement> Statements { get; set; }
    internal Token EndOfFileToken { get; set; }
    internal string FileName { get; set; }
    internal AmdDependency[] AmdDependencies { get; set; } = Array.Empty<AmdDependency>();
    internal string ModuleName { get; set; }
    internal FileReference[] ReferencedFiles { get; set; } = Array.Empty<FileReference>();
    internal FileReference[] TypeReferenceDirectives { get; set; } = Array.Empty<FileReference>();
    internal LanguageVariant LanguageVariant { get; set; }
    internal bool IsDeclarationFile { get; set; }
    internal Map<string> RenamedDependencies { get; set; }
    internal bool HasNoDefaultLib { get; set; }
    internal ScriptTarget LanguageVersion { get; set; }
    internal ScriptKind ScriptKind { get; set; }
    internal INode ExternalModuleIndicator { get; set; }
    internal Node CommonJsModuleIndicator { get; set; }
    internal List<string> Identifiers { get; set; }
    internal int NodeCount { get; set; }
    internal int IdentifierCount { get; set; }
    internal int SymbolCount { get; set; }
    internal List<Diagnostic> ParseDiagnostics { get; set; }
    internal List<Diagnostic> AdditionalSyntacticDiagnostics { get; set; }
    internal List<Diagnostic> BindDiagnostics { get; set; }
    internal Map<string> ClassifiableNames { get; set; }
    internal Map<ResolvedModuleFull> ResolvedModules { get; set; }
    internal Map<ResolvedTypeReferenceDirective> ResolvedTypeReferenceDirectiveNames { get; set; }
    internal LiteralExpression[] Imports { get; set; } = Array.Empty<LiteralExpression>();
    internal LiteralExpression[] ModuleAugmentations { get; set; } = Array.Empty<LiteralExpression>();
    internal PatternAmbientModule[] PatternAmbientModules { get; set; } = Array.Empty<PatternAmbientModule>();
    internal string[] AmbientModuleNames { get; set; } = Array.Empty<string>();
    internal TextRange CheckJsDirective { get; set; }
    string ISourceFileLike.Text { get; set; }
    int[] ISourceFileLike.LineMap { get; set; } = Array.Empty<int>();
}