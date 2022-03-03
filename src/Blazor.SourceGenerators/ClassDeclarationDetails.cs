// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Blazor.SourceGenerators;

record class ClassDeclarationDetails(
    GeneratorOptions Options,
    ClassDeclarationSyntax ClassDeclaration,
    AttributeSyntax InteropAttribute);
