// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// In-memory implementation of <see cref="AdditionalText"/> used by
/// <c>TypeDeclarationSourcesTests</c> to drive the generator's
/// <c>AdditionalFiles</c> ingestion path without touching disk.
/// </summary>
internal sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) =>
        SourceText.From(content);
}
