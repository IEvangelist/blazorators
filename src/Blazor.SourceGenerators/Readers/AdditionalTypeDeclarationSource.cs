// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

/// <summary>
/// Value-equatable snapshot of an <c>AdditionalFile</c> ending in
/// <c>.d.ts</c>, captured at the generator pipeline boundary so the
/// incremental cache can compare inputs without holding a reference to
/// the Roslyn <c>AdditionalText</c> object (which is not value-equatable).
///
/// <para>
/// <see cref="FileName"/> is the basename of the file path - this is the
/// key consumers reference via
/// <c>JSAutoInteropAttribute.TypeDeclarationSources</c>. We accept either
/// the bare basename ("my.d.ts") or any trailing path segment match so
/// MSBuild-mangled paths still resolve.
/// </para>
/// </summary>
internal sealed record AdditionalTypeDeclarationSource(
    string Path,
    string FileName,
    string Content);
