// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.TypeScript;

public interface ITypeScriptAbstractSyntaxTree
{
    string SourceStr { get; set; }
    
    Node RootNode { get; set; }
    
    void ParseAsAst(string source, string fileName = "app.ts", bool setChildren = true);
}
