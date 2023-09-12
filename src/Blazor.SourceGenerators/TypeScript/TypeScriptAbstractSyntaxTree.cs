// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Compiler;
using Blazor.SourceGenerators.TypeScript.Types;

#nullable disable
namespace Blazor.SourceGenerators.TypeScript;

internal sealed class TypeScriptAbstractSyntaxTree : ITypeScriptAbstractSyntaxTree
{
    public ScriptTarget Target { get; }

    public string RawSourceText { get; }

    public RootNodeSourceFile RootNode { get; }

    private TypeScriptAbstractSyntaxTree(
        string sourceText = null,
        string fileName = "app.ts",
        ScriptTarget target = ScriptTarget.Latest)
    {
        Target = target;

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        RawSourceText = sourceText;
        var parser = new Parser();
        RootNode = parser.ParseSourceFile(
            fileName,
            sourceText,
            Target,
            true,
            ScriptKind.Ts);
        RootNode.AbstractSyntaxTree = this;
        RootNode.ParseChildren(this);
    }

    /// <summary>
    /// Gets a representation of the TypeScript abstract syntax tree from the given
    /// <paramref name="sourceText"/> and <paramref name="fileName"/>.
    /// </summary>
    /// <param name="sourceText">
    /// The source text to parse the abstract syntax tree from.
    /// </param>
    /// <param name="fileName">
    /// The name of the file to parse the abstract syntax tree from.
    /// </param>
    /// <param name="target">
    /// The target script version to parse the abstract syntax tree from.
    /// </param>
    /// <returns>An instance of <see cref="ITypeScriptAbstractSyntaxTree"/> instance.</returns>
    public static ITypeScriptAbstractSyntaxTree FromSourceText(
        string sourceText,
        string fileName = "lib.dom.d.ts",
        ScriptTarget target = ScriptTarget.Latest) =>
        new TypeScriptAbstractSyntaxTree(sourceText, fileName, target);
}
