// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Compiler;

// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class SourceFile : Declaration, ISourceFileLike
{
    public SourceFile()
    {
        Kind = TypeScriptSyntaxKind.SourceFile;
    }

    public NodeArray<IStatement> Statements { get; set; }
    public Token EndOfFileToken { get; set; }
    public string FileName { get; set; }
    public AmdDependency[] AmdDependencies { get; set; }
    public string ModuleName { get; set; }
    public FileReference[] ReferencedFiles { get; set; }
    public FileReference[] TypeReferenceDirectives { get; set; }
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
    public LiteralExpression[] Imports { get; set; }
    public LiteralExpression[] ModuleAugmentations { get; set; }
    public PatternAmbientModule[] PatternAmbientModules { get; set; }
    public string[] AmbientModuleNames { get; set; }
    public TextRange CheckJsDirective { get; set; }
    public string Text { get; set; }
    public int[] LineMap { get; set; }
}