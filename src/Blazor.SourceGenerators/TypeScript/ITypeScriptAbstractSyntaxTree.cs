// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.TypeScript;

public interface ITypeScriptAbstractSyntaxTree
{
    /// <summary>
    /// Gets the raw source text used to parse the abstract syntax tree.
    /// </summary>
    string RawSourceText { get; }

    /// <summary>
    /// Gets the root node (<see cref="RootNodeSourceFile"/>) of the abstract syntax tree.
    /// </summary>
    RootNodeSourceFile RootNode { get; }

    /// <summary>
    /// Gets the script target (<see cref="ScriptTarget"/>) of the abstract syntax tree."/>
    /// </summary>
    ScriptTarget Target { get; }
}
