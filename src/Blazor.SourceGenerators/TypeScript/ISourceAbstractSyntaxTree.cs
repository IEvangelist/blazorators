// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface ISourceAbstractSyntaxTree
{
    internal ScriptTarget ScriptTarget { get; set; }

    internal string SourceStr { get; set; }

    internal Node RootNode { get; set; }

    internal string GetTreeString(bool withPos);

    internal IEnumerable<Node> GetDescendants();

    internal Task LoadAbstractSyntaxTreeAsync(string source, string fileName, bool setChildren = true);
}
