// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using TsParser = Blazor.SourceGenerators.TypeScript.Parser.Parser;

namespace Blazor.SourceGenerators.TypeScript;

internal sealed class SourceAbstractSyntaxTree : ISourceAbstractSyntaxTree
{
    private bool _childrenMade = false;

    private ISourceAbstractSyntaxTree Ast => this;

    ScriptTarget ISourceAbstractSyntaxTree.ScriptTarget { get; set; } = ScriptTarget.Latest;
    string ISourceAbstractSyntaxTree.SourceStr { get; set; } = default!;
    Node ISourceAbstractSyntaxTree.RootNode { get; set; } = default!;

    async Task ISourceAbstractSyntaxTree.LoadAbstractSyntaxTreeAsync(
        string source, string fileName, bool setChildren)
    {
        Ast.SourceStr = source;
        var parser = new TsParser();
        var sourceFile =
            await parser.ParseSourceFileAsync(
                fileName,
                source,
                Ast.ScriptTarget,
                null,
                false,
                ScriptKind.Ts);
        Ast.RootNode = sourceFile;
        Ast.RootNode.AbstractSyntaxTree = this;

        if (setChildren)
        {
            _childrenMade = true;
            Ast.RootNode.MakeChildren(this);
        }
    }

    internal IEnumerable<Node> OfKind(SyntaxKind kind) =>
        Ast.RootNode?.OfKind(kind) ?? Enumerable.Empty<Node>();

    IEnumerable<Node> ISourceAbstractSyntaxTree.GetDescendants()
    {
        if (!_childrenMade && Ast.RootNode != null)
        {
            Ast.RootNode.MakeChildren(this);
            _childrenMade = true;
        }

        return Ast.RootNode?.GetDescendants() ?? Enumerable.Empty<Node>();
    }

    string ISourceAbstractSyntaxTree.GetTreeString(bool withPos) =>
        ((INode)Ast.RootNode)?.GetTreeString(withPos) ?? "";
}