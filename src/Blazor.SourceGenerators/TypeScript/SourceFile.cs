// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SourceFile : Declaration, ISourceFileLike
{
    internal SourceFile() => ((INode)this).Kind = CommentKind.SourceFile;

    internal NodeArray<IStatement> Statements { get; set; }
    internal Token EndOfFileToken { get; set; } // Token<SyntaxKind.EndOfFileToken>
    internal string FileName { get; set; }
    internal AmdDependency[] AmdDependencies { get; set; }
    internal string ModuleName { get; set; }
    internal FileReference[] ReferencedFiles { get; set; }
    internal FileReference[] TypeReferenceDirectives { get; set; }
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
    internal LiteralExpression[] Imports { get; set; }
    internal LiteralExpression[] ModuleAugmentations { get; set; }
    internal PatternAmbientModule[] PatternAmbientModules { get; set; }
    internal string[] AmbientModuleNames { get; set; }
    internal TextRange CheckJsDirective { get; set; } // CheckJsDirective
    internal string Text { get; set; }
    internal int[] LineMap { get; set; }
}