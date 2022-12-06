// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SourceFile : Declaration, ISourceFileLike
{
    public SourceFile() => ((INode)this).Kind = TypeScriptSyntaxKind.SourceFile;

    public NodeArray<IStatement> Statements { get; set; }
    public Token EndOfFileToken { get; set; }
    public string FileName { get; set; }
    public AmdDependency[] AmdDependencies { get; set; } = Array.Empty<AmdDependency>();
    public string ModuleName { get; set; }
    public FileReference[] ReferencedFiles { get; set; } = Array.Empty<FileReference>();
    public FileReference[] TypeReferenceDirectives { get; set; } = Array.Empty<FileReference>();
    public LanguageVariant LanguageVariant { get; set; }
    public bool IsDeclarationFile { get; set; }
    public Map<string> RenamedDependencies { get; set; }
    public bool HasNoDefaultLib { get; set; }
    public ScriptTarget LanguageVersion { get; set; }
    public ScriptKind ScriptKind { get; set; }
    public INode ExternalModuleIndicator { get; set; }
    public Node CommonJsModuleIndicator { get; set; }
    public List<string> Identifiers { get; set; }
    public int NodeCount { get; set; }
    public int IdentifierCount { get; set; }
    public int SymbolCount { get; set; }
    public List<TypeScriptDiagnostic> ParseDiagnostics { get; set; }
    public List<TypeScriptDiagnostic> AdditionalSyntacticDiagnostics { get; set; }
    public List<TypeScriptDiagnostic> BindDiagnostics { get; set; }
    public Map<string> ClassifiableNames { get; set; }
    public Map<ResolvedModuleFull> ResolvedModules { get; set; }
    public Map<ResolvedTypeReferenceDirective> ResolvedTypeReferenceDirectiveNames { get; set; }
    public LiteralExpression[] Imports { get; set; } = Array.Empty<LiteralExpression>();
    public LiteralExpression[] ModuleAugmentations { get; set; } = Array.Empty<LiteralExpression>();
    public PatternAmbientModule[] PatternAmbientModules { get; set; } = Array.Empty<PatternAmbientModule>();
    public string[] AmbientModuleNames { get; set; } = Array.Empty<string>();
    public TextRange CheckJsDirective { get; set; }
    string ISourceFileLike.Text { get; set; }
    int[] ISourceFileLike.LineMap { get; set; } = Array.Empty<int>();
}