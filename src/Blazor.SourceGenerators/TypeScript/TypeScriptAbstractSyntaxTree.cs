// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Compiler;
using Blazor.SourceGenerators.TypeScript.Types;

#nullable disable
namespace Blazor.SourceGenerators.TypeScript;

internal sealed class TypeScriptAbstractSyntaxTree : ITypeScriptAbstractSyntaxTree
{
    private readonly ScriptTarget _languageVersion;

    public string RawSourceText { get; set; }
    public RootNodeSourceFile RootNode { get; set; }

    public TypeScriptAbstractSyntaxTree(
        string source = null,
        string fileName = "app.ts",
        ScriptTarget languageVersion = ScriptTarget.Latest)
    {
        _languageVersion = languageVersion;
        if (source is not null)
        {
            ParseAsAst(source, fileName);
        }
    }

    public void ParseAsAst(string source, string fileName = "app.ts")
    {
        RawSourceText = source;
        var parser = new Parser();
        RootNode = parser.ParseSourceFile(
            fileName,
            source,
            _languageVersion,
            true,
            ScriptKind.Ts);
        RootNode.AbstractSyntaxTree = this;
        RootNode.ParseChildren(this);
    }
}
