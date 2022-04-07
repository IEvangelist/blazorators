// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    string GetLocalFileText(string filePath) => File.ReadAllText(filePath);
}
