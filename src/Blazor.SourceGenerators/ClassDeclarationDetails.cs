// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

record class ClassDeclarationDetails(
    GeneratorOptions Options,
    ClassDeclarationSyntax ClassDeclaration,
    AttributeSyntax InteropAttribute);
