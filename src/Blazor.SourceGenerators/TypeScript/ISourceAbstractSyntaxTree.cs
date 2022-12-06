// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface ISourceAbstractSyntaxTree
{
    public ScriptTarget ScriptTarget { get; set; }

    public string SourceStr { get; set; }

    public Node RootNode { get; set; }

    public string GetTreeString(bool withPos);

    public IEnumerable<Node> GetDescendants();

    public Task LoadAbstractSyntaxTreeAsync(string source, string fileName, bool setChildren = true);
}
