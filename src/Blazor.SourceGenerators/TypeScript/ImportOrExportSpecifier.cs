// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface ImportOrExportSpecifier : IDeclaration
{
    internal Identifier PropertyName { get; set; }
}
